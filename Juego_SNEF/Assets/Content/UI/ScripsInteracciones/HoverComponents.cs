using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

[RequireComponent(typeof(RectTransform))]
public class HoverComponents : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler,
    IPointerClickHandler
{
    [Header("Escalados")]
    public Vector3 normalScale = Vector3.one;
    public Vector3 hoverScale = Vector3.one * 1.05f;
    public Vector3 pressedScale = Vector3.one * 0.95f;

    [Header("Duraciones")]
    public float transitionDuration = 0.15f;
    public float pressedTransitionDuration = 0.08f;

    [Header("Easing")]
    public AnimationCurve easeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Audio")]
    [Tooltip("Si se asigna, todos los sonidos usarán esta fuente global.")]
    public AudioSource sharedAudioSource;  // <- arrastra aquí tu fuente global
    [Tooltip("Si no hay sharedAudioSource, intentará usar un AudioSource local (opcional).")]
    public AudioSource fallbackLocalAudioSource;

    public AudioClip hoverClip;
    public AudioClip downClip;
    public AudioClip upClip;
    public AudioClip clickClip;
    [Range(0f, 1f)] public float volume = 1f;

    private Coroutine currentRoutine;
    private bool isPointerOver;
    private bool isPressed;

    void Awake()
    {
        transform.localScale = normalScale;

        // Si no hay global, intenta hallar uno local (opcional)
        if (sharedAudioSource == null && fallbackLocalAudioSource == null)
            fallbackLocalAudioSource = GetComponent<AudioSource>(); // por si tienes uno en el botón

        // Ajustes recomendados si existe alguna fuente
        var src = sharedAudioSource != null ? sharedAudioSource : fallbackLocalAudioSource;
        if (src != null)
        {
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.enabled = true;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isPointerOver = true;
        if (!isPressed) StartScaleRoutine(hoverScale, transitionDuration);
        PlayOneShotSafe(hoverClip);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isPointerOver = false;
        if (!isPressed) StartScaleRoutine(normalScale, transitionDuration);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPressed = true;
        StartScaleRoutine(pressedScale, pressedTransitionDuration);
        PlayOneShotSafe(downClip);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;
        StartScaleRoutine(isPointerOver ? hoverScale : normalScale, transitionDuration);
        PlayOneShotSafe(upClip);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        PlayOneShotSafe(clickClip);
    }

    private void PlayOneShotSafe(AudioClip clip)
    {
        if (clip == null) return;

        var src = sharedAudioSource != null ? sharedAudioSource : fallbackLocalAudioSource;

        if (src != null && src.enabled && src.gameObject.activeInHierarchy)
        {
            src.PlayOneShot(clip, volume);
        }
        else
        {
            // Fallback ultra seguro (por si el global está deshabilitado):
            var cam = Camera.main;
            Vector3 pos = cam ? cam.transform.position : Vector3.zero;
            AudioSource.PlayClipAtPoint(clip, pos, volume);
        }
    }

    private void StartScaleRoutine(Vector3 target, float duration)
    {
        if (currentRoutine != null) StopCoroutine(currentRoutine);
        currentRoutine = StartCoroutine(ScaleRoutine(target, duration));
    }

    private IEnumerator ScaleRoutine(Vector3 target, float duration)
    {
        Vector3 initial = transform.localScale;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = easeCurve.Evaluate(t);
            transform.localScale = Vector3.LerpUnclamped(initial, target, eased);
            yield return null;
        }
        transform.localScale = target;
        currentRoutine = null;
    }
}
