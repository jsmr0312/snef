using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// Popup de presupuesto (+/-): aparece, sube y se desvanece.
/// - El TEXTO permanece siempre en blanco.
/// - El RECTÁNGULO (Image) cambia a positivo/negativo.
/// - Se oculta (alpha 0) al terminar la animación.
public class BudgetPopupUI : MonoBehaviour
{
    [Header("Refs")]
    public RectTransform root;     // normalmente el RectTransform de este mismo GO
    public TMP_Text text;          // tu PresupuestoExtraText
    public Image background;       // el rectángulo verde (o el que uses)
    CanvasGroup _cg;               // para el fade general

    [Header("Animación")]
    public float rise = 40f;         // píxeles hacia arriba
    public float popInTime = 0.10f;
    public float holdTime = 0.35f;
    public float fadeOutTime = 0.28f;

    [Header("Colores fondo")]
    public Color positiveBg = new Color(0.15f, 0.85f, 0.2f, 1f);
    public Color negativeBg = new Color(0.95f, 0.2f, 0.2f, 1f);

    [Header("Texto")]
    public Color textColor = Color.white; // SIEMPRE blanco (o el que quieras)

    Coroutine _running;

    void Awake()
    {
        if (!root) root = GetComponent<RectTransform>();
        _cg = GetComponent<CanvasGroup>();
        if (!_cg) _cg = gameObject.AddComponent<CanvasGroup>();
        // Oculto al inicio
        _cg.alpha = 0f;
    }

    public void Show(int delta)
    {
        if (_running != null) StopCoroutine(_running);
        _running = StartCoroutine(Play(delta));
    }

    IEnumerator Play(int delta)
    {
        if (!root || !text) yield break;

        // Contenido
        text.text = delta >= 0 ? $"+{delta}" : $"{delta}";
        text.color = textColor;
        if (background)
            background.color = delta >= 0 ? positiveBg : negativeBg;

        // Estados iniciales
        Vector2 start = root.anchoredPosition;
        Vector2 mid = start + Vector2.up * (rise * 0.4f);
        Vector2 end = start + Vector2.up * rise;

        root.localScale = new Vector3(0.8f, 0.8f, 1f);
        root.anchoredPosition = start;
        _cg.alpha = 1f;

        // Pop-in
        float t = 0f;
        while (t < popInTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / popInTime);
            float ease = 1f - Mathf.Pow(1f - k, 3f); // easeOutCubic
            root.localScale = Vector3.Lerp(new Vector3(0.8f, 0.8f, 1), Vector3.one, ease);
            root.anchoredPosition = Vector2.Lerp(start, mid, ease);
            yield return null;
        }
        root.localScale = Vector3.one;
        root.anchoredPosition = mid;

        // Hold
        yield return new WaitForSeconds(holdTime);

        // Rise + fade out
        t = 0f;
        while (t < fadeOutTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / fadeOutTime);
            _cg.alpha = 1f - k;
            root.anchoredPosition = Vector2.Lerp(mid, end, k);
            yield return null;
        }

        // Ocultar y reset
        _cg.alpha = 0f;
        root.anchoredPosition = start;
        root.localScale = Vector3.one;
        _running = null;
    }
}
