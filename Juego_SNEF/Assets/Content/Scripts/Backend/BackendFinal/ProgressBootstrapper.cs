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
#endif

    void Start()
    {
        StartCoroutine(BootstrapRoutine());
    }

    IEnumerator BootstrapRoutine()
    {
        string token = WebGLBridge.Token ?? "";
        string raw = ReadLocal(progressKey);

        // --- Espera corta si aún no existe (primer ingreso típico) ---
        float waited = 0f;
        while ((string.IsNullOrEmpty(raw) || raw == "{}") &&
               waited < waitForProgressSeconds)
        {
            yield return new WaitForSecondsRealtime(pollIntervalSeconds);
            waited += pollIntervalSeconds;
            raw = ReadLocal(progressKey);
        }

        bool noBootstrap = string.IsNullOrEmpty(raw) || raw == "{}";
        bool isGuest = resetOnGuestToken && IsGuestToken(token);

        // ======= Caso invitado (con o sin token) o sin bootstrap =======
        if (noBootstrap && (isGuest || string.IsNullOrEmpty(token)))
        {
            if (log) Debug.Log($"[Bootstrap] Reset invitado. noBootstrap={noBootstrap}, isGuest={isGuest}, hasToken={!string.IsNullOrEmpty(token)}");
            GuestHardReset();
            yield break;
        }

        // ======= Sin bootstrap => NO mezclar con progreso local =======
        if (noBootstrap)
        {
            if (log) Debug.Log("[Bootstrap] No hay progress-storage; reseteo limpio (modo estricto).");
            GuestHardReset();            // ← limpio real: sin avatar, sin stands, sin minijuegos
            yield break;
        }

        // ======= Sí hay bootstrap => aplicarlo =======
        string json = UnwrapIfQuoted(raw);
        ProgressCore.I?.LoadFromBootstrapJson(json);

        if (clearKeyAfterRead) RemoveLocal(progressKey);
        if (syncStatsHere) MirrorStatsFromProgress();

        if (log) Debug.Log("[Bootstrap] Progreso inicial aplicado desde localStorage.");
    }

    // ---------------- helpers ----------------
    void GuestHardReset()
    {
        // Opcional: limpiar todos los PlayerPrefs si quieres un reset 100% limpio
        if (hardDeleteAllPrefsOnGuest)
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
        }
        else
        {
            // Limpia selección de personaje/legacy si los usas
            PlayerPrefs.DeleteKey("personaje1Select");
            PlayerPrefs.DeleteKey("personaje2Select");
            PlayerPrefs.DeleteKey("personaje3Select");
            PlayerPrefs.Save();
        }

        // Reset del modelo central y UI (sin métricas)
        ProgressCore.I?.ResetLocalProgress("guest mode");
        Stats.I?.SetTotalsSilently(0, 0);
    }

    string ReadLocal(string key)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try { return __GetLocalStorageItem(key); } catch { return ""; }
#else
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
        if (s.Length >= 2 && s[0] == '\"' && s[^1] == '\"')
        {
            s = s.Substring(1, s.Length - 2);
            s = Regex.Unescape(s);
        }
        return s;
    }

    // ---- Detección robusta de token invitado ----
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

            // Acepta varias convenciones comunes
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
