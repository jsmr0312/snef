using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

[RequireComponent(typeof(RectTransform))]
public class HoverComponents : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Escalados")]
    public Vector3 normalScale = Vector3.one;
    public Vector3 hoverScale = Vector3.one * 1.05f;
    public Vector3 pressedScale = Vector3.one * 0.95f;

    [Header("Duraciones")]
    public float transitionDuration = 0.15f; // para hover / release
    public float pressedTransitionDuration = 0.08f; // más rápido al presionar

    [Header("Easing")]
    public AnimationCurve easeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    // Internos
    private Coroutine currentRoutine;
    private bool isPointerOver;
    private bool isPressed;

    void Awake()
    {
        transform.localScale = normalScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isPointerOver = true;
        if (isPressed) return; // si está presionado, no sobrescribir
        StartScaleRoutine(hoverScale, transitionDuration);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isPointerOver = false;
        if (isPressed) return;
        StartScaleRoutine(normalScale, transitionDuration);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPressed = true;
        StartScaleRoutine(pressedScale, pressedTransitionDuration);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;
        // Si aún está encima, volver a hover; si salió, normal.
        Vector3 target = isPointerOver ? hoverScale : normalScale;
        StartScaleRoutine(target, transitionDuration);
    }

    private void StartScaleRoutine(Vector3 target, float duration)
    {
        if (currentRoutine != null)
            StopCoroutine(currentRoutine);
        currentRoutine = StartCoroutine(ScaleRoutine(target, duration));
    }

    private IEnumerator ScaleRoutine(Vector3 target, float duration)
    {
        Vector3 initial = transform.localScale;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime; // usa unscaled si quieres que no frene con timeScale
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = easeCurve.Evaluate(t);
            transform.localScale = Vector3.LerpUnclamped(initial, target, eased);
            yield return null;
        }
        transform.localScale = target;
        currentRoutine = null;
    }
}
