using UnityEngine;

[RequireComponent(typeof(Collider))]
public class StandZone : MonoBehaviour
{
    [Header("Metadata del Stand")]
    public string standId;        // uuid
    public string standNumber;    // "E1-03" etc.
    public string ecosystemName;  // "Ecosistema 1"...

    float _enterRealtime;
    bool _inside;

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        _inside = true;
        _enterRealtime = Time.realtimeSinceStartup;

        StandContext.I?.SetCurrentStand(standId, standNumber, ecosystemName);
        MetricsClient.I?.TrackSponsorVisitado(standId, standNumber, ecosystemName);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        FlushTime();
    }

    void OnDisable()
    {
        if (_inside) FlushTime();
    }

    void FlushTime()
    {
        _inside = false;
        int dur = Mathf.Max(0, Mathf.RoundToInt(Time.realtimeSinceStartup - _enterRealtime));
        MetricsClient.I?.TrackTiempoEnStand(standId, dur);
    }
}

