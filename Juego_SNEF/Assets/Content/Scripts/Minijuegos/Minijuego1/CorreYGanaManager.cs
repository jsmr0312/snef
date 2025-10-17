using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;
using System.Collections;

public class CorreYGanaManager : MonoBehaviour
{
    [Header("Tiempo de nivel")]
    public float tiempoNivel = 60f;
    public TextMeshProUGUI contadorText;

    [Header("Derrota")]
    public GameObject panelDerrota;
    public Button btnReintentarDerrota;
    public Button btnAbandonar;
    public string escenaAbandonar = "Lobby";

    [Header("Victoria")]
    public GameObject panelVictoria;
    public Image estrella1;
    public Image estrella2;
    public Image estrella3;
    public Color colorEstrellaGanada = new Color(1f, 0.84f, 0f);
    public Color colorEstrellaPerdida = new Color(0.4f, 0.4f, 0.4f);
    public TextMeshProUGUI puntuacionText;
    public TextMeshProUGUI recordText;
    public Button btnJugarOtraVez;
    public Button btnContinuar;
    public string escenaContinuar = "SiguienteEscena";

    [Header("SFX Estrellas")]
    public AudioSource sfxSource;
    public AudioClip estrellaSfx;
    [Range(0f, 1f)] public float estrellaSfxVolume = 1f;
    public bool sfxSoloSiEstrellaGanada = true;
    public bool sfxPitchAscendente = true;
    public float sfxPitchBase = 1f;
    public float sfxPitchStep = 0.07f;

    [Header("Umbrales de estrellas (segundos, MENOS es mejor)")]
    public float tiempo3Estrellas = 20f;
    public float tiempo2Estrellas = 35f;
    public float tiempo1Estrella = 50f;

    [Header("Puntos TOTALES por estrellas (cumulativos)")]
    public int[] puntosPorTotalEstrellas = new int[] { 0, 100, 200, 300 };

    [Header("A dónde acreditar el premio")]
    public bool acreditarEnPresupuesto = true;
    public bool acreditarEnPuntaje = false;

    [Header("Identificador de minijuego (para progreso)")]
    [Tooltip("Si lo dejas vacío, usará el nombre de la escena activa")]
    public string minijuegoId = "";

    [Header("Jugador / Control")]
    public FallRespawner respawner;
    public MonoBehaviour[] controladoresAInhabilitar;

    [Header("Visual de estrellas")]
    public bool mostrarMejorMarcaEnUI = true;

    [Header("Puntuación (Animación)")]
    public float scoreCountDuration = 0.8f;
    public string scorePrefix = "+";
    public string completadoTexto = "COMPLETADO";

    // ---- estado interno ----
    float _tiempoRestante;
    bool _corriendo;
    bool _terminado;
    float _tardo;

    // Métricas / salida
    float _sessionStart;
    bool _victoriaMostrada;
    int _premioPartida;
    int _estrellasPartida;

    // Cache reseteables
    readonly List<ILevelResettable> _reseteables = new List<ILevelResettable>();

    // Rutinas UI
    Coroutine _scoreRoutine;
    Coroutine _starsRoutine;

    public static CorreYGanaManager I { get; private set; }
    int _presupuestoPickups = 0;

    void Awake()
    {
        I = this;
        _presupuestoPickups = 0;

        // Scope id y ENTRADA
        string baseId = string.IsNullOrWhiteSpace(minijuegoId)
            ? SceneManager.GetActiveScene().name
            : minijuegoId;
        minijuegoId = MinigameScope.ScopedId(baseId);

        _sessionStart = Time.unscaledTime;
        _victoriaMostrada = false;
        _premioPartida = 0;
        _estrellasPartida = 0;

        if (MinigameScope.I)
        {
            MetricsClient.I?.TrackEntradaMinijuego(MinigameScope.I.standId, MinigameScope.I.minigameName);
            string friendly = string.IsNullOrEmpty(MinigameScope.I.minigameName)
                   ? UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
                   : MinigameScope.I.minigameName;
            MetricsClient.I?.TrackEscenaVisitada(
            "minigame",
            minijuegoId,                 // scoped id ya calculado arriba
            friendly,
            MinigameScope.I.ecosystemName
               );
        }


        if (panelVictoria) panelVictoria.SetActive(false);
        if (panelDerrota) panelDerrota.SetActive(false);

        if (btnReintentarDerrota) btnReintentarDerrota.onClick.AddListener(Reintentar);
        if (btnAbandonar) btnAbandonar.onClick.AddListener(Abandonar);
        if (btnJugarOtraVez) btnJugarOtraVez.onClick.AddListener(Reintentar);
        if (btnContinuar) btnContinuar.onClick.AddListener(Continuar);
    }

    void OnDestroy() { if (I == this) I = null; }

    public static void ReportPickup(int delta)
    {
        if (I != null && delta != 0) I._presupuestoPickups += delta;
    }

    void Start()
    {
        if (string.IsNullOrWhiteSpace(minijuegoId))
            minijuegoId = SceneManager.GetActiveScene().name;

        CacheReseteables();
        ResetearNivel(true);
    }

    void Update()
    {
        if (!_corriendo || _terminado) return;

        _tiempoRestante -= Time.deltaTime;
        if (_tiempoRestante <= 0f)
        {
            _tiempoRestante = 0f;
            PerderPorTiempo();
        }
        ActualizarContador();
    }

    // ========= FINISH =========
    public void NotificarMeta()
    {
        if (_terminado) return;
        _terminado = true;
        _corriendo = false;

        _tardo = tiempoNivel - _tiempoRestante;

        // estrellas de la PARTIDA
        int estrellasPartida = CalcularEstrellas(_tardo);

        // --- ROBUSTEZ: asegurar histórico previo (Stats ← Progress) ---
        int preBest = GetPreBest(minijuegoId);
        if (preBest > 0) Stats.I?.ImportMinigameBest(minijuegoId, preBest);

        // Registrar y calcular premio
        int premio = 0; bool improvedStars, improvedTime;
        if (Stats.I != null)
        {
            premio = Stats.I.RegisterMinigameResult(
                minijuegoId,
                estrellasPartida,
                _tardo,
                puntosPorTotalEstrellas,
                out improvedStars,
                out improvedTime
            );

            if (premio > 0)
            {
                if (acreditarEnPresupuesto) Stats.I.AddPresupuesto(premio);
                if (acreditarEnPuntaje) Stats.I.AddPuntaje(premio);
            }
        }

        // UI
        MostrarCursor(true);
        Pausar(true);
        if (panelVictoria) panelVictoria.SetActive(true);

        _estrellasPartida = estrellasPartida;
        _premioPartida = Mathf.Max(0, premio);

        if (!_victoriaMostrada)
        {
            _victoriaMostrada = true;
            int dur = Mathf.RoundToInt(Time.unscaledTime - _sessionStart);
            if (MinigameScope.I)
                MetricsClient.I?.TrackTiempoEnMinijuego(MinigameScope.I.standId, MinigameScope.I.minigameName, dur, true);
        }

        bool sinMasPuntosPosibles = false;
        int bestStarsNow = 0;
        if (Stats.I != null)
        {
            var snap = Stats.I.GetProgress(minijuegoId);
            bestStarsNow = snap.bestStars;
            sinMasPuntosPosibles = (premio <= 0) && (bestStarsNow >= 3);
        }

        if (puntuacionText)
        {
            if (sinMasPuntosPosibles)
            {
                if (_scoreRoutine != null) StopCoroutine(_scoreRoutine);
                puntuacionText.text = completadoTexto;
            }
            else
            {
                if (_scoreRoutine != null) StopCoroutine(_scoreRoutine);
                _scoreRoutine = StartCoroutine(AnimarPuntuacion(premio));
            }
        }

        // Estrellas UI → mejor marca real (Stats) o fallback al preBest
        int estrellasUI = mostrarMejorMarcaEnUI ? Mathf.Max(bestStarsNow, preBest) : estrellasPartida;

        if (recordText && Stats.I != null)
            recordText.text = $"Mejor tiempo: {FormatearTiempo(Stats.I.GetProgress(minijuegoId).bestTime)}";

        PrepararEstrellas();
        if (_starsRoutine != null) StopCoroutine(_starsRoutine);
        _starsRoutine = StartCoroutine(AnimarEstrellas(estrellasUI));

        // Misiones / Logros
        if (MinigameScope.I)
            MissionManager.I?.NotifyMinigameResultByStand(MinigameScope.I.standId, estrellasPartida);
        if (MinigameScope.I && estrellasPartida > 0)
            AchievementsManager.I?.NotifyMinigameCompletedType(MinigameScope.I.minigameId);
    }

    // ========= Derrota =========
    void PerderPorTiempo()
    {
        if (_terminado) return;
        _terminado = true;
        _corriendo = false;

        MostrarCursor(true);
        Pausar(true);
        if (panelDerrota) panelDerrota.SetActive(true);
    }

    // ========= Botones =========
    public void Reintentar()
    {
        Pausar(false);
        MostrarCursor(false);

        if (panelVictoria) panelVictoria.SetActive(false);
        if (panelDerrota) panelDerrota.SetActive(false);

        if (_scoreRoutine != null) { StopCoroutine(_scoreRoutine); _scoreRoutine = null; }
        if (_starsRoutine != null) { StopCoroutine(_starsRoutine); _starsRoutine = null; }

        if (respawner != null) respawner.Respawn();
        ResetReseteables();
        ResetearNivel(false);
    }
    public void Abandonar()
    {
        // Solo monedas recogidas
        int coins = Mathf.Max(0, _presupuestoPickups);
        if (MinigameScope.I)
        {
            MetricsClient.I?.TrackMinijuegoFinalizado(MinigameScope.I.standId, MinigameScope.I.minigameName, 0, "lose", coins, 0);
            if (coins > 0)
                MetricsClient.I?.TrackPresupuesto(coins, "minijuego", MinigameScope.I.standId, MinigameScope.I.ecosystemName);
        }

        Pausar(false);
        MostrarCursor(false);
        SceneManager.LoadScene(escenaAbandonar);
    }
    public void Continuar()
    {
        int coinsPrize = Mathf.Max(0, _premioPartida);
        int coinsPick = Mathf.Max(0, _presupuestoPickups);
        int coinsTotal = coinsPrize + coinsPick;

        if (MinigameScope.I)
        {
            MetricsClient.I?.TrackMinijuegoFinalizado(MinigameScope.I.standId, MinigameScope.I.minigameName, _estrellasPartida, "win", coinsTotal, 0);
            if (coinsPick > 0) MetricsClient.I?.TrackPresupuesto(coinsPick, "minijuego", MinigameScope.I.standId, MinigameScope.I.ecosystemName);
            if (coinsPrize > 0) MetricsClient.I?.TrackPresupuesto(coinsPrize, "minijuego", MinigameScope.I.standId, MinigameScope.I.ecosystemName);
        }

        Pausar(false);
        MostrarCursor(false);
        SceneManager.LoadScene(escenaContinuar);
    }

    // ========= Helpers =========
    int GetPreBest(string scopedId)
    {
        int pre = 0;
        if (Stats.I != null) pre = Stats.I.GetProgress(scopedId).bestStars;

        var list = ProgressCore.I?.Data?.minigames;
        if (list != null)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                if (m != null && m.id == scopedId)
                {
                    if (m.stars > pre) pre = m.stars;
                    break;
                }
            }
        }
        return pre;
    }

    void ResetearNivel(bool primeraVez)
    {
        _tiempoRestante = tiempoNivel;
        _tardo = 0f;
        _terminado = false;
        _corriendo = true;
        ActualizarContador();

        if (!primeraVez) PrepararEstrellas();
    }

    void ActualizarContador()
    {
        if (!contadorText) return;
        int t = Mathf.CeilToInt(_tiempoRestante);
        int m = t / 60;
        int s = t % 60;
        contadorText.text = $"{m:00}:{s:00}";
    }

    int CalcularEstrellas(float tiempoTardado)
    {
        if (tiempoTardado <= tiempo3Estrellas) return 3;
        if (tiempoTardado <= tiempo2Estrellas) return 2;
        if (tiempoTardado <= tiempo1Estrella) return 1;
        return 0;
    }

    void PrepararEstrellas()
    {
        if (estrella1) { estrella1.color = colorEstrellaPerdida; estrella1.transform.localScale = Vector3.zero; }
        if (estrella2) { estrella2.color = colorEstrellaPerdida; estrella2.transform.localScale = Vector3.zero; }
        if (estrella3) { estrella3.color = colorEstrellaPerdida; estrella3.transform.localScale = Vector3.zero; }
    }

    IEnumerator AnimarEstrellas(int cantidad)
    {
        Image[] arr = new Image[] { estrella1, estrella2, estrella3 };

        for (int i = 0; i < arr.Length; i++)
        {
            Image img = arr[i];
            if (img == null) continue;

            bool ganada = i < cantidad;
            Color c0 = colorEstrellaPerdida;
            Color c1 = ganada ? colorEstrellaGanada : colorEstrellaPerdida;

            if (estrellaSfx && sfxSource && (!sfxSoloSiEstrellaGanada || ganada))
            {
                sfxSource.pitch = sfxPitchAscendente ? (sfxPitchBase + sfxPitchStep * i) : 1f;
                sfxSource.PlayOneShot(estrellaSfx, estrellaSfxVolume);
            }

            float t = 0f, dur = 0.28f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / dur;
                float ease = Mathf.SmoothStep(0f, 1f, t);
                img.transform.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, ease);
                img.color = Color.Lerp(c0, c1, ease);
                yield return null;
            }
            img.transform.localScale = Vector3.one;
            img.color = c1;

            yield return new WaitForSecondsRealtime(0.12f);
        }
        if (sfxSource) sfxSource.pitch = 1f;
    }

    IEnumerator AnimarPuntuacion(int premio)
    {
        if (puntuacionText == null) yield break;

        float elapsed = 0f;
        int lastShown = -1;
        puntuacionText.text = $"{scorePrefix}0";

        while (elapsed < scoreCountDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / scoreCountDuration);
            int current = Mathf.RoundToInt(Mathf.Lerp(0, premio, t));
            if (current != lastShown)
            {
                puntuacionText.text = $"{scorePrefix}{current}";
                lastShown = current;
            }
            yield return null;
        }
        puntuacionText.text = $"{scorePrefix}{premio}";
    }

    void Pausar(bool pausa)
    {
        Time.timeScale = pausa ? 0f : 1f;
        if (controladoresAInhabilitar != null)
            foreach (var c in controladoresAInhabilitar)
                if (c) c.enabled = !pausa;
    }

    void MostrarCursor(bool visible)
    {
        Cursor.visible = visible;
        Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
    }

    void CacheReseteables()
    {
        _reseteables.Clear();
        var monos = GameObject.FindObjectsOfType<MonoBehaviour>(true);
        foreach (var m in monos)
        {
            if (m is ILevelResettable r && m.gameObject.scene.IsValid())
                _reseteables.Add(r);
        }
    }

    void ResetReseteables()
    {
        foreach (var r in _reseteables) r?.ResetState();
    }

    string FormatearTiempo(float seconds)
    {
        if (float.IsInfinity(seconds) || seconds <= 0f) return "--:--.--";
        int m = (int)(seconds / 60f);
        float s = seconds % 60f;
        return $"{m:00}:{s:00.00}";
    }


}
