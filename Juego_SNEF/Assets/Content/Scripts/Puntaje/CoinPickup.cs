using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

[RequireComponent(typeof(Collider))]
public class CoinPickup : MonoBehaviour, ILevelResettable
{
    [Header("Recompensa")]
    public int presupuestoGanado = 10;
    public string nombreObjeto = "FONDO DE EMERGENCIA";
    public string formatoPuntos = "+{0}";

    [Header("Animación de recogida (objeto)")]
    public float riseHeight = 0.6f;
    public float riseTime = 0.18f;
    public float dropExtra = 0.8f;
    public float fallGravity = 18f;
    public float spinSpeedOnCollect = 720f;

    [Header("FX Opcionales")]
    public AudioSource sfx;
    public ParticleSystem particles;

    [Header("Popup (Canvas hijo)")]
    public bool showPopup = true;
    public PopupPopAndScale popup;                 // script del popup flotante
    public Vector3 popupWorldOffset = new Vector3(0f, 1.0f, 0f);

    [Header("Eventos (Opcional)")]
    public UnityEvent onCollected;

    // ================== OBJETO ESPECIAL (OPCIONAL) ==================
    [Header("Objeto especial (opcional)")]
    [Tooltip("Actívalo si este pickup debe elegir un ítem visual aleatorio al aparecer/resetear.")]
    public bool usarObjetoEspecial = false;

    [Tooltip("SpriteRenderer del hijo 'SpriteObjeto' (si usas sprite 2D en mundo).")]
    public SpriteRenderer spriteObjetoRenderer;

    [Tooltip("O bien, Image del hijo 'SpriteObjeto' (si es UI en World Space).")]
    public Image spriteObjetoImage;

    [Tooltip("TMP del hijo 'Canvas/NombreObjeto' para mostrar el nombre en el mundo.")]
    public TextMeshProUGUI nombreObjetoText;

    [Tooltip("TMP del hijo 'Canvas/Cantidad' (solo para mostrar '+N' fijo en el mundo).")]
    public TextMeshProUGUI cantidadText;

    [System.Serializable]
    public class ItemVisual
    {
        public string nombre;
        public Sprite sprite;
    }

    [Tooltip("Lista de posibles objetos. Se elige uno al azar (o por índice fijo).")]
    public List<ItemVisual> posiblesObjetos = new List<ItemVisual>();

    [Tooltip("Elegir al azar cada vez que se resetea. Si está en false, solo se elige una vez al inicio.")]
    public bool elegirAlReset = true;

    [Tooltip("Si >=0 y válido, fuerza seleccionar ese índice en lugar de al azar.")]
    public int indiceFijo = -1;

    // ================================================================

    private Collider _col;
    private RotateAndLevitate _rot;
    private bool _collected;

    private Vector3 _startPos;
    private Quaternion _startRot;
    private Vector3 _startScale;

    private int _chosenIndex = -1;

    void Awake()
    {
        _col = GetComponent<Collider>();
        _col.isTrigger = true;
        _rot = GetComponent<RotateAndLevitate>();

        _startPos = transform.position;
        _startRot = transform.rotation;
        _startScale = transform.localScale;

        // Inicializa visual del objeto especial (si aplica)
        if (usarObjetoEspecial)
        {
            ElegirYAplicarVisual(inicial: true);
        }

        // Actualiza el texto de cantidad del hijo Canvas (si lo usas)
        if (cantidadText)
            cantidadText.text = string.Format(formatoPuntos, presupuestoGanado);
    }

    void OnTriggerEnter(Collider other)
    {
        if (_collected) return;
        if (!other.CompareTag("Player")) return;
        _collected = true;

        if (Stats.I) Stats.I.AddPresupuesto(presupuestoGanado);
        onCollected?.Invoke();

        if (sfx) sfx.Play();
        if (particles) particles.Play();

        _col.enabled = false;
        if (_rot) _rot.enabled = false;

        // Popup independiente
        if (showPopup && popup != null)
        {
            string puntos = string.Format(formatoPuntos, presupuestoGanado);
            // Usa el nombreObjeto ACTUAL (puede venir del ítem aleatorio)
            popup.Play(nombreObjeto, puntos, transform.position + popupWorldOffset);
        }

        StartCoroutine(CollectAnimAndHide());
    }

    IEnumerator CollectAnimAndHide()
    {
        Vector3 startPos = transform.position;

        // SUBIDA
        float t = 0f;
        while (t < riseTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / riseTime);
            float e = 1f - Mathf.Pow(1f - k, 3f);
            Vector3 p = startPos;
            p.y = startPos.y + riseHeight * e;
            transform.position = p;
            transform.Rotate(Vector3.up, spinSpeedOnCollect * Time.deltaTime, Space.World);
            yield return null;
        }

        // CAÍDA
        float v = 0f;
        float targetY = startPos.y - dropExtra;
        Vector3 pos = transform.position;

        while (pos.y > targetY)
        {
            v += fallGravity * Time.deltaTime;
            pos.y -= v * Time.deltaTime;
            transform.position = pos;
            transform.Rotate(Vector3.up, spinSpeedOnCollect * Time.deltaTime, Space.World);
            yield return null;
        }

        gameObject.SetActive(false);
    }

    // === ILevelResettable ===
    public void ResetState()
    {
        StopAllCoroutines();
        _collected = false;

        transform.position = _startPos;
        transform.rotation = _startRot;
        transform.localScale = _startScale;

        if (sfx) { sfx.Stop(); sfx.time = 0f; }
        if (particles) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        if (_rot) _rot.enabled = true;
        _col.enabled = true;
        gameObject.SetActive(true);

        // Elegir nuevo visual si así se configuró
        if (usarObjetoEspecial && elegirAlReset)
        {
            ElegirYAplicarVisual(inicial: false);
        }

        // Refrescar textos visibles en el mundo
        if (cantidadText)
            cantidadText.text = string.Format(formatoPuntos, presupuestoGanado);
        if (nombreObjetoText)
            nombreObjetoText.text = nombreObjeto;
    }

    // ------------------- Objeto especial: elegir/aplicar -------------------
    void ElegirYAplicarVisual(bool inicial)
    {
        if (posiblesObjetos == null || posiblesObjetos.Count == 0)
        {
            // Sin lista: usa lo que ya esté en 'nombreObjeto' y sprite actual
            if (nombreObjetoText) nombreObjetoText.text = nombreObjeto;
            return;
        }

        // Determina índice
        if (indiceFijo >= 0 && indiceFijo < posiblesObjetos.Count)
        {
            _chosenIndex = indiceFijo;
        }
        else if (inicial || elegirAlReset)
        {
            _chosenIndex = Random.Range(0, posiblesObjetos.Count);
        }
        else if (_chosenIndex < 0 || _chosenIndex >= posiblesObjetos.Count)
        {
            // fallback
            _chosenIndex = 0;
        }

        var item = posiblesObjetos[_chosenIndex];

        // Actualiza nombre lógico (afecta popup)
        nombreObjeto = string.IsNullOrEmpty(item.nombre) ? nombreObjeto : item.nombre;

        // Actualiza nombre visible en el mundo
        if (nombreObjetoText)
            nombreObjetoText.text = nombreObjeto;

        // Actualiza sprite (SpriteRenderer o UI Image)
        if (spriteObjetoRenderer)
            spriteObjetoRenderer.sprite = item.sprite;
        if (spriteObjetoImage)
            spriteObjetoImage.sprite = item.sprite;
    }
}
