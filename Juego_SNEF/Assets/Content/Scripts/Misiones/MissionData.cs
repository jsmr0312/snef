using UnityEngine;

[CreateAssetMenu(menuName = "Missions/Mission Data")]
public class MissionData : ScriptableObject
{
    public string id;
    public string description;
    public MissionType type;
    public int targetAmount;
}

public enum MissionType
{
    VisitStand,
    CompleteQuiz,
    EnterMinigame,
    // …
}
