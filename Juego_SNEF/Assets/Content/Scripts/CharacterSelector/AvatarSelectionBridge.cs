using UnityEngine;

/// <summary>
/// Puente de compatibilidad para selección de avatar basada en ÍNDICE (1..12) o por ID.
/// - Lee/escribe el índice en PlayerPrefs (legacy compatible)
/// - Resuelve ID ↔ índice usando AvatarIdMapper si existe en escena
/// - Actualiza ProgressCore (cache local) y ProgressRemote (backend) cuando cambia el avatar
/// </summary>
public static class AvatarSelectionBridge
{
    // Claves legacy usadas por tus scripts
    private const string KEY_AVATAR_INDEX = "avatar_index";             // 1..12
    private const string KEY_AVATAR_SELECTED_FLAG = "avatar_selected";  // 0/1

    /// Devuelve el índice guardado en PlayerPrefs (1..12). Si no existe o no es válido, devuelve 0.
    public static int GetSelectedIndexFromPrefs()
    {
        int idx = PlayerPrefs.GetInt(KEY_AVATAR_INDEX, 0);
        return (idx >= 1 && idx <= 12) ? idx : 0;
    }

    /// Busca en las flags legacy personaje{1..12}Select en PlayerPrefs y devuelve el índice activo (o 0).
    public static int GetSelectedIndexFromLegacyPrefs()
    {
        for (int i = 1; i <= 12; i++)
        {
            string key = $"personaje{i}Select";
            if (PlayerPrefs.GetInt(key, 0) == 1) return i;
        }
        return 0;
    }

    /// Dado un set de 12 bools (personaje1..12), devuelve el índice activo (1..12) o 0 si ninguno.
    public static int GetSelectedIndexFromBools(
        bool p1, bool p2, bool p3, bool p4, bool p5, bool p6,
        bool p7, bool p8, bool p9, bool p10, bool p11, bool p12)
    {
        if (p1) return 1; if (p2) return 2; if (p3) return 3; if (p4) return 4;
        if (p5) return 5; if (p6) return 6; if (p7) return 7; if (p8) return 8;
        if (p9) return 9; if (p10) return 10; if (p11) return 11; if (p12) return 12;
        return 0;
    }

    /// Guarda el índice (1..12) en PlayerPrefs y marca avatar_selected=1.
    /// Si hay mapper/progreso, sincroniza ID y backend.
    public static void SetSelectedIndexToPrefs(int index, bool syncProgress = true)
    {
        if (index < 1 || index > 12)
        {
            Debug.LogWarning($"[AvatarSelectionBridge] Índice inválido: {index}");
            return;
        }

        PlayerPrefs.SetInt(KEY_AVATAR_INDEX, index);
        PlayerPrefs.SetInt(KEY_AVATAR_SELECTED_FLAG, 1);
        PlayerPrefs.Save();

        if (!syncProgress) return;

        string avatarId = ResolveAvatarId(index);
        if (!string.IsNullOrEmpty(avatarId))
        {
            if (ProgressCore.I != null)
                ProgressCore.I.SetAvatar(avatarId);

            if (ProgressRemote.I != null)
                ProgressRemote.I.UpdateProfileAvatar(avatarId);
        }
    }

    /// Devuelve el ID del avatar (string) según el índice, usando AvatarIdMapper si existe.
    public static string ResolveAvatarId(int index)
    {
        var mapper = Object.FindObjectOfType<AvatarIdMapper>();
        if (mapper == null) return null;
        return mapper.GetByIndex(index);
    }

    /// Atajo: obtiene el ID del avatar actualmente seleccionado (lee prefs/legacy y mapea).
    public static string GetSelectedAvatarId()
    {
        int idx = GetSelectedIndexFromPrefs();
        if (idx == 0) idx = GetSelectedIndexFromLegacyPrefs();
        return ResolveAvatarId(idx);
    }

    /// NUEVO: Fija el avatar por **ID** (p.ej. "a3") y sincroniza progreso.
    /// - Si encuentra el índice correspondiente en el mapper, lo guarda en PlayerPrefs (legacy compat).
    /// - Siempre actualiza ProgressCore y ProgressRemote con el ID.
    public static void SetAvatarId(string avatarId, bool alsoWriteIndex = true)
    {
        if (string.IsNullOrWhiteSpace(avatarId))
        {
            Debug.LogWarning("[AvatarSelectionBridge] avatarId vacío.");
            return;
        }
        string clean = avatarId.Trim();

        // Intentar mapear a índice para mantener compatibilidad con scripts legacy
        if (alsoWriteIndex)
        {
            int mappedIndex = 0;
            var mapper = Object.FindObjectOfType<AvatarIdMapper>();
            if (mapper != null && mapper.avatarIds != null && mapper.avatarIds.Length > 0)
            {
                for (int i = 0; i < mapper.avatarIds.Length; i++)
                {
                    var id = mapper.avatarIds[i];
                    if (!string.IsNullOrWhiteSpace(id) &&
                        string.Equals(id.Trim(), clean, System.StringComparison.OrdinalIgnoreCase))
                    {
                        mappedIndex = i + 1; // 1..N
                        break;
                    }
                }
            }

            if (mappedIndex > 0)
            {
                PlayerPrefs.SetInt(KEY_AVATAR_INDEX, mappedIndex);
                PlayerPrefs.SetInt(KEY_AVATAR_SELECTED_FLAG, 1);
                PlayerPrefs.Save();
            }
            else
            {
                Debug.LogWarning($"[AvatarSelectionBridge] No se encontró índice para avatarId='{clean}'. Se sincroniza por ID sin tocar PlayerPrefs.");
            }
        }

        // Cache local → HUDs/reenudación
        if (ProgressCore.I != null)
            ProgressCore.I.SetAvatar(clean);

        // Remoto → perfil.avatar_id
        if (ProgressRemote.I != null)
            ProgressRemote.I.UpdateProfileAvatar(clean);
    }
}
