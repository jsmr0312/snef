// Assets/Scripts/Missions/MissionManager.cs
using System;
using UnityEngine;

public class MissionManager : MonoBehaviour
{
    public static MissionManager I { get; private set; }

    public enum MissionType
    {
        VisitStand,
        CompleteQuiz,    // <-- nueva misión
        // (en el futuro: EnterArcade, etc.)
    }

    [Header("VisitStand")]
    public int standsToVisit = 3;
    private int _standsVisited;
    private bool _visitStandDone;

    [Header("CompleteQuiz")]
    [Tooltip("Cuántas veces hay que abrir el quiz (normalmente 1)")]
    public int quizzesToComplete = 1;
    private int _quizzesDone;
    private bool _completeQuizDone;

    // Eventos para la UI
    public event Action<MissionType, int, int> OnMissionProgress;
    public event Action<MissionType> OnMissionCompleted;

    void Awake()
    {
        if (I == null)
        {
            I = this;
            DontDestroyOnLoad(gameObject);
        }
        else Destroy(gameObject);
    }

    public void NotifyEvent(MissionType type)
    {
        switch (type)
        {
            case MissionType.VisitStand:
                if (_visitStandDone) return;
                _standsVisited++;
                Debug.Log($"[Misión] VisitStand: {_standsVisited}/{standsToVisit}");
                OnMissionProgress?.Invoke(type, _standsVisited, standsToVisit);
                if (_standsVisited >= standsToVisit)
                {
                    _visitStandDone = true;
                    Debug.Log("[Misión] ¡VisitStand completada! 🎉");
                    OnMissionCompleted?.Invoke(type);
                }
                break;

            case MissionType.CompleteQuiz:
                if (_completeQuizDone) return;
                _quizzesDone++;
                Debug.Log($"[Misión] CompleteQuiz: {_quizzesDone}/{quizzesToComplete}");
                OnMissionProgress?.Invoke(type, _quizzesDone, quizzesToComplete);
                if (_quizzesDone >= quizzesToComplete)
                {
                    _completeQuizDone = true;
                    Debug.Log("[Misión] ¡CompleteQuiz completada! 🎉");
                    OnMissionCompleted?.Invoke(type);
                }
                break;
        }
    }

    public (int actual, int objetivo) GetProgress(MissionType type)
    {
        return type switch
        {
            MissionType.VisitStand => (_standsVisited, standsToVisit),
            MissionType.CompleteQuiz => (_quizzesDone, quizzesToComplete),
            _ => (0, 0)
        };
    }
}
