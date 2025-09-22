using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;

/// Attach this to a GameObject named "ProgressCore" in your first scene.
/// It will persist across scenes (DontDestroyOnLoad).
public class ProgressCore : MonoBehaviour
{
    public static ProgressCore I { get; private set; }

    [Header("API")]
    public string baseUrl = "https://api.estudiohera.mx";
    public string progressPath = "/game/progress"; // GET/PUT
    public string tokenPlayerPrefsKey = "snef_token";

    [Header("Opciones")]
    public bool saveLocalOnEachChange = true; // guarda "progress_v1" local cada cambio
    public bool autoCreateRemoteIfMissing = true; // si GET=404, hace PUT para crearlo
    public float requestTimeout = 8f;

    [Header("Dev / Pruebas")]
    [Tooltip("Permite enviar PUT sin token (útil para webhook.site). Si hay token, se envía Authorization igualmente.")]
    public bool allowSendWithoutToken = true;
    [Tooltip("Escribe logs del request/response en la consola.")]
    public bool verboseLogs = true;

    // === MODELO (tu "archivo único" JSON v1) ===
    [Serializable]
    public class GameProgressV1
    {
        public int schema_version = 1;
        public Profile profile = new Profile();
        public Progress progress = new Progress();
        public List<string> owned_items = new List<string>();
        public List<StandProgress> stands = new List<StandProgress>();


        public World world = new World();
        public List<MissionEntry> missions = new List<MissionEntry>();
        public List<AchievementEntry> achievements = new List<AchievementEntry>();
        public List<MinigameEntry> minigames = new List<MinigameEntry>();
        public Meta meta = new Meta();
    }
    [Serializable] public class Profile { public string avatar_id; }
    [Serializable] public class Progress { public int presupuesto; public int puntaje; }

    [Serializable]
    public class StandProgress
    {
        public string stand_id;                // slug único del stand (ej. "eco1_banco_master")
        public string type;                    // "master" | "premier" | "excellence" | "punto"
        public string phase;                   // "Initial" | "Waiting" | "PostScreens" | "Final"
        public List<string> viewed_screens = new List<string>();  // ids de pantallas vistas ("screen1"...)
        public bool quiz_unlocked;             // tras ver todas las pantallas
        public int quiz_best;                  // mejor puntaje (0..N)
        public int quiz_last;                  // último puntaje
        public bool arcade_unlocked;           // para MASTER
        public string updated_at;              // iso
    }

    [Serializable] public class World { public string scene; public float x; public float y; public float z; }
    [Serializable] public class MissionEntry { public string id; public string state; public string updated_at; }
    [Serializable] public class AchievementEntry { public string id; public bool unlocked; public string updated_at; }
    [Serializable] public class MinigameEntry { public string id; public int best_score; public int stars; public string updated_at; }
    [Serializable] public class Meta { public string updated_at; public string app_version; }

    public GameProgressV1 Data { get; private set; } = new GameProgressV1();

    const string KEY_LOCAL = "progress_v1";
    const string KEY_PENDING = "pending_save_v1";

    public event Action<GameProgressV1> OnChanged;

    void Awake()
    {
        if (I != null) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
        LoadLocalOrInit();
    }

    // ------------------ API PÚBLICA (tú llamas esto) ------------------

    public void SetAvatar(string avatarId)
    {
        Data.profile.avatar_id = avatarId;
        Touch();
    }

    public void AddPresupuesto(int delta) { Data.progress.presupuesto = Mathf.Max(0, Data.progress.presupuesto + delta); Touch(); }
    public void SetPresupuesto(int value) { Data.progress.presupuesto = Mathf.Max(0, value); Touch(); }
    public void AddPuntaje(int delta) { Data.progress.puntaje = Mathf.Max(0, Data.progress.puntaje + delta); Touch(); }
    public void SetPuntaje(int value) { Data.progress.puntaje = Mathf.Max(0, value); Touch(); }

    public void SetPlayerPos(Transform t, string sceneName)
    {
        if (t != null)
        {
            Data.world.scene = sceneName;
            Data.world.x = t.position.x;
            Data.world.y = t.position.y;
            Data.world.z = t.position.z;
            Touch();
        }
    }

    public bool IsOwned(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return false;
        return Data.owned_items.Contains(itemId);
    }

    public bool OwnItem(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return false;
        if (Data.owned_items.Contains(itemId)) return false; // ya se tenía
        Data.owned_items.Add(itemId);
        Touch(); // marca updated_at y guarda local si corresponde
        return true;
    }


    public void UpsertMission(string id, string state)
    {
        var now = DateTime.UtcNow.ToString("o");
        var e = Data.missions.Find(m => m.id == id);
        if (e == null) { e = new MissionEntry { id = id }; Data.missions.Add(e); }
        e.state = state; e.updated_at = now;
        Touch();
    }

    public void UpsertAchievement(string id, bool unlocked)
    {
        var now = DateTime.UtcNow.ToString("o");
        var e = Data.achievements.Find(a => a.id == id);
        if (e == null) { e = new AchievementEntry { id = id }; Data.achievements.Add(e); }
        e.unlocked = unlocked; e.updated_at = now;
        Touch();
    }

    public void UpsertMinigame(string id, int bestScore, int stars)
    {
        var now = DateTime.UtcNow.ToString("o");
        var e = Data.minigames.Find(m => m.id == id);
        if (e == null) { e = new MinigameEntry { id = id }; Data.minigames.Add(e); }
        e.best_score = Mathf.Max(e.best_score, bestScore);
        e.stars = Mathf.Max(e.stars, stars);
        e.updated_at = now;
        Touch();
    }

    // ---------- STANDS: CRUD básico en memoria ----------
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

    public void Stand_SetPhase(string standId, string phase, string type = null)
    {
        var s = EnsureStand(standId, type);
        if (s == null) return;
        s.phase = phase;
        s.updated_at = DateTime.UtcNow.ToString("o");
        Touch();
    }

    public void Stand_MarkScreenViewed(string standId, string screenId, string type = null)
    {
        var s = EnsureStand(standId, type);
        if (s == null || string.IsNullOrEmpty(screenId)) return;
        if (!s.viewed_screens.Contains(screenId))
            s.viewed_screens.Add(screenId);
        s.updated_at = DateTime.UtcNow.ToString("o");
        Touch();
    }

    public void Stand_UnlockQuiz(string standId)
    {
        var s = EnsureStand(standId, null);
        if (s == null) return;
        s.quiz_unlocked = true;
        s.updated_at = DateTime.UtcNow.ToString("o");
        Touch();
    }

    public void Stand_RecordQuiz(string standId, int score, int total)
    {
        var s = EnsureStand(standId, null);
        if (s == null) return;
        s.quiz_last = Mathf.Max(0, score);
        if (score > s.quiz_best) s.quiz_best = score;
        // si quieres marcar "Final" al pasar, puedes hacerlo aquí o desde el NPC
        s.updated_at = DateTime.UtcNow.ToString("o");
        Touch();
    }

    public void Stand_UnlockArcade(string standId)
    {
        var s = EnsureStand(standId, null);
        if (s == null) return;
        s.arcade_unlocked = true;
        s.updated_at = DateTime.UtcNow.ToString("o");
        Touch();
    }

    // ---------- Consultas de estado (para hidratar al entrar a escena) ----------
    public bool Stand_IsArcadeUnlocked(string standId)
    {
        var s = Data.stands.Find(x => x.stand_id == standId);
        return s != null && s.arcade_unlocked;
    }
    public bool Stand_IsQuizUnlocked(string standId)
    {
        var s = Data.stands.Find(x => x.stand_id == standId);
        return s != null && s.quiz_unlocked;
    }
    public string Stand_GetPhase(string standId)
    {
        return Data.stands.Find(x => x.stand_id == standId)?.phase;
    }


    /// Llama esto desde tu botón/trigger/menú para ENVIAR el JSON COMPLETO al API.
    public void SaveNow(string reason = "")
    {
        Touch(); // asegura meta.updated_at/app_version
        StartCoroutine(PutAllCoroutine(reason));
    }

    /// Igual que arriba, pero puedes ESPERAR a que termine (para spinners/bloquear UI).
    public IEnumerator SaveNowRoutine(string reason = "")
    {
        Touch(); // asegura meta.actualizada
        yield return PutAllCoroutine(reason);
    }

    /// Llama esto tras login (cuando seguro ya hay token) para CARGAR del API.
    public void FetchFromServer()
    {
        StartCoroutine(GetAndMergeCoroutine());
    }

    // ------------------ Internos: Local & Red ------------------

    void Touch()
    {
        Data.meta.updated_at = DateTime.UtcNow.ToString("o");
        if (string.IsNullOrEmpty(Data.meta.app_version)) Data.meta.app_version = Application.version;
        if (saveLocalOnEachChange) SaveLocal();
        OnChanged?.Invoke(Data);
    }

    void LoadLocalOrInit()
    {
        var json = PlayerPrefs.GetString(KEY_LOCAL, "");
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
        PlayerPrefs.SetString(KEY_LOCAL, JsonUtility.ToJson(Data));
        PlayerPrefs.Save();
    }

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
                        Data = remote;
                        SaveLocal();
                        OnChanged?.Invoke(Data);
                        if (verboseLogs) Debug.Log("[ProgressCore] GET OK → adoptado remoto.");
                    }
                    else
                    {
                        if (verboseLogs) Debug.Log("[ProgressCore] GET OK → mantengo local (es más reciente).");
                    }
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
            Debug.LogWarning("[ProgressCore] PUT cancelado: no hay token y allowSendWithoutToken=false.");
            yield break;
        }

        // Si quedó un pendiente de antes, intenta primero
        var pending = PlayerPrefs.GetString(KEY_PENDING, "");
        if (!string.IsNullOrEmpty(pending))
        {
            yield return StartCoroutine(PutRawJson(pending, token, sendAuth, "retry_pending"));
            // si sale bien, PutRawJson borrará el pending
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
                PlayerPrefs.DeleteKey(KEY_PENDING);
                PlayerPrefs.Save();
                if (verboseLogs) Debug.Log($"[ProgressCore] PUT OK ({reason})");
            }
            else
            {
                Debug.LogWarning($"[ProgressCore] PUT falló ({reason}): {req.error} code={req.responseCode}");
                // guarda pendiente para reintentar en el próximo SaveNow
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
