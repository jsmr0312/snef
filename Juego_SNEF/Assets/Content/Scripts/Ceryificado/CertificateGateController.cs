using UnityEngine;

[DefaultExecutionOrder(-200)] // corre temprano para setear la UI pronto
public class CertificateGateController : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("Contenedor raíz con la imagen + botón 'Obtén tu certificado'")]
    public GameObject container;

    [Header("Criterio de desbloqueo")]
    [Tooltip("Si está activo, basta con el logro 'Experto en finanzas' para desbloquear.")]
    public bool gateByExpertOnly = true;

    [Tooltip("Si está activo, requiere TODOS los logros (Gamer, Coleccionista, Ahorrador y Experto).")]
    public bool gateByAllAchievements = false;

    [Header("Pruebas")]
    [Tooltip("Forzar elegibilidad (ignora criterios).")]
    public bool forceEligibleForTests = false;

    bool _lastEligible;

    void OnEnable()
    {
        if (ProgressCore.I != null) ProgressCore.I.OnChanged += _ => Recompute();
        if (AchievementsManager.I != null) AchievementsManager.I.OnChanged += Recompute;
    }

    void OnDisable()
    {
        if (ProgressCore.I != null) ProgressCore.I.OnChanged -= _ => Recompute();
        if (AchievementsManager.I != null) AchievementsManager.I.OnChanged -= Recompute;
    }

    void Start() => Recompute();

    public void ForceRecompute() => Recompute();

    void Recompute()
    {
        bool eligible = false;

        // 1) Si el backend ya lo marcó, respétalo
        if (ProgressCore.I != null)
            eligible = ProgressCore.I.Data?.certificate?.eligible == true;

        // 2) Si no viene del back, evalúa por logros locales
        if (!eligible && AchievementsManager.I != null)
        {
            if (forceEligibleForTests) eligible = true;
            else if (gateByAllAchievements)
            {
                eligible =
                    AchievementsManager.I.Unlocked_Gamer &&
                    AchievementsManager.I.Unlocked_Collector &&
                    AchievementsManager.I.Unlocked_Saver &&
                    AchievementsManager.I.Unlocked_Expert;
            }
            else if (gateByExpertOnly)
            {
                eligible = AchievementsManager.I.Unlocked_Expert;
            }
        }

        // 3) UI
        if (container != null) container.SetActive(eligible);

        // 4) Persistencia + evento canónico (solo en flanco false->true)
        if (!_lastEligible && eligible)
        {
            ProgressCore.I?.SetCertificateEligible(true);
            MetricsClient.I?.TrackCertificadoDesbloqueado(true);
        }
        _lastEligible = eligible;
    }
}
