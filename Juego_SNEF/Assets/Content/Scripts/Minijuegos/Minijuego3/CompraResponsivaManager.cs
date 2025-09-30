using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class CompraResponsivaManager : MonoBehaviour
{
    [Header("Identificador de minijuego")]
    public string minijuegoId = "compra_responsiva";

    [Header("Tiempo de nivel")]
    public bool usarTiempo = true;
    public float tiempoNivel = 60f;
    public TextMeshProUGUI tiempoText;  // mm:ss en cabecera

    [Header("Derrota (por tiempo)")]
    public GameObject panelDerrota;
    public Button btnReintentarDerrota;
    public Button btnAbandonar;
    public string escenaAbandonar = "Lobby";

    [Header("Victoria")]
    public GameObject panelVictoria;
    public Image estrella1, estrella2, estrella3;
    public Color colorEstrellaGanada = new Color(1f, 0.84f, 0f);
    public Color colorEstrellaPerdida = new Color(0.4f, 0.4f, 0.4f);
    public TextMeshProUGUI puntuacionText;   // "+N" o "COMPLETADO"
    public TextMeshProUGUI recordText;       // "Correctas: X/10"
    public Button btnReintentar;
    public Button btnContinuar;
    public string escenaContinuar = "SiguienteEscena";

    [Header("Puntuación final (UI)")]
    public float scoreCountDuration = 0.8f;
    public string scorePrefix = "+";
    public string completadoTexto = "COMPLETADO";
    public bool mostrarMejorMarcaEnUI = true; // como en runner, usa bestStars para mostrar

    [Header("SFX Estrellas")]
    public AudioSource sfxSource;
    public AudioClip estrellaSfx;
    [Range(0f, 1f)] public float estrellaSfxVolume = 1f;
    public bool sfxSoloSiEstrellaGanada = true;
    public bool sfxPitchAscendente = true;
    public float sfxPitchBase = 1f;
    public float sfxPitchStep = 0.07f;

    [Header("Puntos TOTALES por estrellas (cumulativos)")]
    [Tooltip("Index 0..3 = total que corresponde a 0,1,2,3 estrellas. El premio es (totalNuevo - totalPrevio).")]
    public int[] puntosPorTotalEstrellas = new int[] { 0, 100, 200, 300 };

    [Header("A dónde acreditar el premio por estrellas")]
    public bool acreditarEnPresupuesto = true;
    public bool acreditarEnPuntaje = false;

    [Header("Rondas")]
    public int rondasTotales = 10;

    [Header("Economía por respuesta")]
    public int premioCorrecta = 20;
    public int penalizacionIncorrecta = 10;

    [Header("UI Pregunta")]
    public TextMeshProUGUI preguntaText;
    public TextMeshProUGUI contadorText; // "1/10"
    public GameObject panelPregunta;

    [Header("Sprites (clave → sprite)")]
    public List<SpriteCatalogEntry> spriteCatalog = new List<SpriteCatalogEntry>();

    [Header("Banco de preguntas (30)")]
    public List<QuestionDef> bancoPreguntas = new List<QuestionDef>();

    [Header("Opciones en Escena (reusar tus Opcion1/2/3)")]
    public bool usarSlotsDeEscena = true;
    public OptionItem[] optionSlots = new OptionItem[3];

    [Header("Instanciación (si prefieres prefabs)")]
    public Transform[] spawnPoints;
    public OptionItem optionPrefab;

    [Header("Jugador / Control (opcional)")]
    public MonoBehaviour[] controladoresAInhabilitar;

    // ---- estado interno ----
    private int _rondaActual = 0;
    private int _correctas = 0;
    private List<QuestionDef> _preguntasElegidas = new List<QuestionDef>();
    private List<OptionItem> _instanciadas = new List<OptionItem>();
    private Dictionary<string, Sprite> _dictSprites;

    private float _tiempoRestante;
    private bool _corriendo;
    private bool _terminado;

    // corrutinas UI
    Coroutine _scoreRoutine;
    Coroutine _starsRoutine;

    void Awake()
    {
        if (panelVictoria) panelVictoria.SetActive(false);
        if (panelDerrota) panelDerrota.SetActive(false);
        if (panelPregunta) panelPregunta.SetActive(true);

        if (btnReintentar) btnReintentar.onClick.AddListener(Reintentar);
        if (btnContinuar) btnContinuar.onClick.AddListener(Continuar);
        if (btnReintentarDerrota) btnReintentarDerrota.onClick.AddListener(Reintentar);
        if (btnAbandonar) btnAbandonar.onClick.AddListener(Abandonar);

        // catálogo → diccionario
        _dictSprites = new Dictionary<string, Sprite>();
        foreach (var e in spriteCatalog)
            if (!string.IsNullOrEmpty(e.key) && e.sprite && !_dictSprites.ContainsKey(e.key))
                _dictSprites.Add(e.key, e.sprite);
    }

    void Start()
    {
        // elegir 10 sin repetición
        _preguntasElegidas = new List<QuestionDef>(bancoPreguntas);
        Shuffle(_preguntasElegidas);
        if (_preguntasElegidas.Count > rondasTotales)
            _preguntasElegidas.RemoveRange(rondasTotales, _preguntasElegidas.Count - rondasTotales);

        _rondaActual = 0;
        _correctas = 0;

        _tiempoRestante = tiempoNivel;
        _terminado = false;
        _corriendo = true;
        ActualizarReloj();

        MostrarRonda();
    }

    void Update()
    {
        if (!_corriendo || _terminado || !usarTiempo) return;

        _tiempoRestante -= Time.deltaTime;
        if (_tiempoRestante <= 0f)
        {
            _tiempoRestante = 0f;
            ActualizarReloj();
            PerderPorTiempo();          // === igual que el runner ===
            return;
        }
        ActualizarReloj();
    }

    // -------- Derrota por tiempo (clonado del runner) --------
    void PerderPorTiempo()
    {
        if (_terminado) return;
        _terminado = true;
        _corriendo = false;

        // Deshabilita interacción de opciones actuales
        foreach (var it in _instanciadas) it.SetInteractable(false);

        // UI
        MostrarCursor(true);
        Pausar(true);
        if (panelPregunta) panelPregunta.SetActive(false);
        if (panelDerrota) panelDerrota.SetActive(true);
    }

    // ----------------- RONDAS -----------------
    void MostrarRonda()
    {
        if (_terminado) return;

        LimpiarOpciones();
        if (_rondaActual >= _preguntasElegidas.Count)
        {
            TerminarJuego();
            return;
        }

        var q = _preguntasElegidas[_rondaActual];
        if (preguntaText) preguntaText.text = q.pregunta;
        if (contadorText) contadorText.text = $"{_rondaActual + 1}/{rondasTotales}";

        var opciones = new List<OptionTriple>()
        {
            new OptionTriple(q.correctoKey,    q.correctoNombre,    true),
            new OptionTriple(q.incorrecto1Key, q.incorrecto1Nombre, false),
            new OptionTriple(q.incorrecto2Key, q.incorrecto2Nombre, false),
        };
        Shuffle(opciones);

        if (usarSlotsDeEscena)
        {
            for (int i = 0; i < optionSlots.Length && i < opciones.Count; i++)
            {
                var slot = optionSlots[i];
                var data = opciones[i];

                Sprite spr = null; _dictSprites.TryGetValue(data.key, out spr);
                string cantidadLbl = data.isCorrect ? $"+{premioCorrecta}" : $"-{penalizacionIncorrecta}";

                slot.gameObject.SetActive(true);
                slot.Setup(this, data.key, data.displayName, spr, data.isCorrect, cantidadLbl);
                slot.SetInteractable(true);
                _instanciadas.Add(slot);
            }
        }
        else
        {
            for (int i = 0; i < spawnPoints.Length && i < opciones.Count; i++)
            {
                var sp = spawnPoints[i];
                var data = opciones[i];

                Sprite spr = null; _dictSprites.TryGetValue(data.key, out spr);
                string cantidadLbl = data.isCorrect ? $"+{premioCorrecta}" : $"-{penalizacionIncorrecta}";

                var item = Instantiate(optionPrefab, sp.position, sp.rotation, sp.parent);
                item.Setup(this, data.key, data.displayName, spr, data.isCorrect, cantidadLbl);
                _instanciadas.Add(item);
            }
        }
    }

    public void OnOptionChosen(OptionItem chosen)
    {
        if (_terminado) return;

        foreach (var it in _instanciadas) it.SetInteractable(false);

        bool ok = chosen.IsCorrect;
        if (ok)
        {
            _correctas++;
            if (Stats.I) Stats.I.AddPresupuesto(premioCorrecta);
        }
        else
        {
            if (Stats.I) Stats.I.AddPresupuesto(-penalizacionIncorrecta);
        }

        StartCoroutine(ResolverYContinuar(chosen, ok));
    }

    IEnumerator ResolverYContinuar(OptionItem chosen, bool ok)
    {
        yield return chosen.PlayCollectAnimAndHide(ok ? $"+{premioCorrecta}" : $"-{penalizacionIncorrecta}");

        foreach (var it in _instanciadas)
        {
            if (it && it != chosen)
            {
                if (usarSlotsDeEscena) it.gameObject.SetActive(false);
                else Destroy(it.gameObject);
            }
        }
        _instanciadas.Clear();

        _rondaActual++;
        yield return new WaitForSeconds(0.2f);
        MostrarRonda();
    }

    // --------------- VICTORIA / FIN ---------------
    void TerminarJuego()
    {
        if (_terminado) return;
        _terminado = true;
        _corriendo = false;

        int estrellasPartida = CalcularEstrellasPorCorrectas(_correctas);

        // Registrar y calcular premio por estrellas (como runner)
        int premio = 0; bool improvedStars, improvedTime;
        if (Stats.I != null)
        {
            premio = Stats.I.RegisterMinigameResult(
                minijuegoId,
                estrellasPartida,
                0f, // sin tiempo
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
        if (panelPregunta) panelPregunta.SetActive(false);
        if (panelVictoria) panelVictoria.SetActive(true);

        // "COMPLETADO" si ya no hay más puntos por ganar
        if (puntuacionText)
        {
            bool sinMasPuntosPosibles = false;
            if (Stats.I != null)
            {
                var snap = Stats.I.GetProgress(minijuegoId); // ya actualizado
                sinMasPuntosPosibles = (premio <= 0) && (snap.bestStars >= 3);
            }

            if (_scoreRoutine != null) StopCoroutine(_scoreRoutine);
            if (sinMasPuntosPosibles)
                puntuacionText.text = completadoTexto;
            else
                _scoreRoutine = StartCoroutine(AnimarPuntuacion(premio));
        }

        // estrellas a mostrar (best o partida)
        int estrellasUI = estrellasPartida;
        if (Stats.I != null && mostrarMejorMarcaEnUI)
        {
            var snap = Stats.I.GetProgress(minijuegoId);
            estrellasUI = snap.bestStars;
        }

        if (recordText) recordText.text = $"Correctas: {_correctas}/{rondasTotales}";

        PrepararEstrellas();
        if (_starsRoutine != null) StopCoroutine(_starsRoutine);
        _starsRoutine = StartCoroutine(AnimarEstrellas(estrellasUI));
    }

    int CalcularEstrellasPorCorrectas(int ok)
    {
        if (ok >= 9) return 3;
        if (ok > 5) return 2;
        if (ok > 1) return 1;
        return 0;
    }

    // ----- Reloj -----
    void ActualizarReloj()
    {
        if (!tiempoText) return;
        int t = Mathf.CeilToInt(_tiempoRestante);
        int m = t / 60;
        int s = t % 60;
        tiempoText.text = $"{m:00}:{s:00}";
    }

    // ----- Estrellas (idéntico al runner) -----
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
            var img = arr[i];
            if (!img) continue;

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

    // --- Animación del contador de puntuación con prefijo "+" (tiempo real) ---
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

    // -------- Botones --------
    public void Reintentar()
    {
        Pausar(false);
        MostrarCursor(false);

        if (panelVictoria) panelVictoria.SetActive(false);
        if (panelDerrota) panelDerrota.SetActive(false);

        // cortar corrutinas UI si quedaran vivas
        if (_scoreRoutine != null) { StopCoroutine(_scoreRoutine); _scoreRoutine = null; }
        if (_starsRoutine != null) { StopCoroutine(_starsRoutine); _starsRoutine = null; }

        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void Continuar()
    {
        Pausar(false);
        MostrarCursor(false);
        if (!string.IsNullOrEmpty(escenaContinuar))
            SceneManager.LoadScene(escenaContinuar);
    }

    public void Abandonar()
    {
        Pausar(false);
        MostrarCursor(false);
        if (!string.IsNullOrEmpty(escenaAbandonar))
            SceneManager.LoadScene(escenaAbandonar);
    }

    // -------- Helpers comunes al runner --------
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

    void LimpiarOpciones()
    {
        foreach (var it in _instanciadas)
        {
            if (!it) continue;
            if (usarSlotsDeEscena) it.gameObject.SetActive(false);
            else Destroy(it.gameObject);
        }
        _instanciadas.Clear();
    }

    void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // ----- tipos auxiliares -----
    [System.Serializable] public class SpriteCatalogEntry { public string key; public Sprite sprite; }
    [System.Serializable]
    public class QuestionDef
    {
        [TextArea(2, 3)] public string pregunta;
        public string correctoKey, incorrecto1Key, incorrecto2Key;
        public string correctoNombre, incorrecto1Nombre, incorrecto2Nombre;
    }
    struct OptionTriple
    {
        public string key; public string displayName; public bool isCorrect;
        public OptionTriple(string k, string d, bool c) { key = k; displayName = string.IsNullOrEmpty(d) ? k : d; isCorrect = c; }
    }
}
