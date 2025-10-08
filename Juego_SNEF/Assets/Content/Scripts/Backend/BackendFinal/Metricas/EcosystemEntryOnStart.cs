// Assets/Content/Scripts/Backend/EcosystemEntryOnStart.cs
using UnityEngine;

public class EcosystemEntryOnStart : MonoBehaviour
{
    public string ecosystemName = "lobby";

    void Start()
    {
        EcosystemTimer.I?.NotifyEnter(ecosystemName);
        MetricsClient.I?.TrackEntradaEcosistema(ecosystemName);
    }
}
