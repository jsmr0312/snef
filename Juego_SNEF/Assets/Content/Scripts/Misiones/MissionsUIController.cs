using UnityEngine;
using System;

public class MissionsUIController : MonoBehaviour
{
    [Header("Ecosistema objetivo")]
    [Tooltip("Si está vacío o autoDetect está activo, se toma del contexto de escena y se normaliza (\"Ecosistema N\").")]
    public string ecosystemName = "Ecosistema 3";

    [Tooltip("Si está activo, intentará tomar el ecosistema de la escena (StandContext/EcosystemBootstrap) y normalizarlo.")]
    public bool autoDetectFromScene = true;

    [Header("Tarjetas (en orden)")]
    public MisionCardUI cardExperience;   // "Visita y completa el punto de experiencia"
    public MisionCardUI cardMinigame;     // "Juega y consigue 3 estrellas..."
    public MisionCardUI cardFourStands;   // "Visita y completa 4 stands"

    // Cache del eco objetivo (siempre normalizado) y estado previo para animaciones
    string _targetEco = "";
    bool prevExp, prevMini, prevFour;

    void Awake()
    {
        // 1) Detecta ecosistema real desde la escena si es posible
        string raw = ecosystemName;

        if (autoDetectFromScene)
        {
            // Prioridad 1: StandContext
            string sceneEco = null;
            var sc = (StandContext.I != null) ? StandContext.I : FindObjectOfType<StandContext>();
            if (sc != null && !string.IsNullOrWhiteSpace(sc.ecosystemName))
                sceneEco = sc.ecosystemName;

            // Prioridad 2: EcosystemBootstrap
            if (string.IsNullOrWhiteSpace(sceneEco))
            {
                var boot = FindObjectOfType<EcosystemBootstrap>();
                if (boot != null && !string.IsNullOrWhiteSpace(boot.ecosystemName))
                    sceneEco = boot.ecosystemName;
            }

            if (!string.IsNullOrWhiteSpace(sceneEco))
                raw = sceneEco;
        }

        if (string.IsNullOrWhiteSpace(raw))
            raw = ecosystemName; // fallback al del Inspector

        _targetEco = MissionManager.NormalizeEcoName(raw);
        ecosystemName = _targetEco; // refleja en el inspector lo que usamos

        // Títulos (opcional)
        if (cardExperience) cardExperience.SetTitle("VISITA Y COMPLETA EL PUNTO DE EXPERIENCIA");
        if (cardMinigame) cardMinigame.SetTitle("JUEGA Y CONSIGUE 3 ESTRELLAS EN UN MINIJUEGO");
        if (cardFourStands) cardFourStands.SetTitle("VISITA Y COMPLETA 4 STANDS");
    }

    void OnEnable()
    {
        if (MissionManager.I != null)
            MissionManager.I.OnEcoStateChanged += OnEcoChanged;

        // Pintado inmediato por si MissionManager ya está listo
        RefreshUI(animateNewCompletions: false);

        // 🔥 Asegura refresco aunque el evento inicial ya haya pasado (race fix)
        CancelInvoke(nameof(DelayedRefresh));
        Invoke(nameof(DelayedRefresh), 0.05f); // 1–3 frames
    }

    void OnDisable()
    {
        if (MissionManager.I != null)
            MissionManager.I.OnEcoStateChanged -= OnEcoChanged;
        CancelInvoke(nameof(DelayedRefresh));
    }

    void OnEcoChanged(string eco, MissionManager.EcoState _)
    {
        // Compara ecos normalizados (no literal)
        if (!string.Equals(MissionManager.NormalizeEcoName(eco),
                           MissionManager.NormalizeEcoName(ecosystemName),
                           StringComparison.OrdinalIgnoreCase))
            return;

        RefreshUI(animateNewCompletions: true);
    }

    void RefreshUI(bool animateNewCompletions)
    {
        if (MissionManager.I == null)
        {
            // Si todavía no existe, vuelve a intentar pronto
            CancelInvoke(nameof(DelayedRefresh));
            Invoke(nameof(DelayedRefresh), 0.05f);
            return;
        }

        var st = MissionManager.I.GetEcoState(_targetEco);

        bool nowExp = st.experienceCompleted;
        bool nowMini = st.anyMinigame3Stars;
        bool nowFour = MissionManager.I.IsComplete_4Stands(_targetEco);

        if (cardExperience) cardExperience.SetState(nowExp, animateNewCompletions && !prevExp && nowExp);
        if (cardMinigame) cardMinigame.SetState(nowMini, animateNewCompletions && !prevMini && nowMini);
        if (cardFourStands) cardFourStands.SetState(nowFour, animateNewCompletions && !prevFour && nowFour);

        prevExp = nowExp;
        prevMini = nowMini;
        prevFour = nowFour;
    }

    void DelayedRefresh() => RefreshUI(false);

#if UNITY_EDITOR
    void OnValidate()
    {
        ecosystemName = MissionManager.NormalizeEcoName(ecosystemName);
    }
#endif
}
