using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class MetricsClient : MonoBehaviour
{
    public static MetricsClient I { get; private set; }

    [Header("API")]
    public string baseUrl = "https://api.tu-backend.com";
    public string metricsPath = "/metricas/metricas";
    public string dashboardPath = "/metricas/dashboard";

    [Header("Compat Editor (solo fuera de WebGL)")]
    public string tokenPlayerPrefsKey = "auth_token";

    [Header("Comportamiento")]
    public float requestTimeout = 8f;
    public bool verboseLogs = true;
    public bool allowSendWithoutToken = false; // en prod: false

    const string KEY_QUEUE = "metrics_queue_v1";
    const string KEY_SESSION = "metrics_session_id";

    string _sessionId;
    List<string> _preTokenBuffer = new List<string>(); // eventos antes de tener token

    [Serializable]
    class Envelope<T>
    {
        public string name; public T contenido; public string session_id; public string event_time;
    }
    [Serializable] public class AvatarSeleccionadoPayload { public string avatar_id; }
    [Serializable] public class AvatarScreenPayload { public string action; public string avatar_id; }

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);

        _sessionId = PlayerPrefs.GetString(KEY_SESSION, "");
        if (string.IsNullOrEmpty(_sessionId))
        {
            _sessionId = Guid.NewGuid().ToString();
            PlayerPrefs.SetString(KEY_SESSION, _sessionId);
            PlayerPrefs.Save();
        }

        // Suscríbete para saber cuándo llega el token y hacer flush del buffer
        WebGLBridge.OnTokenChanged += OnTokenArrived;

        StartCoroutine(FlushQueue());
    }

    void OnDestroy()
    {
        WebGLBridge.OnTokenChanged -= OnTokenArrived;
    }

    void OnTokenArrived(string tok)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (string.IsNullOrEmpty(tok)) return;
        if (_preTokenBuffer.Count == 0) return;

        if (verboseLogs) Debug.Log($"[Metrics] Token llegó. Enviando buffer pre-token ({_preTokenBuffer.Count})…");
        foreach (var json in new List<string>(_preTokenBuffer))
            StartCoroutine(PostJson(json, "pretoken_flush"));

        _preTokenBuffer.Clear();
#endif
    }

    // === Eventos públicos (escena avatar) ===
    public void TrackAvatarScreenEntered() =>
        PostEvent("avatar_screen_event", new AvatarScreenPayload { action = "entered", avatar_id = null });

    public void TrackAvatarCardViewed(string avatarId) =>
        PostEvent("avatar_screen_event", new AvatarScreenPayload { action = "viewed", avatar_id = avatarId });

    public void TrackAvatarSeleccionado(string avatarId) =>
        PostEvent("avatar_seleccionado", new AvatarSeleccionadoPayload { avatar_id = avatarId });

    public void PostEvent<T>(string name, T contenido)
    {
        var env = new Envelope<T>
        {
            name = name,
            contenido = contenido,
            session_id = _sessionId,
            event_time = DateTime.UtcNow.ToString("o")
        };
        var json = JsonUtility.ToJson(env);
        StartCoroutine(PostJson(json, name));
    }

    IEnumerator PostJson(string json, string reason)
    {
        string token = GetTokenForThisPlatform();

#if UNITY_WEBGL && !UNITY_EDITOR
        // En WebGL: si aún no hay token, BUFERIZA (no uses PlayerPrefs aquí)
        if (string.IsNullOrEmpty(token))
        {
            if (allowSendWithoutToken)
            {
                if (verboseLogs) Debug.LogWarning("[Metrics] SIN token (allowSendWithoutToken=true) → se enviará sin Authorization.");
            }
            else
            {
                if (verboseLogs) Debug.Log($"[Metrics] Aún sin token. Bufferizando evento: {reason}");
                _preTokenBuffer.Add(json);
                yield break;
            }
        }
#endif

        string url = baseUrl.TrimEnd('/') + metricsPath;

        var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        if (!string.IsNullOrEmpty(token))
            req.SetRequestHeader("Authorization", "Bearer " + token);

        req.timeout = Mathf.CeilToInt(requestTimeout);

        if (verboseLogs) Debug.Log($"[Metrics POST] {url}\n{json}");
        yield return req.SendWebRequest();

        bool ok = (req.result == UnityWebRequest.Result.Success) && (req.responseCode < 400);
        long code = req.responseCode;
        if (verboseLogs) Debug.Log($"[Metrics RESP] code={code} body={req.downloadHandler.text} err={req.error}");

        if (!ok)
        {
            // 4xx → NO encolar (token inválido/expirado, dispositivo no autorizado, etc.)
            if (code >= 400 && code < 500)
            {
                Debug.LogError($"[Metrics] {code} :: {req.downloadHandler.text}  (NO se encola)");
                yield break;
            }
            // Otras fallas → encolar
            Enqueue(json);
            if (verboseLogs) Debug.LogWarning("[Metrics] Falló, agregado a la cola: " + reason);
            yield break;
        }
        if (verboseLogs) Debug.Log("[Metrics OK] " + reason);
    }

    string GetTokenForThisPlatform()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // En WebGL NO usamos PlayerPrefs como fallback (evita token viejo/expirado)
        return SanitizeToken(WebGLBridge.Token);
#else
        // En Editor puedes permitir fallback para pruebas locales
        var t = !string.IsNullOrEmpty(WebGLBridge.Token) ? WebGLBridge.Token : PlayerPrefs.GetString(tokenPlayerPrefsKey, "");
        return SanitizeToken(t);
#endif
    }

    void Enqueue(string json)
    {
        var list = LoadQueue();
        list.Add(json);
        SaveQueue(list);
    }

    IEnumerator FlushQueue()
    {
        var list = LoadQueue();
        if (list.Count == 0) yield break;
        if (verboseLogs) Debug.Log($"[Metrics] FlushQueue {list.Count} pendientes…");
        foreach (var json in new List<string>(list))
        {
            yield return PostJson(json, "retry_queue");
            var fresh = LoadQueue();
            fresh.Remove(json);
            SaveQueue(fresh);
        }
    }

    List<string> LoadQueue()
    {
        try
        {
            string raw = PlayerPrefs.GetString(KEY_QUEUE, "{\"items\":[]}");
            var w = JsonUtility.FromJson<StringArrayWrapper>(raw);
            return new List<string>(w.items ?? new string[0]);
        }
        catch { return new List<string>(); }
    }
    void SaveQueue(List<string> items)
    {
        var w = new StringArrayWrapper { items = items.ToArray() };
        PlayerPrefs.SetString(KEY_QUEUE, JsonUtility.ToJson(w));
        PlayerPrefs.Save();
    }
    [Serializable] class StringArrayWrapper { public string[] items; }

    public void TestDashboard() => StartCoroutine(GetDashboard());
    IEnumerator GetDashboard()
    {
        string url = baseUrl.TrimEnd('/') + dashboardPath;
        var req = UnityWebRequest.Get(url);
        req.timeout = Mathf.CeilToInt(requestTimeout);
        yield return req.SendWebRequest();
        Debug.Log($"[Dashboard GET] code={req.responseCode} body={req.downloadHandler.text}");
    }

    static string SanitizeToken(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var t = raw.Trim();
        if (t.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            t = t.Substring(7).Trim();
        t = t.Replace("\r", "").Replace("\n", "");
        return t;
    }

    // dentro de MetricsClient (clase), al final:
    public int GetBufferedCountForDebug()
    {
        // Devuelve 0 si no hay buffer (o en plataformas que no lo usan)
        try
        {
            var field = typeof(MetricsClient).GetField("_preTokenBuffer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var list = field != null ? field.GetValue(this) as System.Collections.ICollection : null;
            return list != null ? list.Count : 0;
        }
        catch { return 0; }
    }

}
