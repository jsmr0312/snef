using UnityEngine;
using TMPro;

public class AchievementsUIController : MonoBehaviour
{
    [Header("Tarjetas")]
    public AchievementCardUI cardGamer;
    public AchievementCardUI cardSaver;
    public AchievementCardUI cardCollector;
    public AchievementCardUI cardExpert;

    [Header("Progreso Gamer (opcional)")]
    public TextMeshProUGUI gamerProgressText; // "x/4"

    void OnEnable()
    {
        if (AchievementsManager.I != null)
            AchievementsManager.I.OnChanged += OnAchievementsChanged;

        RefreshUI(false);
    }

    void OnDisable()
    {
        if (AchievementsManager.I != null)
            AchievementsManager.I.OnChanged -= OnAchievementsChanged;
    }

    void OnAchievementsChanged() => RefreshUI(true);

    void RefreshUI(bool animateNew)
    {
        var A = AchievementsManager.I;
        if (A == null) return;

        if (cardGamer) cardGamer.SetUnlocked(A.Unlocked_Gamer, animateNew);
        if (cardSaver) cardSaver.SetUnlocked(A.Unlocked_Saver, animateNew);
        if (cardCollector) cardCollector.SetUnlocked(A.Unlocked_Collector, animateNew);
        if (cardExpert) cardExpert.SetUnlocked(A.Unlocked_Expert, animateNew);

        if (gamerProgressText)
            gamerProgressText.text = $"{A.Gamer_TypesCompleted}/{Mathf.Max(1, A.gamerRequiredTypes)}";
    }
}
