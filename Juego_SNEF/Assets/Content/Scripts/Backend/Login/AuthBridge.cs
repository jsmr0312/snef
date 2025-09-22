using System;
using System.Text;
using UnityEngine;

public class AuthBridge : MonoBehaviour
{
    [Header("Opciones")]
    public string tokenKey = "auth_token";
    public string usernameKey = "player_username";

    [Header("WebGL")]
    public string queryParam = "token"; // ?token=XYZ

    [Header("Dev: pegar token a mano")]
    [TextArea] public string devToken;

    [Header("Dev: simular login")]
    public bool simulateLogin;
    public APIClient api;
    public string devUsername = "bunchyta";
    public string devPassword = "1234";

    void Awake()
    {
        // 1) Querystring
        var qTok = GetQuery(queryParam);
        if (!string.IsNullOrEmpty(qTok)) { SaveToken(qTok, "(from_query)"); return; }

        // 2) Dev token pegado a mano
        if (!string.IsNullOrWhiteSpace(devToken)) { SaveToken(devToken, "(devToken)"); return; }

        // 3) Simular login
        if (simulateLogin && api != null)
        {
            StartCoroutine(api.PostJson<LoginBody, LoginResponse>(
                "/game/login-player",
                new LoginBody { username = devUsername.Trim(), password = devPassword },
                onOk: res => SaveToken(res.token, devUsername),
                onErr: err => Debug.LogError("[AuthBridge] Login simulado falló: " + err)
            ));
        }
    }

    // Para que la página WebGL inyecte el token vía JS:
    // unityInstance.SendMessage('AuthBridge','SetExternalToken','<token>')
    public void SetExternalToken(string token) => SaveToken(token, "(from_js)");

    void SaveToken(string rawToken, string username)
    {
        string token = SanitizeToken(rawToken);
        if (string.IsNullOrEmpty(token))
        {
            Debug.LogError("[AuthBridge] Token vacío/ inválido después de sanitizar.");
            return;
        }
        PlayerPrefs.SetString(tokenKey, token);
        if (!string.IsNullOrEmpty(username)) PlayerPrefs.SetString(usernameKey, username);
        PlayerPrefs.Save();
        Debug.Log("[AuthBridge] Token guardado. len=" + token.Length);
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

    // Quita "Bearer ", comillas, saltos de línea y caracteres de control
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
