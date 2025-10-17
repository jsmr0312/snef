// Assets/Content/Scripts/Backend/EcosystemEntryOnStart.cs
using UnityEngine;
using UnityEngine.SceneManagement;
public class EcosystemEntryOnStart : MonoBehaviour
{
    public string ecosystemName = "lobby";

    void Start()
    {
        EcosystemTimer.I?.NotifyEnter(ecosystemName);
        MetricsClient.I?.TrackEntradaEcosistema(ecosystemName);
        MetricsClient.I?.TrackEscenaVisitada(
            "ecosystem",
            ecosystemName,                               // scene_id
            SceneManager.GetActiveScene().name,          // scene_name
            ecosystemName                                // ecosystem_name
       );
    }
}
