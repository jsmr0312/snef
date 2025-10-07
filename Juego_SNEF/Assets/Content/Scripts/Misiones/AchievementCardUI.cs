using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class AchievementCardUI : MonoBehaviour
{
    [Header("Refs")]
    public Image icon;                 // imagen grande de la tarjeta

    [Header("Sprites")]
    public Sprite lockedSprite;
    public Sprite unlockedSprite;

    [Header("Animación")]
    public float popDuration = 0.25f;

    bool _isUnlocked;

    void Awake()
    {
        Apply(false, false);
    }

    public void SetUnlocked(bool value, bool animate)
    {
        if (value == _isUnlocked)
        {
            Apply(value, false);
            return;
        }
        _isUnlocked = value;
        Apply(value, animate);
    }

    void Apply(bool unlocked, bool animate)
    {
        StopAllCoroutines();

        if (icon)
            icon.sprite = unlocked ? unlockedSprite : lockedSprite;

        if (unlocked && animate) StartCoroutine(DoPop());
        else if (icon) icon.rectTransform.localScale = Vector3.one;
    }

    IEnumerator DoPop()
    {
        if (icon == null) yield break;

        var rt = icon.rectTransform;
        rt.localScale = Vector3.one * 0.3f;

        float t = 0f;
        while (t < popDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / popDuration);
            // easeOutBack
            float s = 1.70158f;
            float k2 = (k - 1f);
            float ease = 1f + (k2 * k2 * ((s + 1f) * k2 + s));
            rt.localScale = Vector3.one * Mathf.Lerp(0.3f, 1f, ease);
            yield return null;
        }
        rt.localScale = Vector3.one;
    }
}
