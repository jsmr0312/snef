// Assets/Scripts/Missions/MissionsUIController.cs
using UnityEngine;

public class MissionsUIController : MonoBehaviour
{
    [Tooltip("Prefab de MissionEntry (con componente MissionUIEntry)")]
    public MissionUIEntry entryPrefab;

    void Start()
    {
        // === Entry para VisitStand ===
        var standEntry = Instantiate(entryPrefab, transform);
        standEntry.missionType = MissionManager.MissionType.VisitStand;
        InitEntry(standEntry);

        // === Entry para CompleteQuiz ===
        var quizEntry = Instantiate(entryPrefab, transform);
        quizEntry.missionType = MissionManager.MissionType.CompleteQuiz;
        InitEntry(quizEntry);
    }

    private void InitEntry(MissionUIEntry entry)
    {
        // Estado inicial
        var prog = MissionManager.I.GetProgress(entry.missionType);
        entry.Refresh(prog, prog.actual >= prog.objetivo);

        // Suscribir eventos
        MissionManager.I.OnMissionProgress += (type, a, o) =>
        {
            if (type != entry.missionType) return;
            entry.Refresh((a, o), a >= o);
        };
        MissionManager.I.OnMissionCompleted += (type) =>
        {
            if (type != entry.missionType) return;
            var p = MissionManager.I.GetProgress(type);
            entry.Refresh(p, true);
        };
    }
}
