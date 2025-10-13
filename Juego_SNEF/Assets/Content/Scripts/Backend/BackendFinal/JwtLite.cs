// JwtLite.cs — añade helpers para exp (unix) y verificación de caducidad
using System;
using System.Text;
using System.Text.RegularExpressions;

public static class JwtLite
{
    // (EXISTENTE) — GetClaim(...) y GetUserId(...)

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

        try
        {
            var rx = new Regex($"\"{Regex.Escape(claimKey)}\"\\s*:\\s*\"([^\"]+)\"");
            var m = rx.Match(json);
            if (m.Success) return m.Groups[1].Value;

            rx = new Regex($"\"{Regex.Escape(claimKey)}\"\\s*:\\s*([0-9]+)");
            m = rx.Match(json);
            if (m.Success) return m.Groups[1].Value;
        }
        catch { }
        return null;
    }

    public static string GetUserId(string jwt)
    {
        var v = GetClaim(jwt, "user_id");
        if (!string.IsNullOrEmpty(v)) return v;
        v = GetClaim(jwt, "sub");
        return v;
    }

    // --- NUEVO: lectura de exp (segundos UNIX) + utilidades ---
    public static long GetExpiryUnix(string jwt)
    {
        var raw = GetClaim(jwt, "exp");
        return long.TryParse(raw, out var unix) ? unix : 0L;
    }

    public static int SecondsToExpiry(string jwt, int clockSkewSeconds = 0)
    {
        if (string.IsNullOrWhiteSpace(jwt)) return int.MinValue;
        var expUnix = GetExpiryUnix(jwt);
        if (expUnix <= 0) return int.MaxValue; // sin exp => trátalo como no expirable
        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return (int)(expUnix - nowUnix - clockSkewSeconds);
    }

    public static bool IsExpired(string jwt, int clockSkewSeconds = 30)
    {
        return SecondsToExpiry(jwt, clockSkewSeconds) <= 0;
    }

    public static bool WillExpireSoon(string jwt, int thresholdSeconds = 60)
    {
        return SecondsToExpiry(jwt) <= thresholdSeconds;
    }
}
