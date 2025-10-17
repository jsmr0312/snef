// DisplayUsernameFromJwt.cs
using UnityEngine;
using TMPro;

public class DisplayUsernameFromJwt : MonoBehaviour
{
    [Header("Asignar un TextMeshPro (TMP_Text, TextMeshProUGUI o TextMeshPro)")]
    public TMP_Text target;
    [Tooltip("Texto a mostrar si es anónimo o no hay nombre")]
    public string guestLabel = "Invitad@";

    void OnEnable()
    {
        UpdateFromToken(WebGLBridge.Token);
        WebGLBridge.OnTokenChanged += UpdateFromToken;   // se actualiza si hay refresh
    }

    void OnDisable()
    {
        WebGLBridge.OnTokenChanged -= UpdateFromToken;
    }

    void UpdateFromToken(string jwt)
    {
        if (target == null) return;

        string name = TryGetDisplayName(jwt);
        target.text = string.IsNullOrEmpty(name) ? guestLabel : name;
    }

    // Intenta distintos claims comunes; cae a email (parte antes de @) o sub
    string TryGetDisplayName(string jwt)
    {
        if (string.IsNullOrWhiteSpace(jwt)) return null;

        string[] keys = { "name", "username", "preferred_username", "given_name" };
        foreach (var k in keys)
        {
            var v = JwtLite.GetClaim(jwt, k);
            if (!string.IsNullOrEmpty(v)) return v;
        }

        var email = JwtLite.GetClaim(jwt, "email");
        if (!string.IsNullOrEmpty(email) && email.Contains("@"))
            return email.Split('@')[0];

        // último recurso: sub (id del sujeto), lo recortamos para que no sea larguísimo
        var sub = JwtLite.GetClaim(jwt, "sub");
        if (!string.IsNullOrEmpty(sub))
            return sub.Length > 8 ? $"user-{sub[..8]}" : sub;

        return null;
    }
}
