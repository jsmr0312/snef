using System;
using System.Text;
using UnityEngine;

public class AuthBridge : MonoBehaviour
{
    [Header("Opciones (solo compatibilidad / debug)")]
    public string tokenKey = "auth_token";     // Fallback sólo en dev
    public string usernameKey = "player_username";

    [Header("WebGL")]
    [Tooltip("Nombre del query param si alguna vez pasas ?token=XYZ (sólo debug)")]
    public string queryParam = "token";

    [Header("Dev: pegar token a mano (sólo pruebas)")]
    [TextArea] public string devToken;

    [Header("Dev: simular login (NO llama APIs)")]
    public bool simulateLogin;
    public string devUsername = "bunchyta";

    void Awake()
    {
        // 1) Si viene por querystring (?token=XYZ) — útil en pruebas manuales:
        var qTok = GetQuery(queryParam);
        if (!string.IsNullOrEmpty(qTok))
        {
            SetExternalToken(qTok);
            SaveDevUsername("(from_query)");
            return;
        }

        // 2) Token pegado a mano para dev:
        if (!string.IsNullOrWhiteSpace(devToken))
        {
            SetExternalToken(devToken);
            SaveDevUsername("(devToken)");
            return;
        }

        // 3) "Simular login" ahora sólo aplica devToken si existe (no hace llamadas)
        if (simulateLogin && !string.IsNullOrWhiteSpace(devToken))
        {
            SetExternalToken(devToken);
            SaveDevUsername(devUsername);
            return;
        }

        // Producción: el token debe llegar desde React con:
        // unityInstance.SendMessage("WebBridge","ReceiveToken", token)
        // o, si sigues usando este puente:
        // unityInstance.SendMessage("AuthBridge","SetExternalToken", token)
    }

    // Para que la página WebGL pueda inyectar el token (alternativa a WebBridge):
    // unityInstance.SendMessage("AuthBridge","SetExternalToken","<token>")
    public void SetExternalToken(string rawToken)
    {
        string token = SanitizeToken(rawToken);
        if (string.IsNullOrEmpty(token))
        {
            Debug.LogError("[AuthBridge] Token vacío/ inválido después de sanitizar.");
            return;
        }

        // Preferimos guardarlo en memoria vía WebGLBridge (flujo nuevo)
        if (WebGLBridge.I != null)
        {
            WebGLBridge.I.ReceiveToken(token);
            Debug.Log("[AuthBridge] Token aplicado a WebGLBridge (memoria). len=" + token.Length);
        }
        else
        {
            Debug.LogWarning("[AuthBridge] WebGLBridge no está listo aún. Se usará fallback de dev (PlayerPrefs).");
        }

        // Fallback de desarrollo: PlayerPrefs (EVITAR en producción WebGL)
#if !UNITY_WEBGL || UNITY_EDITOR
        PlayerPrefs.SetString(tokenKey, token);
        PlayerPrefs.Save();
#endif
    }

    void SaveDevUsername(string username)
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        if (!string.IsNullOrEmpty(username))
        {
            PlayerPrefs.SetString(usernameKey, username);
            PlayerPrefs.Save();
        }
#endif
    }

    string GetQuery(string key)
    {
        var url = Application.absoluteURL;
        var i = url.IndexOf('?');
        if (i < 0) return null;
        var pairs = url.Substring(i + 1).Split('&');
        foreach (var p in pairs)
        {
            var kv = p.Split('=');
            if (kv.Length == 2 && kv[0] == key) return Uri.UnescapeDataString(kv[1]);
        }
        return null;
    }

    // Quita "Bearer ", comillas, saltos y controles
    public static string SanitizeToken(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        s = s.Trim();
        s = s.Replace("\r", "").Replace("\n", "");
        if (s.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            s = s.Substring(7).Trim();
        if (s.Length >= 2 && s[0] == '"' && s[s.Length - 1] == '"')
            s = s.Substring(1, s.Length - 2);

        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
            if (!char.IsControl(ch)) sb.Append(ch);
        return sb.ToString();
    }
}
