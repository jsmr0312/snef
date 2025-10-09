using UnityEngine;

[RequireComponent(typeof(Collider))]
public class StandZone : MonoBehaviour
{
    [Header("Metadata del Stand")]
    public string standId;        // uuid
    public string standNumber;    // "E1-03" etc.
    public string ecosystemName;  // "Ecosistema 1"...

    [Header("Anti-rebote")]
    [Tooltip("Evita re-disparos por reaparición del collider del Player o múltiples colliders.")]
    public bool guardAgainstMultipleEnters = true;

    [Tooltip("Si re-entras muy rápido, ignora el evento (segundos). 0 = sin cooldown")]
    [Range(0f, 2f)] public float reenterCooldownSeconds = 0.75f;

    float _enterRealtime;
    bool _inside;
    float _lastEnterSentTime = -999f;

    // Acepta collider del Player en el propio objeto, su rigidbody o en la raíz
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

        // ⛔️ Ya estábamos "dentro" (por ejemplo, se ocultó y reapareció el player dentro del stand)
        if (guardAgainstMultipleEnters && _inside) return;

        // ⛔️ Re-entrada muy rápida (rebote por múltiples colliders/habilitar-deshabilitar)
        if (reenterCooldownSeconds > 0f && now - _lastEnterSentTime < reenterCooldownSeconds) return;

        _inside = true;
        _enterRealtime = now;
        _lastEnterSentTime = now;

        StandContext.I?.SetCurrentStand(standId, standNumber, ecosystemName);
        MetricsClient.I?.TrackSponsorVisitado(standId, standNumber, ecosystemName);
    }

    void OnTriggerExit(Collider other)
    {
        if (!IsPlayerCollider(other)) return;
        FlushTime();
    }

    void OnDisable()
    {
        // Si el stand se desactiva mientras el player estaba dentro, cierra la medición
        if (_inside) FlushTime();
    }

    void FlushTime()
    {
        _inside = false;
        int dur = Mathf.Max(0, Mathf.RoundToInt(Time.realtimeSinceStartup - _enterRealtime));
        MetricsClient.I?.TrackTiempoEnStand(standId, dur);
    }
}
