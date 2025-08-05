using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.Events;

[RequireComponent(typeof(RectTransform))]
public class CharacterCardInteractable : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler,
    ISelectHandler, IDeselectHandler
{
    [Header("Escalados")]
    public Vector3 normalScale = Vector3.one;
    public Vector3 hoverScale = Vector3.one * 1.05f;
    public Vector3 pressedScale = Vector3.one * 0.95f;
    public Vector3 selectedScale = Vector3.one * 1.1f; // más grande cuando está seleccionado

    [Header("Duraciones")]
    public float transitionDuration = 0.15f;
    public float pressedTransitionDuration = 0.08f;

    [Header("Easing")]
    public AnimationCurve easeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Borde de confirmación / selección")]
    public Image confirmBorder; // borde amarillo
    public float confirmPulseDuration = 0.25f;
    public float confirmBorderMaxAlpha = 1f;
    public float confirmBorderMinAlpha = 0f;
    public float confirmHoldTime = 0.4f; // cuánto dura el borde después de confirmar

    [Header("Callbacks (opcional)")]
    public UnityEvent onHoverEnter;
    public UnityEvent onHoverExit;
    public UnityEvent onPressed;
    public UnityEvent onReleased;
    public UnityEvent onConfirmed;
    public UnityEvent onSelected;
    public UnityEvent onDeselected;

    // Estado estático para selección única
    private static CharacterCardInteractable currentSelected;

    // Internos
    private Coroutine scaleRoutine;
    private Coroutine confirmRoutine;
    private bool isPointerOver;
    private bool isPressed;
    private bool hasFocus;
    private bool isSelected;

    void Awake()
    {
        transform.localScale = normalScale;
        if (confirmBorder != null)
        {
            SetBorderAlpha(0f);
            confirmBorder.gameObject.SetActive(true);
        }
    }

    #region Pointer / Mouse

    public void OnPointerEnter(PointerEventData eventData)
    {
        isPointerOver = true;
        UpdateScale();
        onHoverEnter?.Invoke();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isPointerOver = false;
        UpdateScale();
        onHoverExit?.Invoke();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPressed = true;
        StartScale(pressedScale, pressedTransitionDuration);
        onPressed?.Invoke();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;
        UpdateScale();
        onReleased?.Invoke();

        if (isPointerOver)
        {
            Confirm();
            Select(); // al hacer click se selecciona
        }
    }

    #endregion

    #region Keyboard / Gamepad Focus

    public void OnSelect(BaseEventData eventData)
    {
        hasFocus = true;
        UpdateScale();
        onHoverEnter?.Invoke();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        hasFocus = false;
        UpdateScale();
        onHoverExit?.Invoke();
    }

    public void SetFocused(bool focused)
    {
        if (focused)
            OnSelect(null);
        else
            OnDeselect(null);
    }

    public void Press()
    {
        OnPointerDown(null);
    }

    public void Release()
    {
        OnPointerUp(null);
    }

    #endregion

    #region Escalado

    private void UpdateScale()
    {
        if (isPressed)
        {
            StartScale(pressedScale, pressedTransitionDuration);
        }
        else if (isSelected)
        {
            StartScale(selectedScale, transitionDuration);
        }
        else if (isPointerOver || hasFocus)
        {
            StartScale(hoverScale, transitionDuration);
        }
        else
        {
            StartScale(normalScale, transitionDuration);
        }
    }

    private void StartScale(Vector3 target, float duration)
    {
        if (scaleRoutine != null)
            StopCoroutine(scaleRoutine);
        scaleRoutine = StartCoroutine(ScaleCoroutine(target, duration));
    }

    private IEnumerator ScaleCoroutine(Vector3 target, float duration)
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
        scaleRoutine = null;
    }

    #endregion

    #region Confirmación y Selección

    public void Confirm()
    {
        onConfirmed?.Invoke();
        if (confirmRoutine != null)
            StopCoroutine(confirmRoutine);
        confirmRoutine = StartCoroutine(ConfirmRoutine());
    }

    private IEnumerator ConfirmRoutine()
    {
        if (confirmBorder == null)
            yield break;

        // Pulso de confirmación (fade in, mantener, no desaparecer si está seleccionado)
        float half = confirmPulseDuration * 0.5f;

        // Fade in
        yield return AnimateBorderAlpha(confirmBorderMinAlpha, confirmBorderMaxAlpha, half);
        // Mantener
        yield return new WaitForSecondsRealtime(confirmHoldTime);

        if (!isSelected)
        {
            // Solo desaparecer si no quedó seleccionado. Si sí, lo deja persistente.
            yield return AnimateBorderAlpha(confirmBorderMaxAlpha, confirmBorderMinAlpha, half);
        }
        confirmRoutine = null;
    }

    private IEnumerator AnimateBorderAlpha(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = easeCurve.Evaluate(t);
            float a = Mathf.LerpUnclamped(from, to, eased);
            SetBorderAlpha(a);
            yield return null;
        }
        SetBorderAlpha(to);
    }

    private void SetBorderAlpha(float a)
    {
        if (confirmBorder == null) return;
        Color c = confirmBorder.color;
        c.a = a;
        confirmBorder.color = c;
    }

    public void Select()
    {
        if (currentSelected == this)
            return;

        // Deselecciona anterior
        if (currentSelected != null)
            currentSelected.Deselect();

        isSelected = true;
        currentSelected = this;

        // Asegura que el borde quede visible persistentemente
        if (confirmBorder != null)
            SetBorderAlpha(confirmBorderMaxAlpha);

        UpdateScale();
        onSelected?.Invoke();
    }

    public void Deselect()
    {
        if (!isSelected)
            return;

        isSelected = false;
        if (currentSelected == this)
            currentSelected = null;

        // Oculta el borde si no está en hover/focus
        if (confirmBorder != null)
        {
            // Si está con puntero encima o focus, tal vez mantener un poco el hover; pero borde de selección se quita
            if (!isPointerOver && !hasFocus)
                SetBorderAlpha(0f);
        }

        UpdateScale();
        onDeselected?.Invoke();
    }

    #endregion
}
