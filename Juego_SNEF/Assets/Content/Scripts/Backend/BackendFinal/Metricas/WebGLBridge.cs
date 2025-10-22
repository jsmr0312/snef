using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using UnityEngine;

public class WebGLBridge : MonoBehaviour
{
    public static WebGLBridge I { get; private set; }
    public static string Token { get; private set; } = "";
    public static Action<string> OnTokenChanged;

    [Header("Auto detección")]
    public bool tryQueryParam = true;
    public bool tryLocalStorage = true;
    public bool trySessionStorage = true;
    public bool autoScanStorages = true;   // escanear todas las claves buscando un JWT
    public bool listenPostMessage = true;

    [Header("Claves comunes (editable en inspector)")]
    public string keyAuthToken = "auth_token"; // JWT plano
    public string keyAuthData = "auth_data";  // JSON con { token: "..." }

    [Header("Logs")]
    public bool verbose = true;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern string __GetLocalStorageItem(string key);
    [DllImport("__Internal")] static extern int    __GetLocalStorageKeyCount();
    [DllImport("__Internal")] static extern string __GetLocalStorageKeyAt(int index);
    [DllImport("__Internal")] static extern string __GetSessionStorageItem(string key);
    [DllImport("__Internal")] static extern int    __GetSessionStorageKeyCount();
    [DllImport("__Internal")] static extern string __GetSessionStorageKeyAt(int index);
    [DllImport("__Internal")] static extern void   __SubscribeTokenMessages();
    [DllImport("__Internal")] static extern void   __RequestTokenRefresh();
#endif

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (listenPostMessage) __SubscribeTokenMessages();

        // 1) ?token=
        if (string.IsNullOrEmpty(Token) && tryQueryParam)
        {
            var qTok = GetQuery("token");
            if (!string.IsNullOrEmpty(qTok)) { ReceiveToken(qTok); return; }
        }

        // 2) localStorage por claves conocidas
        if (string.IsNullOrEmpty(Token) && tryLocalStorage)
        {
            var t1 = SanitizeToken(SafeGetLocal(keyAuthToken));
            if (!string.IsNullOrEmpty(t1)) { ReceiveToken(t1); return; }

            var raw = SafeGetLocal(keyAuthData);
            var t2 = SanitizeToken(ExtractTokenFromAuthData(raw));
            if (!string.IsNullOrEmpty(t2)) { ReceiveToken(t2); return; }
        }

        // 3) sessionStorage
        if (string.IsNullOrEmpty(Token) && trySessionStorage)
        {
            var s1 = SanitizeToken(SafeGetSession(keyAuthToken));
            if (!string.IsNullOrEmpty(s1)) { ReceiveToken(s1); return; }

            var raw = SafeGetSession(keyAuthData);
            var s2 = SanitizeToken(ExtractTokenFromAuthData(raw));
            if (!string.IsNullOrEmpty(s2)) { ReceiveToken(s2); return; }
        }

        // 4) escanear todas las claves buscando un JWT
        if (string.IsNullOrEmpty(Token) && autoScanStorages)
        {
            var any = TryAutoScan(out var foundKey, out var foundToken);
            if (any && !string.IsNullOrEmpty(foundToken))
            {
                if (verbose) Debug.Log($"[WebGLBridge] Token detectado en '{foundKey}'.");
                ReceiveToken(foundToken);
                return;
            }
        }

        if (verbose)
            Debug.Log("[WebGLBridge] No se encontró token en local/session storage ni en querystring. " +
                      "Esperando postMessage o SendMessage(\"WebBridge\",\"ReceiveToken\",token).");
#else
        if (verbose) Debug.Log("[WebGLBridge] Editor: no se resuelve token automáticamente.");
#endif
    }

    /// <summary>
    /// Reintenta detectar el token ahora mismo desde query/local/session o escaneo completo.
    /// </summary>
    public bool TryRescanStoragesNow()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // query ?token=
        if (tryQueryParam)
        {
            var qTok = GetQuery("token");
            if (!string.IsNullOrEmpty(qTok)) { ReceiveToken(qTok); return true; }
        }
        // localStorage
        if (tryLocalStorage)
        {
            var t1 = SanitizeToken(SafeGetLocal(keyAuthToken));
            if (!string.IsNullOrEmpty(t1)) { ReceiveToken(t1); return true; }

            var rawL = SafeGetLocal(keyAuthData);
            var t2 = SanitizeToken(ExtractTokenFromAuthData(rawL));
            if (!string.IsNullOrEmpty(t2)) { ReceiveToken(t2); return true; }
        }
        // sessionStorage
        if (trySessionStorage)
        {
            var s1 = SanitizeToken(SafeGetSession(keyAuthToken));
            if (!string.IsNullOrEmpty(s1)) { ReceiveToken(s1); return true; }

            var rawS = SafeGetSession(keyAuthData);
            var s2 = SanitizeToken(ExtractTokenFromAuthData(rawS));
            if (!string.IsNullOrEmpty(s2)) { ReceiveToken(s2); return true; }
        }
        // escaneo total
        if (autoScanStorages && TryAutoScan(out _, out var found))
        {
            ReceiveToken(found);
            return true;
        }

        if (verbose) Debug.Log("[WebGLBridge] TryRescanStoragesNow() no encontró token.");
        return false;
#else
        return false;
#endif
    }

    // Llamado desde JS/React por postMessage bridge: unityInstance.SendMessage("WebBridge","ReceiveToken", token)
    public void ReceiveToken(string token)
    {
        var newToken = SanitizeToken(token ?? "");
        if (string.IsNullOrEmpty(newToken))
        {
            if (verbose) Debug.LogWarning("[WebGLBridge] Token vacío recibido por postMessage. Se ignora.");
            return; // no borrar el token vigente
        }
        if (newToken == Token) return;

        Token = newToken;
        if (verbose) Debug.Log($"[WebGLBridge] Token set (len={Token.Length})");
        OnTokenChanged?.Invoke(Token);
    }

    public void ClearToken() => ReceiveToken("");

    public void RequestTokenRefresh()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // No-op: tokens permanentes; dejamos rastro por compatibilidad
        if (verbose) Debug.Log("[WebGLBridge] RequestTokenRefresh() ignorado (token permanente).");
        // __RequestTokenRefresh(); // si en un futuro vuelves a habilitar refresh en el wrapper
#else
        if (verbose) Debug.Log("[WebGLBridge] Editor: RequestTokenRefresh() ignorado.");
#endif
    }

    // ----------------- Auxiliares -----------------
    static string SanitizeToken(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        s = s.Trim().Replace("\r", "").Replace("\n", "");
        if (s.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) s = s.Substring(7).Trim();
        if (s.Length >= 2 && s[0] == '\"' && s[^1] == '\"') s = s.Substring(1, s.Length - 2);
        return s;
    }

    static string ExtractTokenFromAuthData(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        try
        {
            // JSON barato: busca "token":"..."
            var m = Regex.Match(raw, "\"token\"\\s*:\\s*\"([^\"]+)\"");
            return m.Success ? m.Groups[1].Value : "";
        }
        catch { return ""; }
    }

    static bool LooksLikeJwt(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        // patrón simple: tres segmentos base64url separados por punto
        return Regex.IsMatch(s, "^[A-Za-z0-9_-]+\\.[A-Za-z0-9_-]+\\.[A-Za-z0-9_-]+$");
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    string SafeGetLocal(string key)   { try { return __GetLocalStorageItem(key)   ?? ""; } catch { return ""; } }
    string SafeGetSession(string key) { try { return __GetSessionStorageItem(key) ?? ""; } catch { return ""; } }
    int    LocalCount()               { try { return __GetLocalStorageKeyCount(); }   catch { return 0; } }
    int    SessionCount()             { try { return __GetSessionStorageKeyCount(); } catch { return 0; } }
    string LocalKeyAt(int i)          { try { return __GetLocalStorageKeyAt(i)   ?? ""; } catch { return ""; } }
    string SessionKeyAt(int i)        { try { return __GetSessionStorageKeyAt(i) ?? ""; } catch { return ""; } }
#else
    string SafeGetLocal(string key) => "";
    string SafeGetSession(string key) => "";
    int LocalCount() => 0;
    int SessionCount() => 0;
    string LocalKeyAt(int i) => "";
    string SessionKeyAt(int i) => "";
#endif

    bool TryAutoScan(out string foundKey, out string token)
    {
        foundKey = null; token = null;

        // 1) localStorage
        int nL = LocalCount();
        for (int i = 0; i < nL; i++)
        {
            var k = LocalKeyAt(i);
            var v = SafeGetLocal(k);

            var t = SanitizeToken(ExtractTokenFromAuthData(v));
            if (LooksLikeJwt(t)) { foundKey = "localStorage:" + k; token = t; return true; }

            var sv = SanitizeToken(v);
            if (LooksLikeJwt(sv)) { foundKey = "localStorage:" + k; token = sv; return true; }
        }

        // 2) sessionStorage
        int nS = SessionCount();
        for (int i = 0; i < nS; i++)
        {
            var k = SessionKeyAt(i);
            var v = SafeGetSession(k);

            var t = SanitizeToken(ExtractTokenFromAuthData(v));
            if (LooksLikeJwt(t)) { foundKey = "sessionStorage:" + k; token = t; return true; }

            var sv = SanitizeToken(v);
            if (LooksLikeJwt(sv)) { foundKey = "sessionStorage:" + k; token = sv; return true; }
        }

        return false;
    }

    string GetQuery(string key)
    {
        var url = Application.absoluteURL;
        var i = url.IndexOf('?');
        if (i < 0) return null;
        var pairs = url[(i + 1)..].Split('&');
        foreach (var p in pairs)
        {
            var kv = p.Split('=');
            if (kv.Length == 2 && kv[0] == key)
                return Uri.UnescapeDataString(kv[1]);
        }
        return null;
    }
}
