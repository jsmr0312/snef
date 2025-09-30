using UnityEngine;
using TMPro;
using System.Collections;

[RequireComponent(typeof(Collider))]
public class OptionItem : MonoBehaviour
{
    [Header("Visual")]
    public SpriteRenderer spriteRenderer;           // hijo: SpriteObjeto
    public TextMeshProUGUI nombreTMP;               // Canvas/NombreObjeto
    public TextMeshProUGUI cantidadTMP;             // Canvas/Cantidad (opcional)
    public PopupPopAndScale popup;                  // popup global
    public Vector3 popupWorldOffset = new Vector3(0, 1.0f, 0);

    [Header("Animación de recogida")]
    public float riseHeight = 0.6f;
    public float riseTime = 0.18f;
    public float dropExtra = 0.8f;
    public float fallGravity = 18f;
    public float spinSpeedOnCollect = 720f;

    [Header("FX")]
    public AudioSource sfx;
    public ParticleSystem particles;

    [Header("Reset al reutilizar (para slots en escena)")]
    [Tooltip("Restaurar posición/rotación/escala originales cada vez que se usa Setup().")]
    public bool resetTransformOnSetup = true;
    [Tooltip("Si tu sprite hijo también queda girado, asigna aquí su Transform para resetearlo.")]
    public Transform spriteRootToReset; // normalmente el hijo "SpriteObjeto"

    // Estado
    public string Key { get; private set; }
    public string DisplayName { get; private set; }
    public bool IsCorrect { get; private set; }

    private CompraResponsivaManager _mgr;
    private Collider _col;
    private RotateAndLevitate _rot;
    private bool _clicked;

    // Pose base (del slot en escena)
    private Vector3 _baseLocalPos;
    private Quaternion _baseLocalRot;
    private Vector3 _baseLocalScale;
    private Quaternion _baseSpriteLocalRot;

    void Awake()
    {
        _col = GetComponent<Collider>();
        _col.isTrigger = true;
        _rot = GetComponent<RotateAndLevitate>();

        // Capturamos la pose original del slot
        _baseLocalPos = transform.localPosition;
        _baseLocalRot = transform.localRotation;
        _baseLocalScale = transform.localScale;

        if (spriteRootToReset == null && spriteRenderer != null)
            spriteRootToReset = spriteRenderer.transform;

        if (spriteRootToReset != null)
            _baseSpriteLocalRot = spriteRootToReset.localRotation;
    }

    // Setup principal (con cantidadLabel)
    public void Setup(CompraResponsivaManager mgr, string key, string display, Sprite sprite, bool correct, string cantidadLabel)
    {
        _mgr = mgr;
        Key = key;
        DisplayName = display;
        IsCorrect = correct;

        // --- Reset de pose base para que no herede el giro de la ronda anterior ---
        if (resetTransformOnSetup)
        {
            // Pausamos momentáneamente el flotado para que no sobrescriba el reset este frame
            bool reactivateRot = false;
            if (_rot && _rot.enabled) { _rot.enabled = false; reactivateRot = true; }

            transform.localPosition = _baseLocalPos;
            transform.localRotation = _baseLocalRot;
            transform.localScale = _baseLocalScale;

            if (spriteRootToReset != null)
                spriteRootToReset.localRotation = _baseSpriteLocalRot;

            if (reactivateRot) _rot.enabled = true;
        }

        if (spriteRenderer) spriteRenderer.sprite = sprite;
        if (nombreTMP) nombreTMP.text = string.IsNullOrEmpty(display) ? key : display;
        if (cantidadTMP) cantidadTMP.text = cantidadLabel;

        _clicked = false;

        if (_col) _col.enabled = true;
        if (_rot) _rot.enabled = true;
        gameObject.SetActive(true);
    }

    // Overload de seguridad (por si alguna llamada antigua no pasa cantidadLabel)
    public void Setup(CompraResponsivaManager mgr, string key, string display, Sprite sprite, bool correct)
    {
        string autoLabel = correct ? $"+{mgr.premioCorrecta}" : $"-{mgr.penalizacionIncorrecta}";
        Setup(mgr, key, display, sprite, correct, autoLabel);
    }

    public void SetInteractable(bool enabled)
    {
        if (_col) _col.enabled = enabled;
    }

    void OnMouseDown()
    {
        if (_clicked || (_col && !_col.enabled)) return;
        _clicked = true;
        _mgr.OnOptionChosen(this);
    }

    public IEnumerator PlayCollectAnimAndHide(string popupPointsText)
    {
        if (sfx) sfx.Play();
        if (particles) particles.Play();

        if (_col) _col.enabled = false;
        if (_rot) _rot.enabled = false;

        if (popup)
        {
            string nombre = string.IsNullOrEmpty(DisplayName) ? Key : DisplayName;
            popup.Play(nombre, popupPointsText, transform.position + popupWorldOffset);
        }

        Vector3 startPos = transform.position;

        // SUBIDA
        float t = 0f;
        while (t < riseTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, riseTime));
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
        // Nota: el reset de rotación/posición lo hacemos en Setup() para la siguiente ronda.
    }
}
