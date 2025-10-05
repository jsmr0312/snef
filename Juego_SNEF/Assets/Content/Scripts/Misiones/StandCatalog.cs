using System;
using System.Collections.Generic;
using UnityEngine;

public enum StandKind { Regular, Experience }

public class StandCatalog : MonoBehaviour
{
    public static StandCatalog I { get; private set; }

    [Serializable]
    public class Entry
    {
        public string standId;
        public string ecosystemName;   // escribe "Ecosistema 3", "Ecosistema 2", etc.
        public StandKind kind = StandKind.Regular; // Regular o Experience (punto de experiencia)
        public string displayName;     // opcional (sponsor)
    }

    [Header("Catálogo de stands")]
    public List<Entry> entries = new List<Entry>();

    Dictionary<string, Entry> _byId;

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);

        _byId = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in entries)
        {
            if (e == null || string.IsNullOrWhiteSpace(e.standId)) continue;
            _byId[e.standId.Trim()] = e;
        }
    }

    public bool TryGet(string standId, out Entry e)
    {
        e = null;
        if (string.IsNullOrWhiteSpace(standId)) return false;
        return _byId != null && _byId.TryGetValue(standId.Trim(), out e);
    }
}
