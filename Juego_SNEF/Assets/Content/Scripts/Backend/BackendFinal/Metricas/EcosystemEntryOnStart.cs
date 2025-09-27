// Assets/Content/Scripts/Backend/EcosystemEntryOnStart.cs
using UnityEngine;

public class EcosystemEntryOnStart : MonoBehaviour
{
    public string ecosystemName = "lobby";

    void Start()
    {
        MetricsClient.I?.TrackEntradaEcosistema(ecosystemName);
    }
}
