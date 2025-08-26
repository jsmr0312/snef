using UnityEngine;
using System;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
public class StandZoneTracker : MonoBehaviour
{
    [Header("Identidad del Stand (expuesta para inspector)")]
    public string stand_id = "00000000-0000-0000-0000-000000000000"; // usa UUID real
    public string stand_number = "A-01";
    public string stand_name = "FinTech México";
    public string company = "FinTech México";
    public string industry = "Financial Services";
    public string ecosystem_name = "Seguridad Financiera";

    [Header("Detección de jugador")]
    public string playerTag = "Player";

    [Header("Reglas")]
    [Tooltip("Solo mandar sponsor_visitado una vez por sesión")]
    public bool visitOncePerSession = true;

    private static HashSet<string> visitedThisSession = new HashSet<string>();

    private bool inZone = false;
    private float enterTime = 0f;

    void Reset()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (inZone) return;

        inZone = true;
        enterTime = Time.unscaledTime;

        // sponsor_visitado (una vez por sesión si así se configura)
        if (!visitOncePerSession || !visitedThisSession.Contains(stand_id))
        {
            visitedThisSession.Add(stand_id);
            var c = new AnalyticsClient.SponsorVisitContenido
            {
                session_id = AnalyticsIdentity.SessionId,
                user_id = AnalyticsIdentity.UserId,
                event_time = AnalyticsClient.NowIsoUtc(),
                stand_id = stand_id,
                stand_number = stand_number,
                stand_name = stand_name,
                company = company,
                industry = industry,
                ecosystem_name = ecosystem_name
            };
            AnalyticsClient.I?.TrackSponsorVisit(c);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (!inZone) return;

        float duration = Time.unscaledTime - enterTime;
        inZone = false;

        var c = new AnalyticsClient.TiempoEnStandContenido
        {
            session_id = AnalyticsIdentity.SessionId,
            user_id = AnalyticsIdentity.UserId,
            event_time = AnalyticsClient.NowIsoUtc(),
            stand_id = stand_id,
            duracion_segundos = Mathf.Max(0, Mathf.RoundToInt(duration)),
            ecosystem_name = ecosystem_name
        };
        AnalyticsClient.I?.TrackTiempoEnStand(c);
    }

    // Si se cierra la app/escena mientras el jugador está dentro, intenta cerrar el evento
    void OnDisable()
    {
        if (!inZone) return;
        float duration = Time.unscaledTime - enterTime;
        inZone = false;

        var c = new AnalyticsClient.TiempoEnStandContenido
        {
            session_id = AnalyticsIdentity.SessionId,
            user_id = AnalyticsIdentity.UserId,
            event_time = AnalyticsClient.NowIsoUtc(),
            stand_id = stand_id,
            duracion_segundos = Mathf.Max(0, Mathf.RoundToInt(duration)),
            ecosystem_name = ecosystem_name
        };
        AnalyticsClient.I?.TrackTiempoEnStand(c);
    }
}
