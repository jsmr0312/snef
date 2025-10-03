// CorreYGanaManager.cs (versión ajustada)
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
    public TextMeshProUGUI puntuacionText;   // "Obtuviste XXX"
    public TextMeshProUGUI recordText;       // opcional: "Mejor tiempo: 00:18.52"
    public Button btnJugarOtraVez;
    public Button btnContinuar;
    public string escenaContinuar = "SiguienteEscena";

    [Header("SFX Estrellas")]
    [Tooltip("AudioSource para reproducir el sonido de cada estrella (desactiva Play On Awake).")]
    public AudioSource sfxSource;
    [Tooltip("Clip de sonido (tu mp3 de estrella).")]
    public AudioClip estrellaSfx;
    [Range(0f, 1f)] public float estrellaSfxVolume = 1f;
    [Tooltip("Si está activo, sólo suena cuando la estrella es ganada (amarilla).")]
    public bool sfxSoloSiEstrellaGanada = true;
    [Tooltip("Hace que la 2a/3a estrella suenen un poquito más agudas (efecto 'ti-ri-ring').")]
    public bool sfxPitchAscendente = true;
    [Tooltip("Pitch base para la 1a estrella.")]
    public float sfxPitchBase = 1f;
    [Tooltip("Incremento de pitch por estrella (2a=base+step, 3a=base+2*step).")]
    public float sfxPitchStep = 0.07f;

    [Header("Umbrales de estrellas (segundos, MENOS es mejor)")]
    public float tiempo3Estrellas = 20f;     // <= 3★
    public float tiempo2Estrellas = 35f;     // <= 2★
    public float tiempo1Estrella = 50f;      // <= 1★

    [Header("Puntos TOTALES por estrellas (cumulativos)")]
    [Tooltip("Index 0..3 = total que corresponde a 0,1,2,3 estrellas. El premio de la partida es (totalNuevo - totalPrevio).")]
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
    [Tooltip("Si está activo, la UI muestra la MEJOR marca histórica; si se desactiva, muestra las estrellas de la partida actual.")]
    public bool mostrarMejorMarcaEnUI = true;

    [Header("Puntuación (Animación)")]
    [Tooltip("Duración del conteo animado de la puntuación (en segundos, usando tiempo real).")]
    public float scoreCountDuration = 0.8f;
    [Tooltip("Prefijo de la puntuación mostrada (p.ej. '+').")]
    public string scorePrefix = "+";
    [Tooltip("Texto a mostrar cuando ya no hay más puntos por ganar (3 estrellas alcanzadas históricamente y premio=0).")]
    public string completadoTexto = "COMPLETADO";

    // ---- estado interno ----
    float _tiempoRestante;
    bool _corriendo;
    bool _terminado;
    float _tardo;

    // === Métricas / sesión minijuego ===
    float _sessionStart;
    bool _victoriaMostrada;
    int _premioPartida;          // para enviar monedas en salida
    int _estrellasPartida;       // score para minijuego_finalizado

    // Cache de objetos reseteables (monedas, etc.)
    readonly List<ILevelResettable> _reseteables = new List<ILevelResettable>();

    // Corrutinas activas
    Coroutine _scoreRoutine;
    Coroutine _starsRoutine;

    void Awake()
    {

        // === Scope del id (stand::minijuego) y entrada ===
        {
            string baseId = string.IsNullOrWhiteSpace(minijuegoId)
                ? UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
                : minijuegoId;
            minijuegoId = MinigameScope.ScopedId(baseId);

            _sessionStart = Time.unscaledTime;
            _victoriaMostrada = false;
            _premioPartida = 0;
            _estrellasPartida = 0;

            // ENTRADA A MINIJUEGO (al entrar a la escena)
            if (MinigameScope.I)
                MetricsClient.I?.TrackEntradaMinijuego(MinigameScope.I.standId, MinigameScope.I.minigameName);
        }
        if (panelVictoria) panelVictoria.SetActive(false);
        if (panelDerrota) panelDerrota.SetActive(false);

        if (btnReintentarDerrota) btnReintentarDerrota.onClick.AddListener(Reintentar);
        if (btnAbandonar) btnAbandonar.onClick.AddListener(Abandonar);
        if (btnJugarOtraVez) btnJugarOtraVez.onClick.AddListener(Reintentar);
        if (btnContinuar) btnContinuar.onClick.AddListener(Continuar);
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

    // -------- FINISH (lo llama el FinishTrigger) --------
    public void NotificarMeta()
    {
        if (_terminado) return;
        _terminado = true;
        _corriendo = false;

        _tardo = tiempoNivel - _tiempoRestante;

        // --- calcular estrellas de la partida ---
        int estrellasPartida = CalcularEstrellas(_tardo);

        // --- registrar progreso y calcular premio ---
        int premio = 0;
        bool improvedStars, improvedTime;
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

        // --- mostrar UI ---
        MostrarCursor(true);
        Pausar(true);
        if (panelVictoria) panelVictoria.SetActive(true);

        // ... ya calculaste estrellasPartida, premio, mostraste panel, etc.

        _estrellasPartida = estrellasPartida;
        _premioPartida = Mathf.Max(0, premio);

        // TIEMPO EN MINIJUEGO (cuando se muestra la pantalla de victoria)
        if (!_victoriaMostrada)
        {
            _victoriaMostrada = true;
            int dur = Mathf.RoundToInt(Time.unscaledTime - _sessionStart);
            if (MinigameScope.I)
                MetricsClient.I?.TrackTiempoEnMinijuego(MinigameScope.I.standId, MinigameScope.I.minigameName, dur, true);
        }


        // Determinar si ya NO es posible ganar más puntos:
        // condición: premio==0 Y bestStars ya es 3 (máximo histórico alcanzado)
        bool sinMasPuntosPosibles = false;
        if (Stats.I != null)
        {
            var snap = Stats.I.GetProgress(minijuegoId); // ya actualizado tras RegisterMinigameResult
            sinMasPuntosPosibles = (premio <= 0) && (snap.bestStars >= 3);
        }

        // Texto de puntuación
        if (puntuacionText)
        {
            if (sinMasPuntosPosibles)
            {
                // Ya no hay forma de ganar más: muestra "completado"
                if (_scoreRoutine != null) StopCoroutine(_scoreRoutine);
                puntuacionText.text = completadoTexto;
            }
            else
            {
                // Aún se puede ganar (o se ganó en esta corrida): mostrar +N con animación
                if (_scoreRoutine != null) StopCoroutine(_scoreRoutine);
                _scoreRoutine = StartCoroutine(AnimarPuntuacion(premio));
            }
        }

        // Usa MEJOR marca para la UI si el toggle está activo
        int estrellasUI = estrellasPartida;
        if (Stats.I != null)
        {
            var snap = Stats.I.GetProgress(minijuegoId);
            if (mostrarMejorMarcaEnUI)
                estrellasUI = snap.bestStars;

            if (recordText)
                recordText.text = $"Mejor tiempo: {FormatearTiempo(snap.bestTime)}";
        }

        // Resetea y anima usando estrellasUI (no las de la partida)
        PrepararEstrellas();
        if (_starsRoutine != null) StopCoroutine(_starsRoutine);
        _starsRoutine = StartCoroutine(AnimarEstrellas(estrellasUI));
    }

    // -------- Derrota --------
    void PerderPorTiempo()
    {
        if (_terminado) return;
        _terminado = true;
        _corriendo = false;

        // (opcional) si quieres mandar tiempo aun en derrota, omite completed o mándalo false.
        // El usuario pidió tiempo al mostrar victoria, así que aquí NO lo mandamos.


        MostrarCursor(true);
        Pausar(true);
        if (panelDerrota) panelDerrota.SetActive(true);
    }

    // -------- Botones --------
    public void Reintentar()
    {
        Pausar(false);
        MostrarCursor(false);

        if (panelVictoria) panelVictoria.SetActive(false);
        if (panelDerrota) panelDerrota.SetActive(false);

        // Detener corrutinas de UI si quedaran vivas
        if (_scoreRoutine != null) { StopCoroutine(_scoreRoutine); _scoreRoutine = null; }
        if (_starsRoutine != null) { StopCoroutine(_starsRoutine); _starsRoutine = null; }

        // Respawn jugador
        if (respawner != null) respawner.Respawn();

        // Respawn monedas / reseteables
        ResetReseteables();

        ResetearNivel(false);
    }

    public void Abandonar()
    {
        {
            // SALIDA (finalizado) como LOSE (no hay premio)
            if (MinigameScope.I)
            {
                MetricsClient.I?.TrackMinijuegoFinalizado(
                    MinigameScope.I.standId,
                    MinigameScope.I.minigameName,
                    0, "lose", 0, 0
                );
            }
        }

        Pausar(false);
        MostrarCursor(false);
        SceneManager.LoadScene(escenaAbandonar);
    }

    public void Continuar()
    {

        {
            // SALIDA (finalizado) como WIN + monedas
            if (MinigameScope.I)
            {
                MetricsClient.I?.TrackMinijuegoFinalizado(
                    MinigameScope.I.standId,
                    MinigameScope.I.minigameName,
                    _estrellasPartida,
                    "win",
                    Mathf.Max(0, _premioPartida),
                    0
                );
                if (_premioPartida > 0)
                    MetricsClient.I?.TrackMonedasObtenidas(_premioPartida, "minijuego", MinigameScope.I.standId, MinigameScope.I.ecosystemName);
            }
        }

        Pausar(false);
        MostrarCursor(false);
        SceneManager.LoadScene(escenaContinuar);
    }

    // -------- Helpers --------
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

            // --- SFX: justo cuando empieza a "aparecer" la estrella ---
            if (estrellaSfx && sfxSource && (!sfxSoloSiEstrellaGanada || ganada))
            {
                sfxSource.pitch = sfxPitchAscendente ? (sfxPitchBase + sfxPitchStep * i) : 1f;
                sfxSource.PlayOneShot(estrellaSfx, estrellaSfxVolume);
            }

            // --- Animación de pop con tiempo real ---
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

    // --- Animación del contador de puntuación con prefijo "+" y tiempo real ---
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
