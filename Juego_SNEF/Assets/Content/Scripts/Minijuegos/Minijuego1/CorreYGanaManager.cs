// CorreYGanaManager.cs (REEMPLAZA por esta versión)
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
    public float tiempo1Estrella = 50f;     // <= 1★

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


    // ---- estado interno ----
    float _tiempoRestante;
    bool _corriendo;
    bool _terminado;
    float _tardo;

    // Cache de objetos reseteables (monedas, etc.)
    readonly List<ILevelResettable> _reseteables = new List<ILevelResettable>();

    void Awake()
    {
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

        // --- registrar progreso y calcular premio (igual que antes) ---
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
        if (puntuacionText) puntuacionText.text = premio.ToString();

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
        StartCoroutine(AnimarEstrellas(estrellasUI));

    }

    // -------- Derrota --------
    void PerderPorTiempo()
    {
        if (_terminado) return;
        _terminado = true;
        _corriendo = false;

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

        // Respawn jugador
        if (respawner != null) respawner.Respawn();

        // Respawn monedas / reseteables
        ResetReseteables();

        ResetearNivel(false);
    }

    public void Abandonar()
    {
        Pausar(false);
        MostrarCursor(false);
        SceneManager.LoadScene(escenaAbandonar);
    }

    public void Continuar()
    {
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
            // (Usa OneShot para que no se corte aunque suenen seguidas)
            if (estrellaSfx && sfxSource && (!sfxSoloSiEstrellaGanada || ganada))
            {
                sfxSource.pitch = sfxPitchAscendente ? (sfxPitchBase + sfxPitchStep * i) : 1f;
                sfxSource.PlayOneShot(estrellaSfx, estrellaSfxVolume);
            }

            // --- Animación de pop (no escalada para que funcione con Time.timeScale=0) ---
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

            // Pequeño delay entre estrellas (tiempo real)
            yield return new WaitForSecondsRealtime(0.12f);
        }

        // Deja el pitch normal para futuras reproducciones
        if (sfxSource) sfxSource.pitch = 1f;
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
