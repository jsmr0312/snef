// ProgressCore.cs — Versión unificada (local PlayerPrefs + opcional remoto)
// - Auto-spawn en todas las escenas (BeforeSceneLoad)
// - Mantiene tu modelo GameProgressV1 y añade compat con lo nuevo
// - Guarda LOCAL en cada cambio (Editor) y puede PUT a API si lo deseas

using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;

[DefaultExecutionOrder(-10000)]
public class ProgressCore : MonoBehaviour
{
    public static ProgressCore I { get; private set; }

    // === API remoto (opcional) ===
    [Header("API (opcional)")]
    public string baseUrl = "https://api.estudiohera.mx";
    public string progressPath = "/game/progress"; // GET/PUT
    public string tokenPlayerPrefsKey = "snef_token";

    [Header("Opciones")]
    public bool saveLocalOnEachChange = true;       // guarda PlayerPrefs cada cambio
    public bool autoCreateRemoteIfMissing = true;   // si GET=404, hace PUT inicial
    public float requestTimeout = 8f;

    [Header("Dev / Pruebas")]
    public bool allowSendWithoutToken = true;       // para pruebas sin token
    public bool verboseLogs = true;

    // === Claves de almacenamiento local ===
    public const string STORAGE_KEY = "progress-storage"; // compat con bootstrap nuevo
    const string KEY_LOCAL = "progress_v1";               // tu clave histórica
    const string KEY_PENDING = "pending_save_v1";

    // ===== Modelo principal (el tuyo) =====
    [Serializable] public class Profile { public string avatar_id; }

    // Para evitar choque de nombre con la propiedad Progress, renombramos:
    [Serializable] public class Wallet { public int presupuesto; public int puntaje; }

    [Serializable]
    public class StandProgress
    {
        public string stand_id;                // ej. "eco1_banco_master"
        public string type;                    // master | premier | excellence | punto
        public string phase;                   // Initial | Waiting | PostScreens | Final
        public List<string> viewed_screens = new List<string>();
        public bool quiz_unlocked;
        public int quiz_best;
        public int quiz_last;
        public bool arcade_unlocked;
        public string updated_at;

        // NUEVO: tiempos e info de visita para métricas/progreso local
        public int time_spent_s = 0;
        public string last_visit_iso;
    }

    [Serializable] public class World { public string scene; public float x; public float y; public float z; }
    [Serializable] public class MissionEntry { public string id; public string state; public string updated_at; }
    [Serializable] public class AchievementEntry { public string id; public bool unlocked; public string updated_at; }
    [Serializable] public class MinigameEntry { public string id; public int best_score; public int stars; public string updated_at; }
    [Serializable] public class Meta { public string updated_at; public string app_version; }

    [Serializable]
    public class GameProgressV1
    {
        public int schema_version = 1;
        public Profile profile = new Profile();
        public Wallet progress = new Wallet();
        public List<string> owned_items = new List<string>();
        public List<StandProgress> stands = new List<StandProgress>();

        public World world = new World();
        public List<MissionEntry> missions = new List<MissionEntry>();
        public List<AchievementEntry> achievements = new List<AchievementEntry>();
        public List<MinigameEntry> minigames = new List<MinigameEntry>();
        public Meta meta = new Meta();
    }

    // ====== Estado en memoria ======
    public GameProgressV1 Data { get; private set; } = new GameProgressV1();

    // --------- Facade de compatibilidad "Progress" ---------
    // Permite que scripts usen: ProgressCore.I.Progress.presupuesto / .stands / .owned_items / .profile
    public class ProgressFacade
    {
        readonly GameProgressV1 _d;
        public ProgressFacade(GameProgressV1 d) { _d = d; }
        public int presupuesto { get => _d.progress.presupuesto; set => _d.progress.presupuesto = Mathf.Max(0, value); }
        public int puntaje { get => _d.progress.puntaje; set => _d.progress.puntaje = Mathf.Max(0, value); }
        public List<string> owned_items => _d.owned_items;
        public List<StandProgress> stands => _d.stands;
        public Profile profile => _d.profile;
    }
    public ProgressFacade Progress => new ProgressFacade(Data);

    // --------- Eventos ---------
    public event Action<GameProgressV1> OnChanged;

    // ===== Ciclo de vida =====
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void EnsureSingleton()
    {
        if (I == null)
        {
            var go = new GameObject("ProgressCore");
            go.AddComponent<ProgressCore>();
        }
    }

    void Awake()
    {
        if (I != null) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
        LoadLocalOrInit();
    }

    // ===== API pública: Perfil / Wallet / Items =====
    public void SetAvatar(string avatarId) { Data.profile.avatar_id = avatarId; Touch(); }
    public string GetAvatarId() => Data.profile.avatar_id ?? "";

    public void AddPresupuesto(int delta) { Data.progress.presupuesto = Mathf.Max(0, Data.progress.presupuesto + delta); Touch(); }
    public void SetPresupuesto(int value) { Data.progress.presupuesto = Mathf.Max(0, value); Touch(); }
    public void AddPuntaje(int delta) { Data.progress.puntaje = Mathf.Max(0, Data.progress.puntaje + delta); Touch(); }
    public void SetPuntaje(int value) { Data.progress.puntaje = Mathf.Max(0, value); Touch(); }

    public bool IsOwned(string itemId) => !string.IsNullOrEmpty(itemId) && Data.owned_items.Contains(itemId);
    public bool OwnItem(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return false;
        if (Data.owned_items.Contains(itemId)) return false;
        Data.owned_items.Add(itemId); Touch(); return true;
    }

    // ===== API pública: Misiones / Logros / Minijuegos (igual que tu archivo) =====
    public void UpsertMission(string id, string state)
    {
        var now = DateTime.UtcNow.ToString("o");
        var e = Data.missions.Find(m => m.id == id);
        if (e == null) { e = new MissionEntry { id = id }; Data.missions.Add(e); }
        e.state = state; e.updated_at = now; Touch();
    }

    public void UpsertAchievement(string id, bool unlocked)
    {
        var now = DateTime.UtcNow.ToString("o");
        var e = Data.achievements.Find(a => a.id == id);
        if (e == null) { e = new AchievementEntry { id = id }; Data.achievements.Add(e); }
        e.unlocked = unlocked; e.updated_at = now; Touch();
    }

    // === Helpers de clave compuesta (stand::minigame) ===
    public static string MinigameKey(string standId, string minigameId)
    {
        if (string.IsNullOrEmpty(standId)) return minigameId ?? "minigame";
        if (string.IsNullOrEmpty(minigameId)) return standId + "::minigame";
        return standId + "::" + minigameId;
    }

    public void UpsertMinigameScoped(string standId, string minigameId, int bestScore, int stars)
    {
        var key = MinigameKey(standId, minigameId);
        UpsertMinigame(key, bestScore, stars); // usa tu método existente
    }


    public void UpsertMinigame(string id, int bestScore, int stars)
    {
        var now = DateTime.UtcNow.ToString("o");
        var e = Data.minigames.Find(m => m.id == id);
        if (e == null) { e = new MinigameEntry { id = id }; Data.minigames.Add(e); }
        e.best_score = Mathf.Max(e.best_score, bestScore);
        e.stars = Mathf.Max(e.stars, stars);
        e.updated_at = now; Touch();
    }

    // ===== STANDS: CRUD + compat =====
    StandProgress EnsureStand(string standId, string type = null)
    {
        if (string.IsNullOrEmpty(standId)) return null;
        var s = Data.stands.Find(x => x.stand_id == standId);
        if (s == null)
        {
            s = new StandProgress { stand_id = standId, type = type ?? "" };
            Data.stands.Add(s);
            Touch();
        }
        else if (!string.IsNullOrEmpty(type) && string.IsNullOrEmpty(s.type))
        {
            s.type = type;
        }
        return s;
    }

    // — Versión antigua (tuya)
    public void Stand_SetPhase(string standId, string phase, string type = null)
    {
        var s = EnsureStand(standId, type); if (s == null) return;
        s.phase = phase; s.updated_at = DateTime.UtcNow.ToString("o"); Touch();
    }

    // — Compat nueva: (standType, newPhase)
    public void Stand_SetPhase(string standId, string standType, string newPhase, bool onlyForward = false)
    {
        var s = EnsureStand(standId, standType); if (s == null) return;
        if (onlyForward)
        {
            int Rank(string p) => p == "Initial" ? 0 : p == "Waiting" ? 1 : p == "PostScreens" ? 2 : 3;
            if (Rank(newPhase) <= Rank(s.phase ?? "Initial")) return;
        }
        s.phase = newPhase; s.updated_at = DateTime.UtcNow.ToString("o"); Touch();
    }

    public void Stand_MarkScreenViewed(string standId, string screenId, string type = null)
    {
        var s = EnsureStand(standId, type); if (s == null || string.IsNullOrEmpty(screenId)) return;
        if (!s.viewed_screens.Contains(screenId)) s.viewed_screens.Add(screenId);
        s.updated_at = DateTime.UtcNow.ToString("o"); Touch();
    }

    // — Compat nueva:
    public void Stand_AddViewedScreen(string standId, string assetId) => Stand_MarkScreenViewed(standId, assetId);

    public void Stand_UnlockQuiz(string standId) { var s = EnsureStand(standId); if (s == null) return; s.quiz_unlocked = true; s.updated_at = DateTime.UtcNow.ToString("o"); Touch(); }
    public void Stand_RecordQuiz(string standId, int score, int total)
    {
        var s = EnsureStand(standId); if (s == null) return;
        s.quiz_last = Mathf.Max(0, score); if (score > s.quiz_best) s.quiz_best = score;
        s.updated_at = DateTime.UtcNow.ToString("o"); Touch();
    }

    public void Stand_UnlockArcade(string standId) { var s = EnsureStand(standId); if (s == null) return; if (!s.arcade_unlocked) { s.arcade_unlocked = true; s.updated_at = DateTime.UtcNow.ToString("o"); Touch(); } }
    public void Stand_LockArcade(string standId) { var s = EnsureStand(standId); if (s == null) return; if (s.arcade_unlocked) { s.arcade_unlocked = false; s.updated_at = DateTime.UtcNow.ToString("o"); Touch(); } }

    // — Compat nueva: tiempos/visita
    public void Stand_AddTime(string standId, int seconds) { var s = EnsureStand(standId); if (s == null) return; s.time_spent_s += Mathf.Max(0, seconds); s.updated_at = DateTime.UtcNow.ToString("o"); Touch(); }
    public void Stand_SetLastVisitNow(string standId) { var s = EnsureStand(standId); if (s == null) return; s.last_visit_iso = DateTime.UtcNow.ToString("o"); s.updated_at = s.last_visit_iso; Touch(); }

    // Consultas
    public bool Stand_IsArcadeUnlocked(string standId) => Data.stands.Find(x => x.stand_id == standId)?.arcade_unlocked == true;
    public bool Stand_IsQuizUnlocked(string standId) => Data.stands.Find(x => x.stand_id == standId)?.quiz_unlocked == true;
    public string Stand_GetPhase(string standId) => Data.stands.Find(x => x.stand_id == standId)?.phase;

    // ===== Guardado / Carga =====
    public void SaveNow(string reason = "")
    {
        // Siempre guardamos LOCAL de inmediato (para Editor y WebGL sin token)
        Touch(); // asegura meta.updated/app_version y SaveLocal()
        // PUT remoto opcional
        StartCoroutine(PutAllCoroutine(reason));
    }

    public IEnumerator SaveNowRoutine(string reason = "") { Touch(); yield return PutAllCoroutine(reason); }
    public void FetchFromServer() { StartCoroutine(GetAndMergeCoroutine()); }

    void Touch()
    {
        Data.meta.updated_at = DateTime.UtcNow.ToString("o");
        if (string.IsNullOrEmpty(Data.meta.app_version)) Data.meta.app_version = Application.version;
        if (saveLocalOnEachChange) SaveLocal();
        OnChanged?.Invoke(Data);
    }

    void LoadLocalOrInit()
    {
        // 1) Intenta tu clave histórica
        var json = PlayerPrefs.GetString(KEY_LOCAL, "");
        // 2) Si no hay, intenta la clave de bootstrap nueva (para compat)
        if (string.IsNullOrEmpty(json)) json = PlayerPrefs.GetString(STORAGE_KEY, "");

        if (string.IsNullOrEmpty(json))
        {
            Data = new GameProgressV1();
            Data.meta.app_version = Application.version;
            SaveLocal();
        }
        else
        {
            try { Data = JsonUtility.FromJson<GameProgressV1>(json) ?? new GameProgressV1(); }
            catch { Data = new GameProgressV1(); }
        }
    }

    void SaveLocal()
    {
        var json = JsonUtility.ToJson(Data);
        PlayerPrefs.SetString(KEY_LOCAL, json);         // tu clave histórica
        PlayerPrefs.SetString(STORAGE_KEY, json);       // compat con nuevo bootstrap local
        PlayerPrefs.Save();
        if (verboseLogs) Debug.Log("[ProgressCore] SaveLocal: " + json);
    }

    // ====== Bootstrap desde JSON estilo { state:{ progress:{...}, stands:[...] } } ======
    [Serializable] class BootstrapWrapper { public BootstrapState state; public int version; }
    [Serializable] class BootstrapState { public BootstrapProgress progress; }
    [Serializable]
    class BootstrapProgress
    {
        public Profile profile;
        public int puntaje;
        public int presupuesto;
        public List<string> owned_items;
        public List<string> store_items;
        public List<StandProgress> stands;
    }

    public void LoadFromBootstrapJson(string json)
    {
        try
        {
            var b = JsonUtility.FromJson<BootstrapWrapper>(json);
            if (b?.state?.progress != null)
            {
                // adopta campos relevantes
                Data.profile = b.state.progress.profile ?? new Profile();
                Data.progress.puntaje = b.state.progress.puntaje;
                Data.progress.presupuesto = b.state.progress.presupuesto;
                Data.owned_items = b.state.progress.owned_items ?? new List<string>();
                Data.stands = b.state.progress.stands ?? new List<StandProgress>();
                Touch(); // guarda local
                if (verboseLogs) Debug.Log("[ProgressCore] Bootstrap (envoltura) cargado.");
                return;
            }
        }
        catch { /* cae al intento plano */ }

        // Si no es el envoltorio, intenta directamente GameProgressV1
        try
        {
            var direct = JsonUtility.FromJson<GameProgressV1>(json);
            if (direct != null) { Data = direct; Touch(); if (verboseLogs) Debug.Log("[ProgressCore] Bootstrap (v1 directo) cargado."); }
        }
        catch { Debug.LogWarning("[ProgressCore] Bootstrap JSON inválido."); }
    }

    // ====== Red (igual a tu archivo, con mejoras menores) ======
    IEnumerator GetAndMergeCoroutine()
    {
        string token = PlayerPrefs.GetString(tokenPlayerPrefsKey, "");
        if (string.IsNullOrEmpty(token))
        {
            if (verboseLogs) Debug.Log("[ProgressCore] GET cancelado: no hay token.");
            yield break;
        }

        string url = baseUrl.TrimEnd('/') + progressPath;
        using (var req = UnityWebRequest.Get(url))
        {
            req.SetRequestHeader("Authorization", "Bearer " + token);
            req.timeout = Mathf.CeilToInt(requestTimeout);
            if (verboseLogs) Debug.Log($"[GET] {url}");
            yield return req.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
            bool ok = (req.result == UnityWebRequest.Result.Success);
#else
            bool ok = (!req.isNetworkError && !req.isHttpError);
#endif
            if (ok && !string.IsNullOrEmpty(req.downloadHandler.text))
            {
                var remoteJson = req.downloadHandler.text;
                GameProgressV1 remote = null;
                try { remote = JsonUtility.FromJson<GameProgressV1>(remoteJson); } catch { remote = null; }
                if (remote != null)
                {
                    if (IsRemoteNewer(remote.meta?.updated_at, Data.meta?.updated_at))
                    {
                        Data = remote; SaveLocal(); OnChanged?.Invoke(Data);
                        if (verboseLogs) Debug.Log("[ProgressCore] GET OK → adoptado remoto.");
                    }
                    else if (verboseLogs) Debug.Log("[ProgressCore] GET OK → mantengo local (más reciente).");
                }
            }
            else if ((int)req.responseCode == 404 && autoCreateRemoteIfMissing)
            {
                if (verboseLogs) Debug.Log("[ProgressCore] Sin progreso remoto; creando con PUT inicial.");
                SaveNow("bootstrap_first_put");
            }
            else
            {
                Debug.LogWarning("[ProgressCore] GET falló: " + req.error + " code=" + req.responseCode);
            }
        }
    }

    IEnumerator PutAllCoroutine(string reason)
    {
        string token = PlayerPrefs.GetString(tokenPlayerPrefsKey, "");
        bool sendAuth = !string.IsNullOrEmpty(token);

        if (!sendAuth && !allowSendWithoutToken)
        {
            if (verboseLogs) Debug.LogWarning("[ProgressCore] PUT cancelado: no hay token y allowSendWithoutToken=false.");
            yield break;
        }

        // Reintenta pendiente primero
        var pending = PlayerPrefs.GetString(KEY_PENDING, "");
        if (!string.IsNullOrEmpty(pending))
        {
            yield return StartCoroutine(PutRawJson(pending, token, sendAuth, "retry_pending"));
        }

        var json = JsonUtility.ToJson(Data);
        yield return StartCoroutine(PutRawJson(json, token, sendAuth, reason));
    }

    IEnumerator PutRawJson(string json, string token, bool sendAuth, string reason)
    {
        string url = baseUrl.TrimEnd('/') + progressPath;
        byte[] body = Encoding.UTF8.GetBytes(json);

        using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPUT))
        {
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            if (sendAuth) req.SetRequestHeader("Authorization", "Bearer " + token);
            req.timeout = Mathf.CeilToInt(requestTimeout);

            if (verboseLogs) Debug.Log($"[PUT] {url}\n{json}");
            yield return req.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
            bool ok = (req.result == UnityWebRequest.Result.Success);
#else
            bool ok = (!req.isNetworkError && !req.isHttpError);
#endif
            if (verboseLogs) Debug.Log($"[PUT RESP] code={req.responseCode} body={req.downloadHandler.text} error={req.error}");

            if (ok)
            {
                PlayerPrefs.SetString(KEY_LOCAL, json);
                PlayerPrefs.SetString(STORAGE_KEY, json);
                PlayerPrefs.DeleteKey(KEY_PENDING);
                PlayerPrefs.Save();
                if (verboseLogs) Debug.Log($"[ProgressCore] PUT OK ({reason})");
            }
            else
            {
                Debug.LogWarning($"[ProgressCore] PUT falló ({reason}): {req.error} code={req.responseCode}");
                PlayerPrefs.SetString(KEY_PENDING, json);
                PlayerPrefs.Save();
            }
        }
    }

    bool IsRemoteNewer(string remoteIso, string localIso)
    {
        if (string.IsNullOrEmpty(remoteIso)) return false;
        if (string.IsNullOrEmpty(localIso)) return true;
        if (DateTime.TryParse(remoteIso, null, DateTimeStyles.RoundtripKind, out var r) &&
            DateTime.TryParse(localIso, null, DateTimeStyles.RoundtripKind, out var l))
            return r > l;
        return false;
    }


}
