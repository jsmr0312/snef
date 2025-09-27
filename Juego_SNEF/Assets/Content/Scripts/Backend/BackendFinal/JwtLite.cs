using System;
using System.Text;
using System.Text.RegularExpressions;

public static class JwtLite
{
    // Devuelve el valor string de un claim dentro del JWT (sin validar firma).
    public static string GetClaim(string jwt, string claimKey)
    {
        if (string.IsNullOrWhiteSpace(jwt)) return null;
        var parts = jwt.Split('.');
        if (parts.Length < 2) return null;

        string payload = parts[1].Replace('-', '+').Replace('_', '/');
        switch (payload.Length % 4) { case 2: payload += "=="; break; case 3: payload += "="; break; }

        string json;
        try { json = Encoding.UTF8.GetString(Convert.FromBase64String(payload)); }
        catch { return null; }

        // Busca "claimKey":"valor"
        try
        {
            var rx = new Regex($"\"{Regex.Escape(claimKey)}\"\\s*:\\s*\"([^\"]+)\"");
            var m = rx.Match(json);
            if (m.Success) return m.Groups[1].Value;

            // alternativo: id numérico -> lo devolvemos como string
            rx = new Regex($"\"{Regex.Escape(claimKey)}\"\\s*:\\s*([0-9]+)");
            m = rx.Match(json);
            if (m.Success) return m.Groups[1].Value;
        }
        catch { }
        return null;
    }

    // Atajo común: algunos JWT usan "sub", otros "user_id".
    public static string GetUserId(string jwt)
    {
        var v = GetClaim(jwt, "user_id");
        if (!string.IsNullOrEmpty(v)) return v;
        v = GetClaim(jwt, "sub");
        return v;
    }
}
