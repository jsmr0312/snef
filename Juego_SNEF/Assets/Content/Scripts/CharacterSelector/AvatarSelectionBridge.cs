using UnityEngine;

public static class AvatarSelectionBridge
{
    const string KEY_INDEX = "avatar_index";     // 1..12
    const string KEY_SELECTED = "avatar_selected";

    public static int GetSelectedIndexFromPrefs()
    {
        return PlayerPrefs.GetInt(KEY_INDEX, 0);
    }

    public static void SetSelectedIndexToPrefs(int index)
    {
        if (index < 1 || index > 12) return;
        PlayerPrefs.SetInt(KEY_INDEX, index);
        PlayerPrefs.SetInt(KEY_SELECTED, 1);
        PlayerPrefs.Save();
    }

    // Calcula índice desde 12 bools
    public static int GetSelectedIndexFromBools(params bool[] flags)
    {
        for (int i = 0; i < flags.Length; i++) if (flags[i]) return i + 1;
        return 0;
    }

    // Lee índice desde los 12 PlayerPrefs legacy
    public static int GetSelectedIndexFromLegacyPrefs()
    {
        for (int i = 1; i <= 12; i++)
        {
            if (PlayerPrefs.GetInt($"personaje{i}Select", 0) == 1) return i;
        }
        return 0;
    }
}
