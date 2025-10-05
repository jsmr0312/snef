using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MisionCardUI : MonoBehaviour
{
    [Header("Refs")]
    public Image background;                 // el rectángulo blanco
    public TextMeshProUGUI titleText;        // "NombreMisionText"
    public Image checkImage;                 // "checkMisionImage"

    [Header("Estilo")]
    public Color incompleteColor = Color.white;
    public Color completeColor = new Color(0.64f, 0.90f, 0.62f); // verde suave
    public float colorLerpTime = 0.2f;
    public float popDuration = 0.25f;        // pop de la palomita

    bool _isComplete;

    void Awake()
    {
        if (background) background.color = incompleteColor;
        if (checkImage)
        {
            checkImage.gameObject.SetActive(false);
            checkImage.rectTransform.localScale = Vector3.one;
        }
    }

    public void SetTitle(string text)
    {
        if (titleText) titleText.text = text;
    }

    /// <summary>
    /// Actualiza el estado visual. Si 'animate' es true y se completa por primera vez,
    /// muestra la palomita con pop y hace fade a verde.
    /// </summary>
    public void SetState(bool completed, bool animate)
    {
        if (completed == _isComplete)
        {
            // solo asegura el color e icono
            ApplyVisual(completed, false);
            return;
        }

        _isComplete = completed;
        ApplyVisual(completed, animate);
    }

    void ApplyVisual(bool completed, bool animate)
    {
        StopAllCoroutines();

        if (!completed)
        {
            if (checkImage) checkImage.gameObject.SetActive(false);
            if (background) background.color = incompleteColor;
            return;
        }

        // Completed = true
        if (animate)
        {
            StartCoroutine(AnimateComplete());
        }
        else
        {
            if (checkImage) checkImage.gameObject.SetActive(true);
            if (background) background.color = completeColor;
        }
    }

    IEnumerator AnimateComplete()
    {
        if (checkImage)
        {
            checkImage.gameObject.SetActive(true);
            var rt = checkImage.rectTransform;
            rt.localScale = Vector3.one * 0.3f;

            float t = 0f;
            while (t < popDuration)
            {
                t += Time.unscaledDeltaTime;
                float k = t / popDuration;
                // easeOutBack simple:
                float s = 1.70158f;
                float k2 = (k - 1f);
                float ease = 1f + (k2 * k2 * ((s + 1f) * k2 + s));
                rt.localScale = Vector3.one * Mathf.Lerp(0.3f, 1f, Mathf.Clamp01(ease));
                yield return null;
            }
            rt.localScale = Vector3.one;
        }

        if (background)
        {
            Color from = background.color;
            Color to = completeColor;
            float t = 0f;
            while (t < colorLerpTime)
            {
                t += Time.unscaledDeltaTime;
                background.color = Color.Lerp(from, to, t / colorLerpTime);
                yield return null;
            }
            background.color = to;
        }
    }
}
