// MetricsClient.cs
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
    public string baseUrl = "https://api.estudiohera.mx";
    public string metricsPath = "/metricas/metricas";
    [Tooltip("Re-emite 'sesion_iniciada' cada N segundos para mantener viva la sesión (0 = off).")]
    public int heartbeatSeconds = 540; // 9 minutos

    [Header("Comportamiento")]
    public float requestTimeout = 8f;
    public bool verboseLogs = true;

    [Tooltip("Si true y NO hay token, enviará SIN Authorization (para modo anónimo). " +
             "Si tu backend exige JWT, déjalo en false y se bufferiza hasta que haya token.")]
    public bool allowSendWithoutToken = false;

    [Header("Recuperación de sesión")]
    [Tooltip("Logs extra de recuperación")]
    [SerializeField] bool verboseRecovery = true;
    bool _sessionRecoveryInFlight = false;             // evita reentradas
    float _recoveryCooldownUntil = 0f;                 // antirruido tras un fallo
    [SerializeField] float recoveryCooldownSeconds = 30f;

    const string KEY_SESSION = "metrics_session_id";
    string _sessionId;
    List<string> _preTokenBuffer = new();

    class Runner : MonoBehaviour { }
    static Runner _runner;
    static Runner EnsureRunner()
    {
        if (_runner == null)
        {
            var go = new GameObject("MetricsRunner");
            DontDestroyOnLoad(go);
            _runner = go.AddComponent<Runner>();
        }
        return _runner;
    }

    [Serializable] class Envelope<T> { public string name; public T contenido; }

    [Serializable]
    class CanonicalBase
    {
        public string session_id;
        public string user_id;    // uuid | null
        public string event_id;   // uuid por evento
        public string event_time; // ISO-8601 UTC
    }

    // ====== PAYLOADS EXISTENTES ======
    [Serializable] class AvatarSeleccionadoPayload : CanonicalBase { public string avatar_name; }
    [Serializable] class EcosystemEntryPayload : CanonicalBase { public string ecosystem_name; }
    [Serializable] class SponsorVisitPayload : CanonicalBase { public string stand_id; public string stand_number; public string ecosystem_name; }
    [Serializable] class TiempoEnStandPayload : CanonicalBase { public string stand_id; public int duracion_segundos; }
    [Serializable] class ContenidoVisualizadoPayload : CanonicalBase { public string stand_id; public string asset_id; public string ecosystem_name; public int duracion_segundos; public bool completed; public int progreso_pct; }
    [Serializable] class ClickEnlaceExternoPayload : CanonicalBase { public string stand_id; public string url; public string network; }
    [Serializable] class TriviaIniciadaPayload : CanonicalBase { public string stand_id; public string trivia_id; }
    [Serializable] class TriviaCompletadaPayload : CanonicalBase { public string stand_id; public string trivia_id; public int aciertos; public int errores; public int tiempo_segundos; public int coins; }
    [Serializable] class IntentoQuizPayload : CanonicalBase { public string stand_id; public string trivia_id; public int attempt_index; public int tiempo_segundos; public int aciertos; public int errores; }

    // ====== NUEVOS PAYLOADS: MINIJUEGOS + PRESUPUESTO ======
    [Serializable] class EntradaMinijuegoPayload : CanonicalBase { public string stand_id; public string minigame_name; }
    [Serializable] class TiempoEnMinijuegoPayload : CanonicalBase { public string stand_id; public string minigame_name; public int duracion_segundos; public bool completed; }
    [Serializable] class MinijuegoFinalizadoPayload : CanonicalBase { public string stand_id; public string minigame_name; public int score; public string outcome; public int coins; public int xp; }
    [Serializable] class MonedasObtenidasPayload : CanonicalBase { public int amount; public string motivo; public string stand_id; public string ecosystem_name; }

    // === Payloads nuevos adicionales ===
    [Serializable] class MisionCompletadaPayload : CanonicalBase { public string ecosystem_name; public string mision_name; }
    [Serializable] class LogroDesbloqueadoPayload : CanonicalBase { public string achievement_id; public string name; public string categoria; public int puntos; }
    [Serializable] class TiempoEnEcosistemaPayload : CanonicalBase { public string ecosystem_name; public int duracion_segundos; public bool completed; }
    [Serializable] class SalidaEcosistemaPayload : CanonicalBase { public string ecosystem_name; public string reason; }
    [Serializable] class PresupuestoPayload : CanonicalBase { public int amount; public string motivo; public string stand_id; public string ecosystem_name; }
    [Serializable] class PuntajeGeneralJugadorPayload : CanonicalBase { public int puntaje; public string tipo; }
    [Serializable] class SesionIniciadaPayload : CanonicalBase { public string client; }

    [Serializable]
    class ContenidoDescargadoPayload : CanonicalBase
    {
        public bool is_sponsored; public string stand_id; public string asset_id;
        public string asset_type; public int bytes; public string source; public string url; public bool success;
    }

    [Serializable] class TiempoEnPuntoExperienciaPayload : CanonicalBase { public string xp_point_id; public string ecosystem_name; public int duracion_segundos; }
    [Serializable] class ContenidoRevisadoXpPayload : CanonicalBase { public string xp_point_id; public string asset_id; }

    [Serializable]
    class EscenaVisitadaPayload : CanonicalBase
    {
        public string scene_type; public string scene_id; public string scene_name; public string ecosystem_name;
    }

    [Serializable]
    class TiendaItemActualizadoPayload : CanonicalBase
    {
        public string item_id; public string item_name; public bool status; public int price;
    }

    [Serializable] class CertificadoDesbloqueadoPayload : CanonicalBase { public bool status; }
    [Serializable] class CertificadoDescargadoPayload : CanonicalBase { public string name; public bool status; }

    // ===== Trackers =====
    public void TrackCertificadoDesbloqueado(bool status)
    { var p = NewCanonical<CertificadoDesbloqueadoPayload>(); p.status = status; PostEvent("certificado_desbloqueado", p); }

    public void TrackCertificadoDescargado(string name, bool status)
    { var p = NewCanonical<CertificadoDescargadoPayload>(); p.name = (name ?? "").Trim(); p.status = status; PostEvent("certificado_descargado", p); }

    public void TrackTiempoEnEcosistema(string ecosystemName, int durSeg, bool completed)
    { var p = NewCanonical<TiempoEnEcosistemaPayload>(); p.ecosystem_name = ecosystemName; p.duracion_segundos = Mathf.Max(0, durSeg); p.completed = completed; PostEvent("tiempo_en_ecosistema", p); }

    public void TrackSalidaEcosistema(string ecosystemName, string reason = "exit")
    { var p = NewCanonical<SalidaEcosistemaPayload>(); p.ecosystem_name = ecosystemName; p.reason = string.IsNullOrEmpty(reason) ? "exit" : reason; PostEvent("salida_ecosistema", p); }

    public void TrackMisionCompletada(string ecosystemName, string misionName)
    { var p = NewCanonical<MisionCompletadaPayload>(); p.ecosystem_name = ecosystemName; p.mision_name = misionName; PostEvent("mision_completada", p); }

    public void TrackLogroDesbloqueado(string achievementId, string name, string categoria, int puntos)
    { var p = NewCanonical<LogroDesbloqueadoPayload>(); p.achievement_id = achievementId; p.name = name; p.categoria = string.IsNullOrEmpty(categoria) ? "progreso" : categoria; p.puntos = Mathf.Max(0, puntos); PostEvent("logro_desbloqueado", p); }

    public void TrackTiendaItemActualizado(string itemId, string itemName, bool status, int price)
    { var p = NewCanonical<TiendaItemActualizadoPayload>(); p.item_id = itemId ?? ""; p.item_name = itemName ?? ""; p.status = status; p.price = Mathf.Max(0, price); PostEvent("tienda_item_actualizado", p); }

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

    void Start()
    {
        if (heartbeatSeconds > 0) InvokeRepeating(nameof(SendHeartbeat), heartbeatSeconds, heartbeatSeconds);
    }

    void OnApplicationFocus(bool focus) { if (focus) TrackSesionIniciada(); }
    void OnApplicationPause(bool paused) { if (!paused) TrackSesionIniciada(); }

    void SendHeartbeat()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (string.IsNullOrEmpty(GetToken())) return;
#endif
        TrackSesionIniciada();
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

    void ResetSessionId()
    {
        _sessionId = Guid.NewGuid().ToString();
        PlayerPrefs.SetString(KEY_SESSION, _sessionId);
        PlayerPrefs.Save();
        if (verboseLogs) Debug.Log($"[Metrics] Nuevo session_id: {_sessionId}");
    }

    public void TrackSesionIniciada()
    {
        var p = NewCanonical<SesionIniciadaPayload>();
        p.client = "unity_webgl";
        PostEvent("sesion_iniciada", p);
    }

    // ====== API pública EXISTENTE ======
    public void TrackAvatarSeleccionado(string avatarName)
    { var p = NewCanonical<AvatarSeleccionadoPayload>(); p.avatar_name = avatarName; PostEvent("avatar_seleccionado", p); }

    public void TrackEntradaEcosistema(string ecosystemName)
    { var p = NewCanonical<EcosystemEntryPayload>(); p.ecosystem_name = ecosystemName; PostEvent("entrada_ecosistema", p); }

    public void TrackSponsorVisitado(string standId, string standNumber, string ecosystemName)
    { var p = NewCanonical<SponsorVisitPayload>(); p.stand_id = standId; p.stand_number = standNumber; p.ecosystem_name = ecosystemName; PostEvent("sponsor_visitado", p); }

    public void TrackTiempoEnStand(string standId, int duracionSegundos)
    { var p = NewCanonical<TiempoEnStandPayload>(); p.stand_id = standId; p.duracion_segundos = Mathf.Max(0, duracionSegundos); PostEvent("tiempo_en_stand", p); }

    public void TrackContenidoVisualizado(string standId, string assetId, string ecosystemName, int duracionSegundos, bool completed, int progresoPct)
    {
        var p = NewCanonical<ContenidoVisualizadoPayload>();
        p.stand_id = standId; p.asset_id = assetId; p.ecosystem_name = ecosystemName;
        p.duracion_segundos = Mathf.Max(0, duracionSegundos);
        p.completed = completed; p.progreso_pct = Mathf.Clamp(progresoPct, 0, 100);
        PostEvent("contenido_visualizado", p);
    }

    public void TrackEscenaVisitada(string sceneType, string sceneId, string sceneName, string ecosystemName)
    {
        var p = NewCanonical<EscenaVisitadaPayload>();
        p.scene_type = string.IsNullOrEmpty(sceneType) ? "other" : sceneType;
        p.scene_id = sceneId ?? "";
        p.scene_name = sceneName ?? "";
        p.ecosystem_name = ecosystemName ?? "";
        PostEvent("escena_visitada", p);
    }

    public void TrackClickEnlaceExterno(string standId, string url, string network)
    { var p = NewCanonical<ClickEnlaceExternoPayload>(); p.stand_id = standId; p.url = url; p.network = string.IsNullOrEmpty(network) ? "other" : network.ToLowerInvariant(); PostEvent("click_enlace_externo", p); }

    public void TrackTriviaIniciada(string standId, string triviaId)
    { var p = NewCanonical<TriviaIniciadaPayload>(); p.stand_id = standId; p.trivia_id = triviaId; PostEvent("trivia_iniciada", p); }

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

    public void TrackContenidoDescargado(bool isSponsored, string standId, string assetId, string assetType, int bytes, string source, string url, bool success)
    {
        var p = NewCanonical<ContenidoDescargadoPayload>();
        p.is_sponsored = isSponsored; p.stand_id = standId ?? ""; p.asset_id = assetId ?? "";
        p.asset_type = string.IsNullOrEmpty(assetType) ? "other" : assetType.ToLowerInvariant();
        p.bytes = Mathf.Max(0, bytes);
        p.source = string.IsNullOrEmpty(source) ? "external" : source.ToLowerInvariant();
        p.url = url ?? ""; p.success = success;
        PostEvent("contenido_descargado", p);
    }

    // ====== API pública NUEVA: Minijuegos + Presupuesto ======
    public void TrackEntradaMinijuego(string standId, string minigameName)
    { var p = NewCanonical<EntradaMinijuegoPayload>(); p.stand_id = standId; p.minigame_name = minigameName; PostEvent("entrada_minijuego", p); }

    public void TrackTiempoEnMinijuego(string standId, string minigameName, int durSeg, bool completed)
    { var p = NewCanonical<TiempoEnMinijuegoPayload>(); p.stand_id = standId; p.minigame_name = minigameName; p.duracion_segundos = Mathf.Max(0, durSeg); p.completed = completed; PostEvent("tiempo_en_minijuego", p); }

    public void TrackMinijuegoFinalizado(string standId, string minigameName, int score, string outcome, int coins, int xp)
    {
        var p = NewCanonical<MinijuegoFinalizadoPayload>();
        p.stand_id = standId; p.minigame_name = minigameName; p.score = Mathf.Max(0, score);
        p.outcome = string.IsNullOrEmpty(outcome) ? "win" : outcome;
        p.coins = Mathf.Max(0, coins); p.xp = Mathf.Max(0, xp);
        PostEvent("minijuego_finalizado", p);
    }

    public void TrackPuntajeGeneralJugador(int total, int delta)
    {
        var pTotal = NewCanonical<PuntajeGeneralJugadorPayload>();
        pTotal.puntaje = Mathf.Max(0, total); pTotal.tipo = "total";
        PostEvent("puntaje_general_jugador", pTotal);
        // Delta opcional:
        // var pDelta = NewCanonical<PuntajeGeneralJugadorPayload>();
        // pDelta.puntaje = delta; pDelta.tipo = "delta";
        // PostEvent("puntaje_general_jugador", pDelta);
    }

    public void TrackPresupuesto(int amount, string motivo, string standId = null, string ecosystemName = null)
    { var p = NewCanonical<PresupuestoPayload>(); p.amount = amount; p.motivo = string.IsNullOrEmpty(motivo) ? "otro" : motivo; p.stand_id = standId ?? ""; p.ecosystem_name = ecosystemName ?? ""; PostEvent("presupuesto", p); }

    public void TrackTiempoEnPuntoExperiencia(string xpPointId, string ecosystemName, int seconds)
    { var p = NewCanonical<TiempoEnPuntoExperienciaPayload>(); p.xp_point_id = xpPointId ?? ""; p.ecosystem_name = ecosystemName ?? ""; p.duracion_segundos = Mathf.Max(0, seconds); PostEvent("tiempo_en_punto_experiencia", p); }

    public void TrackContenidoRevisadoXP(string xpPointId, string assetId)
    { var p = NewCanonical<ContenidoRevisadoXpPayload>(); p.xp_point_id = xpPointId ?? ""; p.asset_id = assetId ?? ""; PostEvent("contenido_revisado_xp", p); }

    // Retrocompat
    public void TrackMonedasObtenidas(int amount, string motivo, string standId = null, string ecosystemName = null)
        => TrackPresupuesto(amount, motivo, standId, ecosystemName);

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
        EnsureRunner().StartCoroutine(PostJson(json, name));
    }

    static bool BodyIndicaProblemaSesionOToken(string body)
    {
        if (string.IsNullOrEmpty(body)) return false;
        string b = body.ToLowerInvariant()
            .Replace("á", "a").Replace("é", "e").Replace("í", "i").Replace("ó", "o").Replace("ú", "u").Replace("ñ", "n");
        return (b.Contains("sesion") && (b.Contains("no encontrada") || b.Contains("no encontrado")))
               || b.Contains("token invalido") || b.Contains("no autorizado") || b.Contains("forbidden");
    }

    // ------------ SINGLE-FLIGHT RECOVERY UTILS ------------
    bool IsSessionInitJson(string json)
        => !string.IsNullOrEmpty(json) && json.IndexOf("\"name\":\"sesion_iniciada\"", StringComparison.OrdinalIgnoreCase) >= 0;

    IEnumerator PostJson_NoRecovery(string json, string reason)
    {
        var token = GetToken();
#if UNITY_WEBGL && !UNITY_EDITOR
        if (string.IsNullOrEmpty(token) && !allowSendWithoutToken)
        { if (verboseRecovery) Debug.Log($"[Metrics/NoRecovery] sin token → buffer: {reason}"); _preTokenBuffer.Add(json); yield break; }
#endif
        string url = baseUrl.TrimEnd('/') + metricsPath;
        var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
        {
            uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
            downloadHandler = new DownloadHandlerBuffer(),
            timeout = Mathf.CeilToInt(requestTimeout)
        };
        req.SetRequestHeader("Content-Type", "application/json");
        if (!string.IsNullOrEmpty(token)) req.SetRequestHeader("Authorization", "Bearer " + token);
        if (verboseRecovery) Debug.Log($"[Metrics/NoRecovery] POST {reason}");
        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success || req.responseCode >= 400)
        { if (verboseRecovery) Debug.LogWarning($"[Metrics/NoRecovery] {req.responseCode} :: {req.downloadHandler.text}"); }
        else if (verboseRecovery) Debug.Log($"[Metrics/NoRecovery] OK {reason}");
    }

    IEnumerator RecoverSessionAndRetry(string originalJson, string reason)
    {
        if (_sessionRecoveryInFlight)
        {
            if (verboseRecovery) Debug.Log("[Metrics] Recuperación ya en curso → bufferizo evento.");
            _preTokenBuffer.Add(originalJson);
            yield break;
        }

        _sessionRecoveryInFlight = true;
        if (verboseRecovery) Debug.Log("[Metrics] Iniciando recuperación de sesión…");

        ResetSessionId();

        // Enviar sesion_iniciada SIN recuperación para cortar bucles
        {
            var p = NewCanonical<SesionIniciadaPayload>();
            p.client = "unity_webgl";
            var env = new Envelope<SesionIniciadaPayload> { name = "sesion_iniciada", contenido = p };
            var jsonInit = JsonUtility.ToJson(env);
            yield return StartCoroutine(PostJson_NoRecovery(jsonInit, "sesion_iniciada (recovery)"));
        }

        // Reintento del evento original SIN reentrar
        yield return StartCoroutine(PostJson_NoRecovery(originalJson, reason));

        // Drenar buffer si acumuló
        if (_preTokenBuffer.Count > 0)
        {
            var copy = new List<string>(_preTokenBuffer);
            _preTokenBuffer.Clear();
            foreach (var j in copy)
                yield return StartCoroutine(PostJson_NoRecovery(j, "drain_after_recovery"));
        }

        _recoveryCooldownUntil = Time.unscaledTime + recoveryCooldownSeconds;
        _sessionRecoveryInFlight = false;
    }

    // ------------ POST PRINCIPAL ------------
    IEnumerator PostJson(string json, string reason)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (!allowSendWithoutToken)
            yield return TokenLifecycle.I?.WaitUntilTokenValid();
#endif
        // Cooldown: evita martillear si el back sigue rechazando
        if (Time.unscaledTime < _recoveryCooldownUntil && !IsSessionInitJson(json))
        {
            if (verboseRecovery) Debug.Log($"[Metrics] Cooldown activo → bufferizo: {reason}");
            _preTokenBuffer.Add(json);
            yield break;
        }

        int attempts = 0;
    Retry:
        attempts++;

        var token = GetToken();

#if UNITY_WEBGL && !UNITY_EDITOR
        if (string.IsNullOrEmpty(token) && !allowSendWithoutToken)
        { if (verboseLogs) Debug.Log($"[Metrics] Aún sin token. Bufferizando: {reason}"); _preTokenBuffer.Add(json); yield break; }
#endif

        string url = baseUrl.TrimEnd('/') + metricsPath;
        var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
        {
            uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
            downloadHandler = new DownloadHandlerBuffer(),
            timeout = Mathf.CeilToInt(requestTimeout)
        };
        req.SetRequestHeader("Content-Type", "application/json");
        if (!string.IsNullOrEmpty(token)) req.SetRequestHeader("Authorization", "Bearer " + token);

        if (verboseLogs)
        {
            var secs = JwtLite.SecondsToExpiry(token);
            Debug.Log($"[Metrics POST] {url} :: exp_en={secs}s :: {reason}\n{json}");
        }

        yield return req.SendWebRequest();

        bool ok = (req.result == UnityWebRequest.Result.Success) && (req.responseCode < 400);
        if (!ok)
        {
            long code = req.responseCode;
            string body = req.downloadHandler != null ? req.downloadHandler.text : "";
            bool authFail = (code == 401 || code == 403);

            // JAMÁS intentes recuperar si el propio evento es sesion_iniciada (corta bucles)
            if (IsSessionInitJson(json))
            {
                Debug.LogWarning($"[Metrics] {code} en sesion_iniciada :: {body} (no recovery)");
                yield break;
            }

            if (authFail && BodyIndicaProblemaSesionOToken(body) && attempts == 1)
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                WebGLBridge.I?.TryRescanStoragesNow();                     // por si el wrapper rotó el token
                yield return TokenLifecycle.I?.RequestRefreshAndWait();    // no-op, interfaz estable
#endif
                yield return StartCoroutine(RecoverSessionAndRetry(json, reason));
                yield break; // ya reintentamos sin reentrar
            }

            if (code >= 400 && code < 500)
                Debug.LogError($"[Metrics] {code} :: {body} (no reintenta)");
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
