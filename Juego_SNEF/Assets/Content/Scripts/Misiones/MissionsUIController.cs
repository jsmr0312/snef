using UnityEngine;

public class MissionsUIController : MonoBehaviour
{
    [Header("Ecosistema objetivo (debe coincidir exactamente)")]
    public string ecosystemName = "Ecosistema 3";

    [Header("Tarjetas (en orden)")]
    public MisionCardUI cardExperience;   // "Visita y completa el punto de experiencia"
    public MisionCardUI cardMinigame;     // "Juega y consigue 3 estrellas..."
    public MisionCardUI cardFourStands;   // "Visita y completa 4 stands"

    // Estado previo para decidir si animamos
    bool prevExp, prevMini, prevFour;

    void Awake()
    {
        // Textos (por si aún no están en el prefab)
        if (cardExperience) cardExperience.SetTitle("VISITA Y COMPLETA EL PUNTO DE EXPERIENCIA");
        if (cardMinigame) cardMinigame.SetTitle("JUEGA Y CONSIGUE 3 ESTRELLAS EN UN MINIJUEGO");
        if (cardFourStands) cardFourStands.SetTitle("VISITA Y COMPLETA 4 STANDS");
    }

    void OnEnable()
    {
        if (MissionManager.I != null)
            MissionManager.I.OnEcoStateChanged += OnEcoChanged;

        // Pintar estado inicial (sin animación)
        RefreshUI(animateNewCompletions: false);
    }

    void OnDisable()
    {
        if (MissionManager.I != null)
            MissionManager.I.OnEcoStateChanged -= OnEcoChanged;
    }

    void OnEcoChanged(string eco, MissionManager.EcoState _)
    {
        if (!string.Equals(eco, ecosystemName, System.StringComparison.OrdinalIgnoreCase))
            return;

        // Cuando nos notifican cambios de este ecosistema, refrescamos con animación
        RefreshUI(animateNewCompletions: true);
    }

    void RefreshUI(bool animateNewCompletions)
    {
        if (MissionManager.I == null) return;

        var st = MissionManager.I.GetEcoState(ecosystemName);

        bool nowExp = st.experienceCompleted;
        bool nowMini = st.anyMinigame3Stars;
        bool nowFour = MissionManager.I.IsComplete_4Stands(ecosystemName);

        if (cardExperience) cardExperience.SetState(nowExp, animateNewCompletions && !prevExp && nowExp);
        if (cardMinigame) cardMinigame.SetState(nowMini, animateNewCompletions && !prevMini && nowMini);
        if (cardFourStands) cardFourStands.SetState(nowFour, animateNewCompletions && !prevFour && nowFour);

        prevExp = nowExp;
        prevMini = nowMini;
        prevFour = nowFour;
    }
}
