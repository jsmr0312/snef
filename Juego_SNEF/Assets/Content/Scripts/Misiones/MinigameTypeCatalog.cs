using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "SNEF/Minigame Type Catalog")]
public class MinigameTypeCatalog : ScriptableObject
{
    [Serializable]
    public class TypeEntry
    {
        public string typeKey = "tipo1";         // p.ej. "Runner", "Falling", "Shop"
        public List<string> baseIds = new List<string>(); // miniGameId base de ArcadeInteractable
    }

    public List<TypeEntry> entries = new List<TypeEntry>();
    Dictionary<string, string> _byId;

    void OnEnable()
    {
        _byId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in entries)
            foreach (var id in e.baseIds)
                if (!string.IsNullOrWhiteSpace(id))
                    _byId[id.Trim()] = e.typeKey;
    }

    public bool TryResolveType(string baseId, out string typeKey)
    {
        typeKey = null;
        return _byId != null && !string.IsNullOrWhiteSpace(baseId) && _byId.TryGetValue(baseId.Trim(), out typeKey);
    }
}
