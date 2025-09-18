using UnityEngine;
using System.Collections;
using TMPro;

public class PopupFloatAndFade : MonoBehaviour
{
    [Header("Referencias")]
    public TMP_Text nombreTMP;
    public TMP_Text puntosTMP;
    [Tooltip("CanvasGroup en el mismo Canvas para manejar el alpha")]
    public CanvasGroup group;

    [Header("Animación")]
    public float fadeInTime = 0.25f;
    public float holdTime = 2.0f;
    public float fadeOutTime = 0.35f;
    public float riseDistance = 0.6f;

    [Tooltip("Padre temporal opcional para animar en mundo (si lo dejas null, se usa raíz de la escena).")]
    public Transform worldParent;

    // Estado inicial para restaurar
    private Transform _origParent;
    private Vector3 _origLocalPos;
    private Quaternion _origLocalRot;
    private Vector3 _origLocalScale;

    void Awake()
    {
        _origParent = transform.parent;
        _origLocalPos = transform.localPosition;
        _origLocalRot = transform.localRotation;
        _origLocalScale = transform.localScale;

        if (!group) group = GetComponent<CanvasGroup>();
        if (group) group.alpha = 0f;

        if (nombreTMP) nombreTMP.gameObject.SetActive(false);
        if (puntosTMP) puntosTMP.gameObject.SetActive(false);
    }

    public void Play(string nombre, string puntos, Vector3 startWorldPos)
    {
        if (nombreTMP) { nombreTMP.text = nombre; nombreTMP.gameObject.SetActive(true); }
        if (puntosTMP) { puntosTMP.text = puntos; puntosTMP.gameObject.SetActive(true); }

        // Reparent para animar en coordenadas de mundo sin depender del ítem
        transform.SetParent(worldParent, true); // si worldParent==null -> raíz
        transform.position = startWorldPos;
        StopAllCoroutines();
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        if (group) group.alpha = 0f;

        Vector3 start = transform.position;
        Vector3 end = start + Vector3.up * riseDistance;

        // Fade-in + slide
        float t = 0f;
        while (t < fadeInTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, fadeInTime));
            float ease = 1f - Mathf.Pow(1f - k, 3f);
            transform.position = Vector3.Lerp(start, end, ease);
            if (group) group.alpha = k;
            yield return null;
        }
        transform.position = end;
        if (group) group.alpha = 1f;

        yield return new WaitForSeconds(holdTime);

        // Fade-out
        t = 0f;
        while (t < fadeOutTime)
        {
            t += Time.deltaTime;
            float a = 1f - Mathf.Clamp01(t / Mathf.Max(0.0001f, fadeOutTime));
            if (group) group.alpha = a;
            yield return null;
        }
        if (group) group.alpha = 0f;

        // Oculta textos y vuelve a su padre original (queda listo para reuso)
        if (nombreTMP) nombreTMP.gameObject.SetActive(false);
        if (puntosTMP) puntosTMP.gameObject.SetActive(false);

        transform.SetParent(_origParent, true);
        transform.localPosition = _origLocalPos;
        transform.localRotation = _origLocalRot;
        transform.localScale = _origLocalScale;
    }
}
