using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class LluviaObjetosManager : MonoBehaviour
{
    [Header("Identificador")]
    public string minijuegoId = "lluvia_objetos";

    [Header("Visual/Scale")]
    public float itemWorldScale = 10f;

    [Header("Despawn")]
    public float killMarginBelowArea = 3f;

    [Header("Tiempo")]
    public bool usarTiempo = true;
    public float tiempoNivel = 60f;
    public TextMeshProUGUI tiempoText;
    public bool finalizarAlAgotarTiempo = true;

    [Header("UI Derrota / Victoria")]
    public GameObject panelDerrota;
    public Button btnReintentarDerrota;
    public Button btnAbandonar;
    public string escenaAbandonar = "Lobby";

    public GameObject panelVictoria;
    public Image estrella1, estrella2, estrella3;
    public Color colorEstrellaGanada = new Color(1f, 0.84f, 0f);
    public Color colorEstrellaPerdida = new Color(0.4f, 0.4f, 0.4f);
    public TextMeshProUGUI puntuacionText;
    public TextMeshProUGUI recordText;
    public Button btnReintentar;
    public Button btnContinuar;
    public string escenaContinuar = "SiguienteEscena";

    [Header("Popup presupuesto (+/-)")]
    public BudgetPopupUI presupuestoPopup;

    [Header("Animación de puntuación final")]
    public float scoreCountDuration = 0.8f;
    public string scorePrefix = "+";
    public string completadoTexto = "COMPLETADO";
    public bool mostrarMejorMarcaEnUI = true;

    [Header("SFX Estrellas")]
    public AudioSource sfxSource;
    public AudioClip estrellaSfx;
    [Range(0f, 1f)] public float estrellaSfxVolume = 1f;
    public bool sfxSoloSiEstrellaGanada = true;
    public bool sfxPitchAscendente = true;
    public float sfxPitchBase = 1f;
    public float sfxPitchStep = 0.07f;

    [Header("Premio final por estrellas (cumulativo)")]
    public int[] puntosPorTotalEstrellas = new int[] { 0, 100, 200, 300 };
    public bool acreditarEnPresupuesto = true;
    public bool acreditarEnPuntaje = false;

    [Header("Pago por jugada")]
    public int aciertosPorPremio = 5;
    public int premioPorBloque = 20;
    public int penalizacionMalo = 10;
    [Header("A dónde acreditar RESPUESTAS")]
    public bool respuestasEnPresupuesto = true;
    public bool respuestasEnPuntaje = false;

    [Header("HUD de aciertos (opcional)")]
    public TextMeshProUGUI aciertosText;

    [Header("Spawning")]
    public BoxCollider spawnArea;
    public float spawnsPorSegundo = 2.0f;
    [Range(0f, 1f)] public float probBueno = 0.6f;
    public FallingItem itemPrefab;
    public int poolSize = 20;
    public bool usarRigidbody = true;
    public float fallSpeed = 4.5f;

    [Header("Catálogo (clave → sprite)")]
    public List<SpriteCatalogEntry> buenosCatalog = new List<SpriteCatalogEntry>();
    public List<SpriteCatalogEntry> malosCatalog = new List<SpriteCatalogEntry>();

    [Header("Estrellas (auto o manual)")]
    public bool autoCalcularUmbrales = true;
    [Range(0.1f, 1f)] public float eficienciaEsperada = 0.6f;
    [Range(0.1f, 1f)] public float factor1 = 0.4f;
    [Range(0.1f, 1f)] public float factor2 = 0.7f;
    [Range(0.1f, 1f)] public float factor3 = 0.9f;

    public int umbral1 = 5;
    public int umbral2 = 10;
    public int umbral3 = 15;

    [Header("Control (opcional)")]
    public MonoBehaviour[] controladoresAInhabilitar;

    // ---- Estado interno ----
    float _tRestante;
    bool _corriendo;
    bool _terminado;

    // Métricas / salida
    float _sessionStart;
    bool _victoriaMostrada;
    int _premioPartida;
    int _estrellasPartida;

    int _atrapadosBuenos;
    int _bloquesAcreditados;

    // Pool
    readonly List<FallingItem> _pool = new List<FallingItem>();
    readonly List<FallingItem> _activos = new List<FallingItem>();

    // Diccionarios de sprites
    Dictionary<string, Sprite> _dicBuenos = new Dictionary<string, Sprite>();
    Dictionary<string, Sprite> _dicMalos = new Dictionary<string, Sprite>();

    // Rutinas
    Coroutine _spawnRoutine;
    Coroutine _scoreRoutine;
    Coroutine _starsRoutine;

    void Awake()
    {
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
            MetricsClient.I?.TrackEntradaMinijuego(MinigameScope.I.standId, MinigameScope.I.minigameName);

        if (panelDerrota) panelDerrota.SetActive(false);
        if (panelVictoria) panelVictoria.SetActive(false);

        if (btnReintentarDerrota) btnReintentarDerrota.onClick.AddListener(Reintentar);
        if (btnAbandonar) btnAbandonar.onClick.AddListener(Abandonar);
        if (btnReintentar) btnReintentar.onClick.AddListener(Reintentar);
        if (btnContinuar) btnContinuar.onClick.AddListener(Continuar);

        foreach (var e in buenosCatalog) if (!string.IsNullOrEmpty(e.key) && e.sprite) _dicBuenos[e.key] = e.sprite;
        foreach (var e in malosCatalog) if (!string.IsNullOrEmpty(e.key) && e.sprite) _dicMalos[e.key] = e.sprite;
    }

    void Start()
    {
        if (autoCalcularUmbrales)
        {
            float esperados = tiempoNivel * spawnsPorSegundo * probBueno * Mathf.Clamp01(eficienciaEsperada);
            umbral1 = Mathf.Max(1, Mathf.CeilToInt(esperados * factor1));
            umbral2 = Mathf.Max(umbral1 + 1, Mathf.CeilToInt(esperados * factor2));
            umbral3 = Mathf.Max(umbral2 + 1, Mathf.CeilToInt(esperados * factor3));
        }

        for (int i = 0; i < poolSize; i++)
        {
            var go = Instantiate(itemPrefab, transform);
            go.gameObject.SetActive(false);
            go.Init(this, usarRigidbody, fallSpeed);
            _pool.Add(go);
        }

        _tRestante = tiempoNivel;
        _atrapadosBuenos = 0;
        _bloquesAcreditados = 0;
        _corriendo = true;
        _terminado = false;
        ActualizarReloj();
        ActualizarAciertosHUD();

        _spawnRoutine = StartCoroutine(SpawnerLoop());

        MostrarCursor(false);
        Pausar(false);
    }

    void Update()
    {
        if (!_corriendo || _terminado || !usarTiempo) return;

        _tRestante -= Time.deltaTime;
        if (_tRestante <= 0f)
        {
            _tRestante = 0f;
            ActualizarReloj();
            if (finalizarAlAgotarTiempo) TerminarJuego();
            else PerderPorTiempo();
            return;
        }
        ActualizarReloj();
    }

    IEnumerator SpawnerLoop()
    {
        var area = spawnArea ? spawnArea.bounds : new Bounds(Vector3.zero, new Vector3(6, 1, 6));
        float topY = area.max.y;

        while (!_terminado)
        {
            float delay = 1f / Mathf.Max(0.01f, spawnsPorSegundo);
            yield return new WaitForSeconds(delay);

            bool esBueno = Random.value < probBueno;

            string key = esBueno ? PickKey(_dicBuenos) : PickKey(_dicMalos);
            Sprite spr = null;
            if (esBueno) _dicBuenos.TryGetValue(key, out spr);
            else _dicMalos.TryGetValue(key, out spr);

            float x = Random.Range(area.min.x, area.max.x);
            float z = Random.Range(area.min.z, area.max.z);
            Vector3 pos = new Vector3(x, topY, z);

            var item = TakeFromPool();
            item.Setup(esBueno, key, spr, pos);
            _activos.Add(item);
        }
    }

    string PickKey(Dictionary<string, Sprite> dict)
    {
        if (dict.Count == 0) return "ITEM";
        int idx = Random.Range(0, dict.Count);
        int i = 0; foreach (var k in dict.Keys) { if (i == idx) return k; i++; }
        return "ITEM";
    }

    FallingItem TakeFromPool()
    {
        for (int i = 0; i < _pool.Count; i++)
            if (!_pool[i].gameObject.activeSelf) return _pool[i];

        var extra = Instantiate(itemPrefab, transform);
        extra.Init(this, usarRigidbody, fallSpeed);
        _pool.Add(extra);
        return extra;
    }

    public void DevolverAlPool(FallingItem item)
    {
        item.gameObject.SetActive(false);
        _activos.Remove(item);
    }

    // ==== Eventos desde FallingItem ====
    public void OnItemCatched(FallingItem item, bool esBueno)
    {
        if (!_corriendo || _terminado) return;

        if (esBueno)
        {
            _atrapadosBuenos++;
            ActualizarAciertosHUD();

            int bloquesActuales = _atrapadosBuenos / Mathf.Max(1, aciertosPorPremio);
            int nuevosBloques = bloquesActuales - _bloquesAcreditados;
            if (nuevosBloques > 0)
            {
                int monto = nuevosBloques * premioPorBloque;
                AcreditarDelta(monto);
                _bloquesAcreditados = bloquesActuales;
                if (presupuestoPopup) presupuestoPopup.Show(monto);
            }
        }
        else
        {
            int monto = -Mathf.Abs(penalizacionMalo);
            AcreditarDelta(monto);
            if (presupuestoPopup) presupuestoPopup.Show(monto);
        }
    }

    public void OnItemOutOfBounds(FallingItem item) => DevolverAlPool(item);

    void AcreditarDelta(int delta)
    {
        if (Stats.I == null) return;
        if (respuestasEnPresupuesto) Stats.I.AddPresupuesto(delta);
        if (respuestasEnPuntaje) Stats.I.AddPuntaje(delta);
    }

    // ==== Fin de juego ====
    void TerminarJuego()
    {
        if (_terminado) return;
        _terminado = true;
        _corriendo = false;

        if (_spawnRoutine != null) StopCoroutine(_spawnRoutine);

        int estrellasPartida = CalcularEstrellas(_atrapadosBuenos);

        // robustez: asegurar histórico previo
        int preBest = GetPreBest(minijuegoId);
        if (preBest > 0) Stats.I?.ImportMinigameBest(minijuegoId, preBest);

        int premio = 0; bool improvedStars, improvedTime;
        if (Stats.I != null)
        {
            premio = Stats.I.RegisterMinigameResult(
                minijuegoId,
                estrellasPartida,
                0f,
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

        MostrarCursor(true);
        Pausar(true);
        if (panelDerrota) panelDerrota.SetActive(false);
        if (panelVictoria) panelVictoria.SetActive(true);

        bool sinMasPuntos = false;
        int bestStarsNow = 0;
        if (Stats.I != null)
        {
            var snap = Stats.I.GetProgress(minijuegoId);
            bestStarsNow = snap.bestStars;
            sinMasPuntos = (premio <= 0) && (snap.bestStars >= 3);
        }

        if (puntuacionText)
        {
            if (_scoreRoutine != null) StopCoroutine(_scoreRoutine);
            if (sinMasPuntos) puntuacionText.text = completadoTexto;
            else _scoreRoutine = StartCoroutine(AnimarPuntuacion(premio));
        }

        int estrellasUI = mostrarMejorMarcaEnUI ? Mathf.Max(bestStarsNow, preBest) : estrellasPartida;

        if (recordText) recordText.text = $"Atrapados: {_atrapadosBuenos}";
        PrepararEstrellas();
        if (_starsRoutine != null) StopCoroutine(_starsRoutine);
        _starsRoutine = StartCoroutine(AnimarEstrellas(estrellasUI));

        _estrellasPartida = estrellasPartida;
        _premioPartida = Mathf.Max(0, premio);

        if (!_victoriaMostrada)
        {
            _victoriaMostrada = true;
            int dur = Mathf.RoundToInt(Time.unscaledTime - _sessionStart);
            if (MinigameScope.I)
                MetricsClient.I?.TrackTiempoEnMinijuego(MinigameScope.I.standId, MinigameScope.I.minigameName, dur, true);
        }

        if (MinigameScope.I)
            MissionManager.I?.NotifyMinigameResultByStand(MinigameScope.I.standId, estrellasPartida);
        if (MinigameScope.I && estrellasPartida > 0)
            AchievementsManager.I?.NotifyMinigameCompletedType(MinigameScope.I.minigameId);
    }

    void PerderPorTiempo()
    {
        if (_terminado) return;
        _terminado = true;
        _corriendo = false;

        if (_spawnRoutine != null) StopCoroutine(_spawnRoutine);

        MostrarCursor(true);
        Pausar(true);
        if (panelVictoria) panelVictoria.SetActive(false);
        if (panelDerrota) panelDerrota.SetActive(true);
    }

    // ==== Utilidades UI / Estrellas / Tiempo ====
    void ActualizarReloj()
    {
        if (!tiempoText) return;
        int t = Mathf.CeilToInt(_tRestante);
        int m = t / 60;
        int s = t % 60;
        tiempoText.text = $"{m:00}:{s:00}";
    }

    void ActualizarAciertosHUD()
    {
        if (aciertosText) aciertosText.text = _atrapadosBuenos.ToString();
    }

    int CalcularEstrellas(int buenos)
    {
        if (buenos >= umbral3) return 3;
        if (buenos >= umbral2) return 2;
        if (buenos >= umbral1) return 1;
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
            var img = arr[i]; if (!img) continue;

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

    // ==== Botones ====
    public void Reintentar()
    {
        Pausar(false); MostrarCursor(false);
        IntroScreenController.skipNextIntro = true;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    public void Continuar()
    {
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

        Pausar(false); MostrarCursor(false);
        if (!string.IsNullOrEmpty(escenaContinuar)) SceneManager.LoadScene(escenaContinuar);
    }
    public void Abandonar()
    {
        if (MinigameScope.I)
            MetricsClient.I?.TrackMinijuegoFinalizado(MinigameScope.I.standId, MinigameScope.I.minigameName, 0, "lose", 0, 0);

        Pausar(false); MostrarCursor(false);
        if (!string.IsNullOrEmpty(escenaAbandonar)) SceneManager.LoadScene(escenaAbandonar);
    }

    // ==== Helpers comunes ====
    void Pausar(bool pausa)
    {
        Time.timeScale = pausa ? 0f : 1f;
        if (controladoresAInhabilitar != null)
            foreach (var c in controladoresAInhabilitar) if (c) c.enabled = !pausa;
    }
    void MostrarCursor(bool visible)
    {
        Cursor.visible = visible;
        Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!spawnArea) return;
        var b = spawnArea.bounds;
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(b.center, b.size);
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(new Vector3(b.min.x, b.max.y, b.min.z), new Vector3(b.max.x, b.max.y, b.min.z));
        Gizmos.DrawLine(new Vector3(b.min.x, b.max.y, b.max.z), new Vector3(b.max.x, b.max.y, b.max.z));
    }
#endif

    [System.Serializable] public class SpriteCatalogEntry { public string key; public Sprite sprite; }

    // ======= util compartida con otros managers =======
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
}
