using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
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
    public Button btnJugarOtraVez;
    public Button btnContinuar;
    public string escenaContinuar = "SiguienteEscena";

    [Header("Umbrales de estrellas (segundos, MENOS es mejor)")]
    public float tiempo3Estrellas = 20f; // <= 3★
    public float tiempo2Estrellas = 35f; // <= 2★
    public float tiempo1Estrella = 50f; // <= 1★

    [Header("Puntos según estrellas (configurable)")]
    public int puntos1Estrella = 100;
    public int puntos2Estrellas = 200;
    public int puntos3Estrellas = 300;
    [Tooltip("Marca esto si quieres que se acrediten puntos automáticamente al ganar")]
    public bool acreditarPuntosAlGanar = true;

    [Header("Jugador / Control")]
    public FallRespawner respawner;               // Tu script de respawn
    public MonoBehaviour[] controladoresAInhabilitar; // p.ej. movimiento/cámara

    // ---- estado interno ----
    float _tiempoRestante;
    bool _corriendo;
    bool _terminado;
    float _tardo;
    int _ultimoPuntaje;

    void Awake()
    {
        if (panelVictoria) panelVictoria.SetActive(false);
        if (panelDerrota) panelDerrota.SetActive(false);

        if (btnReintentarDerrota) btnReintentarDerrota.onClick.AddListener(Reintentar);
        if (btnAbandonar) btnAbandonar.onClick.AddListener(Abandonar);
        if (btnJugarOtraVez) btnJugarOtraVez.onClick.AddListener(Reintentar);
        if (btnContinuar) btnContinuar.onClick.AddListener(Continuar);
    }

    void Start() => ResetearNivel(true);

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

    // =========== LLAMADO por el FinishTrigger ===========
    public void NotificarMeta()
    {
        if (_terminado) return; // evita dobles disparos
        _terminado = true;
        _corriendo = false;

        _tardo = tiempoNivel - _tiempoRestante;

        int estrellas = CalcularEstrellas(_tardo);
        _ultimoPuntaje = PuntosPorEstrellas(estrellas);

        // Acredita puntos/monedas aquí (cambia AddPuntaje por tu método si usas "monedas")
        if (acreditarPuntosAlGanar)
        {
            // Ejemplo: Stats.I.AddPuntaje(_ultimoPuntaje);
            if (Stats.I != null) Stats.I.AddPuntaje(_ultimoPuntaje);
        }

        MostrarCursor(true);
        Pausar(true);
        if (panelVictoria) panelVictoria.SetActive(true);
        if (puntuacionText) puntuacionText.text = _ultimoPuntaje.ToString();

        PrepararEstrellas();
        StartCoroutine(AnimarEstrellas(estrellas));
    }

    // =========== Derrota ===========
    void PerderPorTiempo()
    {
        if (_terminado) return;
        _terminado = true;
        _corriendo = false;

        MostrarCursor(true);
        Pausar(true);
        if (panelDerrota) panelDerrota.SetActive(true);
    }

    // =========== Botones ===========
    public void Reintentar()
    {
        Pausar(false);
        MostrarCursor(false);

        if (panelVictoria) panelVictoria.SetActive(false);
        if (panelDerrota) panelDerrota.SetActive(false);

        if (respawner != null) respawner.Respawn(); // vuelta al inicio
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

    // =========== Helpers ===========
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

    int PuntosPorEstrellas(int estrellas)
    {
        switch (estrellas)
        {
            case 3: return puntos3Estrellas;
            case 2: return puntos2Estrellas;
            case 1: return puntos1Estrella;
            default: return 0;
        }
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

    // Expuestos por si los quieres leer
    public float TiempoRestante => _tiempoRestante;
    public float TiempoTardado => _tardo;
    public int UltimoPuntaje => _ultimoPuntaje;
}
