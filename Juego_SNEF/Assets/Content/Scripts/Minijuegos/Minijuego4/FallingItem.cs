using UnityEngine;

/// Objeto que cae en el minijuego de “Lluvia de Objetos”.
/// - Puede caer por física (Rigidbody) o por código (sin física).
/// - Al capturarlo: animación pop (bounce) y fade, luego vuelve al pool.
/// - Detecta captura al entrar en un Trigger con tag "Catcher".
public class FallingItem : MonoBehaviour
{
    [Header("Refs")]
    public SpriteRenderer spriteRenderer;   // Asigna si usas sprite
    public Transform visualRoot;            // Resetea rotación al activar (opcional)

    [Header("Despawn")]
    [Tooltip("Cuánto más abajo del mínimo Y del spawn se recicla el ítem.")]
    public float killMarginBelowArea = 3f;

    // Interno
    bool _usarRb;
    float _fallSpeed;
    Rigidbody _rb;
    Collider _col;
    LluviaObjetosManager _mgr;

    bool _esBueno;
    string _key;

    Quaternion _baseLocalRot;
    Vector3 _baseLocalScale;
    float _killY;
    bool _caught;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _col = GetComponent<Collider>();

        if (visualRoot == null && spriteRenderer != null)
            visualRoot = spriteRenderer.transform;

        if (visualRoot != null) _baseLocalRot = visualRoot.localRotation;
        _baseLocalScale = transform.localScale;
    }

    /// Llamado por el manager al construir el pool.
    public void Init(LluviaObjetosManager mgr, bool usarRigidbody, float fallSpeed)
    {
        _mgr = mgr;
        _usarRb = usarRigidbody;
        _fallSpeed = fallSpeed;

        if (_usarRb && _rb) { _rb.useGravity = true; _rb.isKinematic = false; }
        else if (_rb) { _rb.useGravity = false; _rb.isKinematic = true; }
    }

    void OnEnable()
    {
        _caught = false;
        if (_rb)
        {
            _rb.WakeUp();
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }
    }

    /// Reutilización desde el pool.
    public void Setup(bool esBueno, string key, Sprite sprite, Vector3 worldPos)
    {
        _esBueno = esBueno;
        _key = key;

        if (spriteRenderer)
        {
            spriteRenderer.sprite = sprite;
            // asegura alfa 1 para la próxima vida
            var c = spriteRenderer.color; c.a = 1f; spriteRenderer.color = c;
        }
        if (visualRoot) visualRoot.localRotation = _baseLocalRot;

        // tamaño controlado por el manager
        float scale = (_mgr != null) ? Mathf.Max(0.001f, _mgr.itemWorldScale) : 1f;
        transform.localScale = _baseLocalScale * scale;

        transform.position = worldPos;
        transform.rotation = Quaternion.identity;

        if (_usarRb && _rb) { _rb.linearVelocity = Vector3.zero; _rb.angularVelocity = Vector3.zero; }
        if (_col) _col.enabled = true;

        gameObject.SetActive(true);

        // umbral de reciclado
        if (_mgr != null && _mgr.spawnArea != null)
            _killY = _mgr.spawnArea.bounds.min.y - Mathf.Abs(killMarginBelowArea);
        else
            _killY = -10f;
    }

    void Update()
    {
        if (!gameObject.activeSelf || _caught) return;

        // Caída por código si no se usa RB
        if (!_usarRb)
            transform.position += Vector3.down * _fallSpeed * Time.deltaTime;

        // Reciclar si pasó el umbral
        if (transform.position.y < _killY)
            _mgr.OnItemOutOfBounds(this);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!gameObject.activeSelf || _caught) return;

        if (other.CompareTag("Catcher"))
        {
            _caught = true;
            if (_col) _col.enabled = false;
            if (_usarRb && _rb) { _rb.isKinematic = true; _rb.linearVelocity = Vector3.zero; _rb.angularVelocity = Vector3.zero; }

            // Notifica al manager (sumas/penalizaciones)
            _mgr.OnItemCatched(this, _esBueno);

            // VFX de captura y luego volver al pool
            StartCoroutine(CatchVFXThenReturn());
        }
    }

    System.Collections.IEnumerator CatchVFXThenReturn()
    {
        // Bounce pequeñito y luego shrink + fade
        Vector3 s0 = transform.localScale;
        Vector3 s1 = s0 * 1.12f;
        float t = 0f, inTime = 0.09f, outTime = 0.20f;

        // POP (0 → s1)
        while (t < inTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / inTime);
            float ease = 1f - Mathf.Pow(1f - k, 3f);
            transform.localScale = Vector3.Lerp(s0, s1, ease);
            yield return null;
        }

        // SHRINK + FADE (s1 → 0)
        t = 0f;
        Color c0 = spriteRenderer ? spriteRenderer.color : Color.white;
        while (t < outTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / outTime);
            transform.localScale = Vector3.Lerp(s1, Vector3.zero, k);
            if (spriteRenderer)
            {
                var c = spriteRenderer.color;
                c.a = Mathf.Lerp(c0.a, 0f, k);
                spriteRenderer.color = c;
            }
            yield return null;
        }

        // Devolver al pool
        _mgr.DevolverAlPool(this);
    }
}
