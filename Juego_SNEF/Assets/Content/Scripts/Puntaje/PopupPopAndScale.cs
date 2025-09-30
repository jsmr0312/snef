using UnityEngine;
using System.Collections;
using TMPro;

public class PopupPopAndScale : MonoBehaviour
{
    [Header("Referencias")]
    public TMP_Text nombreTMP;
    public TMP_Text puntosTMP;
    public Transform worldParent; // opcional

    [Header("Animación (Pop)")]
    public Vector3 worldOffset = new Vector3(0f, 1f, 0f);
    public float popInTime = 0.28f;
    [Tooltip(">1 = rebote; 1 = sin rebote")]
    public float overshootScale = 1.12f;
    public float holdTime = 2.0f;
    public float popOutTime = 0.24f;
    public float riseDistance = 0.25f;

    [Header("Opcional")]
    [Tooltip("Usa tiempo real (unscaled) para que el popup anime aunque Time.timeScale=0.")]
    public bool useUnscaledTime = false;
    [Tooltip("Hace que el popup siempre mire hacia la cámara.")]
    public bool billboardToCamera = true;
    public Camera targetCamera; // si está vacío, usa Camera.main

    // Estado inicial
    private Transform _origParent;
    private Vector3 _origLocalPos;
    private Quaternion _origLocalRot;
    private Vector3 _origLocalScale;   // escala base real (0.01 en tu caso)

    void Awake()
    {
        _origParent = transform.parent;
        _origLocalPos = transform.localPosition;
        _origLocalRot = transform.localRotation;
        _origLocalScale = transform.localScale;

        if (nombreTMP) nombreTMP.gameObject.SetActive(false);
        if (puntosTMP) puntosTMP.gameObject.SetActive(false);

        // Mantén el tamaño original, solo lo multiplicaremos con factor 0→1
        transform.localScale = Vector3.zero; // será _origLocalScale * 0

        if (billboardToCamera && targetCamera == null)
            targetCamera = Camera.main;
    }

    public void Play(string nombre, string puntos, Vector3 itemWorldPos)
    {
        if (nombreTMP) { nombreTMP.text = nombre; nombreTMP.gameObject.SetActive(true); }
        if (puntosTMP) { puntosTMP.text = puntos; puntosTMP.gameObject.SetActive(true); }

        transform.SetParent(worldParent, true);
        Vector3 startWorld = itemWorldPos + worldOffset;
        transform.position = startWorld;
        // empezar en escala 0 relativa
        SetRelativeScale(0f);

        StopAllCoroutines();
        StartCoroutine(RunPop(startWorld));
    }

    private IEnumerator RunPop(Vector3 startWorld)
    {
        Vector3 endWorld = startWorld + Vector3.up * riseDistance;

        float t = 0f;
        while (t < popInTime)
        {
            t += DT();
            float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, popInTime));

            // billboard opcional
            if (billboardToCamera && targetCamera)
                FaceCamera();

            // slide up
            float easePos = EaseOutCubic(k);
            transform.position = Vector3.Lerp(startWorld, endWorld, easePos);

            // escala relativa con "back" (rebote)
            float s = EaseOutBack(k);
            float target = Mathf.LerpUnclamped(0f, Mathf.Max(1f, overshootScale), s);
            // aterriza exactamente en 1 al final
            float rel = Mathf.Lerp(target, 1f, k);

            SetRelativeScale(rel);
            yield return null;
        }
        transform.position = endWorld;
        SetRelativeScale(1f);

        if (useUnscaledTime) yield return new WaitForSecondsRealtime(holdTime);
        else yield return new WaitForSeconds(holdTime);

        // salida: 1 → 0 con back-in
        t = 0f;
        while (t < popOutTime)
        {
            t += DT();
            float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, popOutTime));

            if (billboardToCamera && targetCamera)
                FaceCamera();

            float rel = Mathf.LerpUnclamped(1f, 0f, EaseInBack(k));
            SetRelativeScale(Mathf.Max(0f, rel));
            yield return null;
        }
        SetRelativeScale(0f);

        if (nombreTMP) nombreTMP.gameObject.SetActive(false);
        if (puntosTMP) puntosTMP.gameObject.SetActive(false);

        // restaurar para reuso
        transform.SetParent(_origParent, true);
        transform.localPosition = _origLocalPos;
        transform.localRotation = _origLocalRot;
        transform.localScale = _origLocalScale;
    }

    // Escala relativa: factor 0..1..overshoot sobre la escala base original
    private void SetRelativeScale(float factor)
    {
        transform.localScale = _origLocalScale * factor;
    }

    // --- reemplazo del método abreviado por una versión clásica ---
    private float DT()
    {
        return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    }

    private void FaceCamera()
    {
        Vector3 dir = transform.position - targetCamera.transform.position;
        if (dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(dir);
    }

    private float EaseOutBack(float x, float c = 1.70158f)
    {
        float t = x - 1f;
        return t * t * ((c + 1f) * t + c) + 1f;
    }
    private float EaseInBack(float x, float c = 1.70158f)
    {
        return x * x * ((c + 1f) * x - c);
    }
    private float EaseOutCubic(float x)
    {
        return 1f - Mathf.Pow(1f - x, 3f);
    }
}
