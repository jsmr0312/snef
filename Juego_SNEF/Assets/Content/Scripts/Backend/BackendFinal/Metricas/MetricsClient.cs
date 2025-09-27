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

    [Header("Comportamiento")]
    public float requestTimeout = 8f;
    public bool verboseLogs = true;

    [Tooltip("Si true y NO hay token, enviará SIN Authorization (para modo anónimo). " +
             "Si tu backend exige JWT, déjalo en false y se bufferiza hasta que haya token.")]
    public bool allowSendWithoutToken = false;

    const string KEY_SESSION = "metrics_session_id";
    string _sessionId;
    List<string> _preTokenBuffer = new();

    [Serializable] class Envelope<T> { public string name; public T contenido; }

    [Serializable]
    class CanonicalBase
    {
        public string session_id;
        public string user_id;    // uuid | null
        public string event_id;   // uuid por evento
        public string event_time; // ISO-8601 UTC
    }

    // ====== PAYLOADS ======
    [Serializable] class AvatarSeleccionadoPayload : CanonicalBase { public string avatar_name; }
    [Serializable] class EcosystemEntryPayload : CanonicalBase { public string ecosystem_name; }

    [Serializable]
    class SponsorVisitPayload : CanonicalBase
    {
        public string stand_id; public string stand_number; public string ecosystem_name;
    }

    [Serializable]
    class TiempoEnStandPayload : CanonicalBase
    {
        public string stand_id; public int duracion_segundos;
    }

    [Serializable]
    class ContenidoVisualizadoPayload : CanonicalBase
    {
        public string stand_id; public string asset_id; public string ecosystem_name;
        public int duracion_segundos; public bool completed; public int progreso_pct;
    }

    [Serializable]
    class ClickEnlaceExternoPayload : CanonicalBase
    {
        public string stand_id; public string url; public string network; // instagram|facebook|web|linkedin|other
    }

    [Serializable]
    class TriviaIniciadaPayload : CanonicalBase
    {
        public string stand_id; public string trivia_id;
    }

    [Serializable]
    class TriviaCompletadaPayload : CanonicalBase
    {
        public string stand_id; public string trivia_id;
        public int aciertos; public int errores; public int tiempo_segundos; public int coins;
    }

    [Serializable]
    class IntentoQuizPayload : CanonicalBase
    {
        public string stand_id; public string trivia_id; public int attempt_index;
        public int tiempo_segundos; public int aciertos; public int errores;
    }

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

        WebGLBridge.OnTokenChanged += OnTokenArrived;
    }

    void OnDestroy() => WebGLBridge.OnTokenChanged -= OnTokenArrived;

    void OnTokenArrived(string tok)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (string.IsNullOrEmpty(tok)) return;
        if (_preTokenBuffer.Count == 0) return;
        if (verboseLogs) Debug.Log($"[Metrics] Token llegó. Flush buffer ({_preTokenBuffer.Count})…");
        foreach (var json in new List<string>(_preTokenBuffer))
            StartCoroutine(PostJson(json, "pretoken_flush"));
        _preTokenBuffer.Clear();
#endif
    }

    // ====== API pública ======

    public void TrackAvatarSeleccionado(string avatarName)
    {
        var p = NewCanonical<AvatarSeleccionadoPayload>(); p.avatar_name = avatarName;
        PostEvent("avatar_seleccionado", p);
    }

    public void TrackEntradaEcosistema(string ecosystemName)
    {
        var p = NewCanonical<EcosystemEntryPayload>(); p.ecosystem_name = ecosystemName;
        PostEvent("entrada_ecosistema", p);
    }

    public void TrackSponsorVisitado(string standId, string standNumber, string ecosystemName)
    {
        var p = NewCanonical<SponsorVisitPayload>();
        p.stand_id = standId; p.stand_number = standNumber; p.ecosystem_name = ecosystemName;
        PostEvent("sponsor_visitado", p);
    }

    public void TrackTiempoEnStand(string standId, int duracionSegundos)
    {
        var p = NewCanonical<TiempoEnStandPayload>();
        p.stand_id = standId; p.duracion_segundos = Mathf.Max(0, duracionSegundos);
        PostEvent("tiempo_en_stand", p);
    }

    public void TrackContenidoVisualizado(string standId, string assetId, string ecosystemName,
                                          int duracionSegundos, bool completed, int progresoPct)
    {
        var p = NewCanonical<ContenidoVisualizadoPayload>();
        p.stand_id = standId; p.asset_id = assetId; p.ecosystem_name = ecosystemName;
        p.duracion_segundos = Mathf.Max(0, duracionSegundos);
        p.completed = completed; p.progreso_pct = Mathf.Clamp(progresoPct, 0, 100);
        PostEvent("contenido_visualizado", p);
    }

    public void TrackClickEnlaceExterno(string standId, string url, string network)
    {
        var p = NewCanonical<ClickEnlaceExternoPayload>();
        p.stand_id = standId; p.url = url; p.network = string.IsNullOrEmpty(network) ? "other" : network.ToLowerInvariant();
        PostEvent("click_enlace_externo", p);
    }

    public void TrackTriviaIniciada(string standId, string triviaId)
    {
        var p = NewCanonical<TriviaIniciadaPayload>(); p.stand_id = standId; p.trivia_id = triviaId;
        PostEvent("trivia_iniciada", p);
    }

    public void TrackTriviaCompletada(string standId, string triviaId, int aciertos, int errores, int tiempoSeg, int coins)
    {
        var p = NewCanonical<TriviaCompletadaPayload>();
        p.stand_id = standId; p.trivia_id = triviaId; p.aciertos = aciertos; p.errores = errores;
        p.tiempo_segundos = Mathf.Max(0, tiempoSeg); p.coins = Mathf.Max(0, coins);
        PostEvent("trivia_completada", p);
    }

    public void TrackIntentoQuiz(string standId, string triviaId, int attemptIndex, int tiempoSeg, int aciertos, int errores)
    {
        var p = NewCanonical<IntentoQuizPayload>();
        p.stand_id = standId; p.trivia_id = triviaId; p.attempt_index = Mathf.Max(1, attemptIndex);
        p.tiempo_segundos = Mathf.Max(0, tiempoSeg); p.aciertos = aciertos; p.errores = errores;
        PostEvent("intento_quiz", p);
    }

    // ====== Helpers ======
    T NewCanonical<T>() where T : CanonicalBase, new()
    {
        return new T
        {
            session_id = _sessionId,
            user_id = JwtLite.GetUserId(WebGLBridge.Token),
            event_id = Guid.NewGuid().ToString(),
            event_time = DateTime.UtcNow.ToString("o")
        };
    }

    void PostEvent<T>(string name, T contenido) where T : class
    {
        var env = new Envelope<T> { name = name, contenido = contenido };
        var json = JsonUtility.ToJson(env);
        StartCoroutine(PostJson(json, name));
    }

    IEnumerator PostJson(string json, string reason)
    {
        var token = GetToken();

#if UNITY_WEBGL && !UNITY_EDITOR
        if (string.IsNullOrEmpty(token) && !allowSendWithoutToken)
        {
            if (verboseLogs) Debug.Log($"[Metrics] Aún sin token. Bufferizando: {reason}");
            _preTokenBuffer.Add(json);
            yield break;
        }
#endif

        string url = baseUrl.TrimEnd('/') + metricsPath;
        var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        if (!string.IsNullOrEmpty(token)) req.SetRequestHeader("Authorization", "Bearer " + token);
        req.timeout = Mathf.CeilToInt(requestTimeout);

        if (verboseLogs) Debug.Log($"[Metrics POST] {url}\n{json}");
        yield return req.SendWebRequest();

        bool ok = (req.result == UnityWebRequest.Result.Success) && (req.responseCode < 400);
        if (!ok)
        {
            long code = req.responseCode;
            if (code >= 400 && code < 500)
                Debug.LogError($"[Metrics] {code} :: {req.downloadHandler.text} (no reintenta)");
            else
                Debug.LogWarning($"[Metrics] {code} :: {req.error} (transitorio)");
            yield break;
        }

        if (verboseLogs) Debug.Log("[Metrics OK] " + reason);
    }

    string GetToken()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return Sanitize(WebGLBridge.Token);
#else
        return Sanitize(string.IsNullOrEmpty(WebGLBridge.Token)
            ? PlayerPrefs.GetString("auth_token", "")
            : WebGLBridge.Token);
#endif
    }

    static string Sanitize(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        s = s.Trim().Replace("\r", "").Replace("\n", "");
        if (s.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) s = s[7..].Trim();
        return s;
    }

    public int GetBufferedCountForDebug() => _preTokenBuffer != null ? _preTokenBuffer.Count : 0;
}
