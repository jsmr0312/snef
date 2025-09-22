using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class MetricsClient : MonoBehaviour
{
    public static MetricsClient I { get; private set; }

    [Header("API")]
    public string baseUrl = "https://api.estudiohera.mx";
    public string metricsPath = "/metricas/metricas";
    public string dashboardPath = "/metricas/dashboard";
    public string tokenPlayerPrefsKey = "auth_token";

    [Header("Opciones")]
    public float requestTimeout = 8f;
    public bool verboseLogs = true;
    public bool allowSendWithoutToken = false; // no envíes sin token

    const string KEY_QUEUE = "metrics_queue_v1";
    const string KEY_SESSION = "metrics_session_id";

    string _sessionId;

    [Serializable] class Envelope<T> { public string name; public T contenido; }
    [Serializable] public class AvatarSeleccionadoPayload { public string avatar_name; }

    void Awake()
    {
        if (I != null) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);

        _sessionId = PlayerPrefs.GetString(KEY_SESSION, "");
        if (string.IsNullOrEmpty(_sessionId))
        {
            _sessionId = Guid.NewGuid().ToString();
            PlayerPrefs.SetString(KEY_SESSION, _sessionId);
            PlayerPrefs.Save();
        }
        StartCoroutine(FlushQueue());
    }

    // === Helpers ===
    public void TrackAvatarSeleccionado(string avatarName)
        => PostEvent("avatar_seleccionado", new AvatarSeleccionadoPayload { avatar_name = avatarName });

    public void PostEvent<T>(string name, T contenido)
    {
        var env = new Envelope<T> { name = name, contenido = contenido };
        StartCoroutine(PostJson(JsonUtility.ToJson(env), reason: name));
    }

    // === POST núcleo ===
    System.Collections.IEnumerator PostJson(string json, string reason)
    {
        string rawTok = PlayerPrefs.GetString(tokenPlayerPrefsKey, "");
        string token = AuthBridge.SanitizeToken(rawTok); // <<< sanitiza aquí también

        if (string.IsNullOrEmpty(token))
        {
            if (allowSendWithoutToken)
                Debug.LogWarning("[Metrics] Enviando sin token (allowSendWithoutToken=true)");
            else
            {
                Debug.LogWarning("[Metrics] Cancelado: no token y allowSendWithoutToken=false.");
                yield break;
            }
        }

        string url = baseUrl.TrimEnd('/') + metricsPath;
        var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        if (!string.IsNullOrEmpty(token))
            req.SetRequestHeader("Authorization", "Bearer " + token); // ya limpio
        req.timeout = Mathf.CeilToInt(requestTimeout);

        if (verboseLogs) Debug.Log($"[Metrics POST] {url}\n{json}");
        yield return req.SendWebRequest();

        long code = req.responseCode;
#if UNITY_2020_2_OR_NEWER
        bool ok = (req.result == UnityWebRequest.Result.Success);
#else
        bool ok = (!req.isNetworkError && !req.isHttpError);
#endif
        if (verboseLogs) Debug.Log($"[Metrics RESP] code={code} body={req.downloadHandler.text} err={req.error}");

        if (!ok)
        {
            if (code >= 400 && code < 500)
            {
                Debug.LogError($"[Metrics] {code} :: {req.downloadHandler.text}  (NO se encola)");
                yield break;
            }
            Enqueue(json);
            if (verboseLogs) Debug.LogWarning("[Metrics] Falló, agregado a la cola: " + reason);
            yield break;
        }
        if (verboseLogs) Debug.Log("[Metrics OK] " + reason);
    }

    void Enqueue(string json)
    {
        var list = LoadQueue();
        list.Add(json);
        SaveQueue(list);
    }

    System.Collections.IEnumerator FlushQueue()
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
        string raw = PlayerPrefs.GetString(KEY_QUEUE, "{\"items\":[]}");
        try
        {
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

    // (opcional)
    public void TestDashboard() => StartCoroutine(GetDashboard());
    System.Collections.IEnumerator GetDashboard()
    {
        string url = baseUrl.TrimEnd('/') + dashboardPath;
        var req = UnityWebRequest.Get(url);
        req.timeout = Mathf.CeilToInt(requestTimeout);
        yield return req.SendWebRequest();
        Debug.Log($"[Dashboard GET] code={req.responseCode} body={req.downloadHandler.text}");
    }
}
