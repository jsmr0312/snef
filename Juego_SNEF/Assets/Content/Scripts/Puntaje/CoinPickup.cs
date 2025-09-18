using UnityEngine;
using UnityEngine.Events;
using System.Collections;
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
    public PopupPopAndScale popup;          // <— referencia al script del Canvas
    public Vector3 popupWorldOffset = new Vector3(0f, 1.0f, 0f);

    [Header("Eventos (Opcional)")]
    public UnityEvent onCollected;

    private Collider _col;
    private RotateAndLevitate _rot;
    private bool _collected;

    private Vector3 _startPos;
    private Quaternion _startRot;
    private Vector3 _startScale;

    void Awake()
    {
        _col = GetComponent<Collider>();
        _col.isTrigger = true;
        _rot = GetComponent<RotateAndLevitate>();

        _startPos = transform.position;
        _startRot = transform.rotation;
        _startScale = transform.localScale;
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

        // Lanza popup independiente (NO depende de este GameObject)
        if (showPopup && popup != null)
        {
            string puntos = string.Format(formatoPuntos, presupuestoGanado);
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

        // El ítem se apaga sin matar el popup (corre en su propio componente)
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
    }
}
