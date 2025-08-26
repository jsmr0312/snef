using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;

public class AnalyticsClient : MonoBehaviour
{
    public static AnalyticsClient I;

    [Header("Endpoint")]
    [Tooltip("URL absoluta del endpoint que recibirá los eventos")]
    public string baseUrl = "https://TU_API/events";  // <- cámbialo

    [Header("Modo prueba")]
    public bool logOnly = true;   // imprime JSON en consola sin hacer POST

    void Awake()
    {
        if (I != null) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
    }

    // ===== Utilidades =====
    public static string NowIsoUtc() => DateTime.UtcNow.ToString("o");

    // Envelope genérico: { "name": "...", "contenido": {...} }
    [Serializable]
    class Envelope<T>
    {
        public string name;
        public T contenido;
        public Envelope(string n, T c) { name = n; contenido = c; }
    }

    // ---- Eventos concretos que vamos a usar hoy ----
    [Serializable]
    public class SponsorVisitContenido
    {
        public string session_id;
        public string user_id;
        public string event_time;
        public string stand_id;
        public string stand_number;
        public string stand_name;
        public string company;
        public string industry;
        public string ecosystem_name;
    }

    [Serializable]
    public class TiempoEnStandContenido
    {
        public string session_id;
        public string user_id;
        public string event_time;
        public string stand_id;
        public int duracion_segundos;
        public string ecosystem_name;
    }

    // API pública para disparar ambos
    public void TrackSponsorVisit(SponsorVisitContenido c) =>
        Send("sponsor_visitado", c);

    public void TrackTiempoEnStand(TiempoEnStandContenido c) =>
        Send("tiempo_en_stand", c);

    // ===== Envío =====
    void Send<T>(string name, T contenido)
    {
        var env = new Envelope<T>(name, contenido);
        string json = JsonUtility.ToJson(env);

        if (logOnly)
        {
            Debug.Log($"[Analytics][LOG ONLY] {json}");
            return;
        }
        StartCoroutine(PostJson(json));
    }

    IEnumerator PostJson(string json)
    {
        var req = new UnityWebRequest(baseUrl, "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
            Debug.LogWarning($"[Analytics] Error {req.responseCode}: {req.error}\n{req.downloadHandler.text}");
        else
            Debug.Log("[Analytics] OK");
    }
}
