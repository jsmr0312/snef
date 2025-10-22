using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

/// Lee localStorage["progress-storage"] (inyectado por tu host React)
/// y aplica/reset el progreso al arrancar.
[DefaultExecutionOrder(-8000)]
public class ProgressBootstrapper : MonoBehaviour
{
    [Header("Clave en localStorage")]
    public string progressKey = "progress-storage";

    [Header("Reset invitado")]
    [Tooltip("Si el token indica invitado, resetea el progreso aunque exista token.")]
    public bool resetOnGuestToken = true;

    [Tooltip("Borrar TODOS los PlayerPrefs al resetear invitado (más agresivo).")]
    public bool hardDeleteAllPrefsOnGuest = false;

    [Header("Opcional")]
    [Tooltip("Si está activo y existe la función JS, limpiará la clave después de leerla.")]
    public bool clearKeyAfterRead = false;

    [Header("Sincronía con Stats (evitar duplicados)")]
    [Tooltip("Déjalo en FALSE si ya usas StatsProgressSync en la escena (recomendado).")]
    public bool syncStatsHere = false;

    [Header("Esperar progreso del host")]
    [Tooltip("Tiempo máximo a esperar a que el host escriba progress-storage (primer ingreso).")]
    public float waitForProgressSeconds = 1.5f;

    [Tooltip("Intervalo de sondeo para detectar que ya apareció la clave.")]
    public float pollIntervalSeconds = 0.1f;

    [Header("Logs")]
    public bool log = true;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern string __GetLocalStorageItem(string key);
#if SNEF_LS_REMOVE
    [DllImport("__Internal")] private static extern void __RemoveLocalStorageItem(string key);
#endif
    [DllImport("__Internal")] private static extern void __SubscribeProgressMessages();
#endif

    void Awake()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try { __SubscribeProgressMessages(); } catch {}
#endif
    }

    void Start()
    {
        StartCoroutine(BootstrapRoutine());
    }

    /// <summary>
    /// Recibe progreso por postMessage/bridge (string json), lo aplica una sola vez
    /// y recalcula misiones/logros asegurando aislamiento por usuario.
    /// </summary>
    public void ReceiveBootstrapJson(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            if (log) Debug.LogWarning("[Bootstrap] JSON vacío recibido por postMessage.");
            return;
        }

        // Si venía entrecomillado, desescapar
        json = UnwrapIfQuoted(json);

        // Detectar límite de usuario ANTES de aplicar el progreso
        bool boundaryChanged = UserBoundaryChanged();
        if (boundaryChanged)
        {
            // Limpiar estados locales dependientes de usuario
            MissionManager.I?.ResetCurrentUserMissions();
            AchievementsManager.I?.ResetCurrentUserAchievements();
        }

        // Aplicar el progreso remoto UNA SOLA VEZ
        ProgressCore.I?.LoadFromBootstrapJson(json);

        // Opcional: reflejar HUD/Stats del ProgressCore al inicio
        if (syncStatsHere) MirrorStatsFromProgress();

        // Rehidratación determinista (misiones + logros)
        MissionManager.I?.RecomputeFromProgressFromProgressCore(mergeWithLocal: !boundaryChanged);
        AchievementsManager.I?.RecheckFromGameState();

        if (log) Debug.Log("[Bootstrap] Progreso recibido por postMessage y aplicado.");

        StartCoroutine(EnsureMissionRecomputeAfterManagers());
    }

    // ProgressBootstrapper.cs - al final de ReceiveBootstrapJson y también al final de BootstrapRoutine


    private IEnumerator EnsureMissionRecomputeAfterManagers()
    {
        // Espera a que el MissionManager esté presente (si ya está, pasa de inmediato)
        float waited = 0f;
        while (MissionManager.I == null && waited < 1.5f)
        {
            yield return null;
            waited += Time.deltaTime;
        }

        // Recomputa otra vez por seguridad (no afecta métricas)
        MissionManager.I?.RecomputeFromProgressFromProgressCore(mergeWithLocal: true);
        AchievementsManager.I?.RecheckFromGameState();
    }


    /// <summary>
    /// Rutina de arranque: lee localStorage, espera opcionalmente, aplica progreso,
    /// limpia clave si procede, y recalcula estados derivados.
    /// </summary>
    private IEnumerator BootstrapRoutine()
    {
        if (log) Debug.Log("[Bootstrap] Inicio de rutina de bootstrap…");

        // Reset duro si es invitado (opcional)
        if (resetOnGuestToken && IsGuestToken(WebGLBridge.Token))
        {
            if (log) Debug.Log("[Bootstrap] Token invitado detectado. Reseteando…");
            GuestHardReset();
            yield break;
        }

        // Esperar dependencias mínimas
        yield return new WaitUntil(() => ProgressCore.I != null);
        // Stats / managers pueden inicializar un frame después en algunos casos
        yield return null;

        // Leer el raw del host/localStorage
        string raw = ReadLocal(progressKey);

        // Espera opcional (primer ingreso) si no está aún
        if (string.IsNullOrEmpty(raw) && waitForProgressSeconds > 0f)
        {
            float t = 0f;
            while (t < waitForProgressSeconds)
            {
                yield return new WaitForSeconds(pollIntervalSeconds);
                raw = ReadLocal(progressKey);
                if (!string.IsNullOrEmpty(raw)) break;
                t += pollIntervalSeconds;
            }
        }

        if (string.IsNullOrEmpty(raw))
        {
            if (log) Debug.Log("[Bootstrap] No se encontró bootstrap en localStorage. Fin de rutina.");
            yield break;
        }

        // ======= Sí hay bootstrap => aplicarlo =======
        string json = UnwrapIfQuoted(raw);

        // Detectar límite de usuario ANTES de aplicar el progreso
        bool boundaryChanged = UserBoundaryChanged();
        if (boundaryChanged)
        {
            // Limpiar estados locales dependientes de usuario
            MissionManager.I?.ResetCurrentUserMissions();
            AchievementsManager.I?.ResetCurrentUserAchievements();
        }

        // Aplicar el progreso remoto UNA SOLA VEZ
        ProgressCore.I?.LoadFromBootstrapJson(json);

        // Limpiar el key si así está configurado
#if UNITY_WEBGL && !UNITY_EDITOR && SNEF_LS_REMOVE
        if (clearKeyAfterRead) RemoveLocal(progressKey);
#endif

        // Reflejar Stats del ProgressCore al HUD si se desea
        if (syncStatsHere) MirrorStatsFromProgress();

        // Rehidratación determinista (misiones + logros)
        MissionManager.I?.RecomputeFromProgressFromProgressCore(mergeWithLocal: !boundaryChanged);
        AchievementsManager.I?.RecheckFromGameState();

        if (log) Debug.Log("[Bootstrap] Progreso inicial aplicado y derivaciones recalculadas.");
    }

    // ---------------- helpers ----------------

    static string CurrentUid()
    {
        string uid = JwtLite.GetUserId(WebGLBridge.Token);
        return string.IsNullOrEmpty(uid) ? "guest" : uid;
    }

    /// <summary>
    /// Devuelve true si cambió el límite de usuario (uid) respecto al último usado.
    /// Guarda el uid actual en PlayerPrefs para próximas comparaciones.
    /// </summary>
    static bool UserBoundaryChanged()
    {
        const string KEY = "SNEF_LAST_UID";
        string uid = CurrentUid();
        string last = PlayerPrefs.GetString(KEY, "");
        bool changed = !string.Equals(uid, last, StringComparison.Ordinal);
        if (changed)
        {
            PlayerPrefs.SetString(KEY, uid);
            PlayerPrefs.Save();
        }
        return changed;
    }

    void GuestHardReset()
    {
        if (hardDeleteAllPrefsOnGuest)
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
        }
        else
        {
            PlayerPrefs.DeleteKey("personaje1Select");
            PlayerPrefs.DeleteKey("personaje2Select");
            PlayerPrefs.DeleteKey("personaje3Select");
            PlayerPrefs.DeleteKey("SNEF_MISSIONS_V1::guest");
            PlayerPrefs.DeleteKey("SNEF_ACHIEVEMENTS_V1::guest");
            PlayerPrefs.Save();
        }

        if (AchievementsManager.I != null)
            AchievementsManager.I.ResetCurrentUserAchievements();

        ProgressCore.I?.ResetLocalProgress("guest mode");
        Stats.I?.SetTotalsSilently(0, 0);

        // En reset sí queremos recomputar sin fusionar (estado limpio)
        MissionManager.I?.RecomputeFromProgressFromProgressCore(mergeWithLocal: false);
        AchievementsManager.I?.RecheckFromGameState();
    }

    string ReadLocal(string key)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try { return __GetLocalStorageItem(key); } catch { return ""; }
#else
        // En Editor/Standalone no hay localStorage; si lo necesitas, emúlalo con PlayerPrefs aquí.
        return "";
#endif
    }

    void RemoveLocal(string key)
    {
#if UNITY_WEBGL && !UNITY_EDITOR && SNEF_LS_REMOVE
        try { __RemoveLocalStorageItem(key); } catch {}
#endif
    }

    static string UnwrapIfQuoted(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        s = s.Trim();
        if (s.Length >= 2 && s[0] == '\"' && s[s.Length - 1] == '\"')
        {
            s = s.Substring(1, s.Length - 2);
            s = Regex.Unescape(s);
        }
        return s;
    }

    static bool IsGuestToken(string tok)
    {
        if (string.IsNullOrEmpty(tok)) return false;
        try
        {
            var parts = tok.Split('.');
            if (parts.Length < 2) return false;
            string payloadB64 = parts[1].Replace('-', '+').Replace('_', '/');
            switch (payloadB64.Length % 4)
            {
                case 2: payloadB64 += "=="; break;
                case 3: payloadB64 += "="; break;
                case 1: payloadB64 += "==="; break;
            }
            string json = Encoding.UTF8.GetString(Convert.FromBase64String(payloadB64)).ToLowerInvariant();

            if (json.Contains("\"is_anon\":true")) return true;
            if (json.Contains("\"isanonymous\":true")) return true;
            if (json.Contains("\"anonymous\":true")) return true;
            if (json.Contains("\"guest\":true")) return true;
            if (json.Contains("\"mode\":\"guest\"")) return true;
            if (json.Contains("\"role\":\"guest\"")) return true;
            if (json.Contains("\"user_id\":null")) return true;
            if (json.Contains("\"user\":\"guest\"")) return true;
            if (json.Contains("\"sub\":\"guest\"")) return true;

            return false;
        }
        catch { return false; }
    }

    // Solo usar si NO tienes StatsProgressSync en escena.
    void MirrorStatsFromProgress()
    {
        if (ProgressCore.I == null || Stats.I == null) return;
        var d = ProgressCore.I.Data.progress;
        int deltaBudget = Mathf.Max(0, d.presupuesto) - Stats.I.Presupuesto;
        int deltaScore = Mathf.Max(0, d.puntaje) - Stats.I.Puntaje;
        if (deltaBudget != 0) Stats.I.AddPresupuesto(deltaBudget);
        if (deltaScore != 0) Stats.I.AddPuntaje(deltaScore);
        Stats.I.ForceRefresh();
    }
}
