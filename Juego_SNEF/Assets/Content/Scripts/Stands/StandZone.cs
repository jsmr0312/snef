using UnityEngine;

[RequireComponent(typeof(Collider))]
public class StandZone : MonoBehaviour
{
    [Header("Metadata del Stand")]
    public string standId;        // uuid
    public string standNumber;    // "E1-03" etc.
    public string ecosystemName;  // "Ecosistema 1"...
    public string standType = "master";  // master|excellence|premier|xp|punto_experiencia

    [Header("Anti-rebote")]
    [Tooltip("Evita re-disparos por reaparición del collider del Player o múltiples colliders.")]
    public bool guardAgainstMultipleEnters = true;

    [Tooltip("Si re-entras muy rápido, ignora el evento (segundos). 0 = sin cooldown")]
    [Range(0f, 2f)] public float reenterCooldownSeconds = 0.75f;

    float _enterRealtime;
    bool _inside;
    float _lastEnterSentTime = -999f;

    bool IsXpPoint()
    {
        var t = (standType ?? "").ToLowerInvariant().Replace(" ", "_");
        return t == "xp" || t == "punto_de_experiencia" || t == "punto_experiencia"
            || t.Contains("xp") || t.Contains("experien");
    }

    bool IsPlayerCollider(Collider other)
    {
        if (other.CompareTag("Player")) return true;
        if (other.attachedRigidbody && other.attachedRigidbody.CompareTag("Player")) return true;
        var root = other.transform.root;
        return root && root.CompareTag("Player");
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsPlayerCollider(other)) return;

        float now = Time.realtimeSinceStartup;

        if (guardAgainstMultipleEnters && _inside) return;
        if (reenterCooldownSeconds > 0f && now - _lastEnterSentTime < reenterCooldownSeconds) return;

        _inside = true;
        _enterRealtime = now;
        _lastEnterSentTime = now;

        StandContext.I?.SetCurrentStand(standId, standNumber, ecosystemName);

        // IMPORTANTE:
        // - Stands normales: sponsor_visitado
        // - Punto de experiencia: NO mandamos sponsor_visitado (sólo medimos tiempo XP)
        if (!IsXpPoint())
            MetricsClient.I?.TrackSponsorVisitado(standId, standNumber, ecosystemName);
    }

    void OnTriggerExit(Collider other)
    {
        if (!IsPlayerCollider(other)) return;
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

        if (IsXpPoint())
            MetricsClient.I?.TrackTiempoEnPuntoExperiencia(standId, ecosystemName, dur);
        else
            MetricsClient.I?.TrackTiempoEnStand(standId, dur);
    }
}
