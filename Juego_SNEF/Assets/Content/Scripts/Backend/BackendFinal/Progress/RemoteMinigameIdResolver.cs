// RemoteMinigameIdResolver.cs
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public static class RemoteMinigameIdResolver
{
    // Extra: mapeos puntuales por si algún día cambias nombres o llega algo “raro”
    static readonly Dictionary<string, string> Overrides = new(StringComparer.OrdinalIgnoreCase)
    {
        // Por si ya te llega directo con ID interno:
        { "corre_y_gana", "corre_y_gana" },
        { "escala_y_gana", "escala_y_gana" },
        { "lluvia_objetos", "lluvia_objetos" },
        { "compra_responsiva", "compra_responsiva" },

        // Por si vienen con espacios o guiones
        { "corre y gana", "corre_y_gana" },
        { "escala y gana", "escala_y_gana" },
        { "lluvia objetos", "lluvia_objetos" },
        { "compra responsiva", "compra_responsiva" },
    };

    static readonly Regex Rx = new Regex(@"minijuego\s*(\d+)\s*[_\- ]?\s*e\s*(\d+)",
                                         RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string ToBaseId(string remoteName)
    {
        if (string.IsNullOrWhiteSpace(remoteName)) return null;

        var key = remoteName.Trim();

        // 1) Overrides/directos
        if (Overrides.TryGetValue(key, out var direct)) return direct;

        // 2) Patrón "MinijuegoX_EY"
        var m = Rx.Match(key);
        if (m.Success && int.TryParse(m.Groups[1].Value, out var idx))
        {
            switch (idx)
            {
                case 1: return "corre_y_gana";
                case 2: return "escala_y_gana";       // mismo manager; variante visual
                case 3: return "lluvia_objetos";
                case 4: return "compra_responsiva";
                default: return null; // Si algún día hay Minijuego5, aquí decides su baseId
            }
        }

        // 3) Fallback: normaliza por si ya es un ID interno con mayúsculas/espacios
        key = key.ToLowerInvariant().Replace(' ', '_');
        return Overrides.TryGetValue(key, out var norm) ? norm : null;
    }
}
