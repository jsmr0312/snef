// UsernameToTMP.cs
using UnityEngine;
using TMPro;
using System.Runtime.InteropServices;

public class UsernameToTMP : MonoBehaviour
{
    [Header("Asignar tu TextMeshPro (UGUI o 3D)")]
    public TMP_Text target;
    public string guestLabel = "Invitad@";

    // Claves en localStorage que ya tienes
    const string GuestKey = "guestlastAttempt"; // trae username para anónimos
    const string AuthKey = "auth-storage";     // suele envolver { state:{ token } }

    void OnEnable()
    {
        UpdateLabel();
        WebGLBridge.OnTokenChanged += OnTokenChanged; // si el token se refresca
    }
    void OnDisable() => WebGLBridge.OnTokenChanged -= OnTokenChanged;
    void OnTokenChanged(string _) => UpdateLabel();

    void UpdateLabel()
    {
        if (target == null) return;
        var name = GetUsernameFromGuestLS() ?? GetUsernameFromJwt() ?? guestLabel;
        target.text = name;
    }

    // --- 1) Caso invitado: lee guestlastAttempt.username ---
    string GetUsernameFromGuestLS()
    {
        var raw = LS_GetItem(GuestKey);
        if (string.IsNullOrEmpty(raw)) return null;
        var data = JsonUtility.FromJson<GuestLastAttempt>(raw);
        return string.IsNullOrEmpty(data?.username) ? null : data.username;
    }

    // --- 2) Caso login normal: saca nombre del JWT ---
    string GetUsernameFromJwt()
    {
        // Prioriza el token expuesto por tu bridge; si no, lee auth-storage
        string jwt = WebGLBridge.Token;
        if (string.IsNullOrEmpty(jwt))
        {
            var authRaw = LS_GetItem(AuthKey);
            if (!string.IsNullOrEmpty(authRaw))
            {
                var auth = JsonUtility.FromJson<AuthStorage>(authRaw);
                jwt = auth?.state?.token;
            }
        }
        if (string.IsNullOrEmpty(jwt)) return null;

        string[] keys = { "name", "preferred_username", "username", "given_name" };
        foreach (var k in keys)
        {
            var v = JwtLite.GetClaim(jwt, k);
            if (!string.IsNullOrEmpty(v)) return v;
        }
        var email = JwtLite.GetClaim(jwt, "email");
        if (!string.IsNullOrEmpty(email) && email.Contains("@"))
            return email.Split('@')[0];

        var sub = JwtLite.GetClaim(jwt, "sub");
        return string.IsNullOrEmpty(sub) ? null : (sub.Length > 8 ? $"user-{sub[..8]}" : sub);
    }

    // --- Wrapper para localStorage (usa tu jslib en WebGL y PlayerPrefs en Editor) ---
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern string __GetLocalStorageItem(string key);
    static string LS_GetItem(string key) => __GetLocalStorageItem(key);
#else
    static string LS_GetItem(string key) => PlayerPrefs.GetString(key);
#endif

    // Modelos para JSON
    [System.Serializable]
    class GuestLastAttempt
    {
        public string time;
        public string username;
        public bool is_anon;
        public string age;
        public string genero;
        public string entidad_federativa;
        public QA[] preguntas;
    }
    [System.Serializable] class QA { public string question; public string answer; }

    [System.Serializable] class AuthStorageState { public string token; }
    [System.Serializable] class AuthStorage { public AuthStorageState state; }
}
