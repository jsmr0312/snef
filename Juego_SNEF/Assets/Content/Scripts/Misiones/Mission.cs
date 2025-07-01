// Assets/Scripts/Missions/Mission.cs
using UnityEngine;

public class Mission
{
    public MissionData data;            // tu ScriptableObject con los datos
    public int currentAmount;           // cuántas veces se ha cumplido
    public bool Completed => currentAmount >= data.targetAmount;

    public Mission(MissionData d)
    {
        data = d;
        currentAmount = 0;
    }

    public void Increment()
    {
        if (Completed) return;
        currentAmount++;
    }
}
