using UnityEngine;

public class AvatarIdMapper : MonoBehaviour
{
    [Tooltip("Mapea índice [1..12] → ID/nombre para métricas (p.ej. a1, a2, Avatar 3). [0] es el avatar 1.")]
    public string[] avatarIds = new string[12];

    public string GetCurrentAvatarId()
    {
        int idx = AvatarSelectionBridge.GetSelectedIndexFromPrefs(); // 1..12
        if (idx == 0) idx = AvatarSelectionBridge.GetSelectedIndexFromLegacyPrefs();
        return GetByIndex(idx);
    }

    public string GetByIndex(int idx)
    {
        if (idx < 1 || idx > avatarIds.Length) return null;
        var id = avatarIds[idx - 1];
        return string.IsNullOrWhiteSpace(id) ? null : id.Trim();
    }
}
