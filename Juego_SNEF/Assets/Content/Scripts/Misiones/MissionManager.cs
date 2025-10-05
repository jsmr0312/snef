using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

/// Misiones por ecosistema (independientes):
///  - Visita y completa N stands (Regular: Master/Premier/Excellence)
///  - Visita y completa el punto de experiencia (Experience)
///  - Juega y consigue 3★ en un minijuego (cualquiera)
public class MissionManager : MonoBehaviour
{
    public static MissionManager I { get; private set; }

    [Serializable]
    public class EcosystemConfig
    {
        public string ecosystemName = "Ecosistema 1";
        [Tooltip("Cuántos stands REGULARES se requieren en este ecosistema")]
        public int requiredStandCompletions = 4;
    }

    [Header("Config por ecosistema")]
    public EcosystemConfig[] ecosystems = new EcosystemConfig[]
    {
        new EcosystemConfig{ ecosystemName = "Ecosistema 1", requiredStandCompletions = 4 },
        new EcosystemConfig{ ecosystemName = "Ecosistema 2", requiredStandCompletions = 4 },
        new EcosystemConfig{ ecosystemName = "Ecosistema 3", requiredStandCompletions = 4 },
        new EcosystemConfig{ ecosystemName = "Ecosistema 4", requiredStandCompletions = 4 },
    };

    // --------- Estado persistente ---------
    [Serializable]
    public class EcoState
    {
        public string eco;
        public List<string> standsCompleted = new List<string>(); // REGULARES (no experience)
        public bool experienceCompleted = false;                   // punto de experiencia
        public bool anyMinigame3Stars = false;                     // >=3★ en cualquier minijuego del eco
    }

    [Serializable]
    class SaveData { public List<EcoState> ecos = new List<EcoState>(); }

    const string PP_KEY = "SNEF_MISSIONS_V1";
    readonly Dictionary<string, EcoState> _byEco =
        new Dictionary<string, EcoState>(StringComparer.OrdinalIgnoreCase);

    public event Action<string, EcoState> OnEcoStateChanged;

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
        Load();
    }

    // ============================== API ======================================

    /// Llamar cuando un stand alcanza la fase final (quiz completado).
    /// ecosystemName puede venir vacío o distinto; lo resolvemos por catálogo usando standId.
    /// standType es la etiqueta del NPC ("Punto de Experiencia" / "Stand Master" / etc.).
    public void NotifyStandCompleted(string ecosystemName, string standId, string standType)
    {
        // 1) Resolver ecosistema robustamente
        ecosystemName = ResolveEcoByIdOrName(ecosystemName, standId);
        if (string.IsNullOrEmpty(ecosystemName))
        {
            Debug.LogWarning($"[Mission] No se pudo resolver ecosistema para standId={standId}. Revisa StandCatalog.");
            return;
        }

        var st = GetEco(ecosystemName);

        // 2) Resolver tipo de stand: priorizar catálogo por ID; si no hay, parsear por texto
        StandKind kind = ParseStandType(standType);
        if (StandCatalog.I && StandCatalog.I.TryGet(standId, out var entry))
            kind = entry.kind;

        // 3) Lógica de misiones
        if (kind == StandKind.Experience)
        {
            if (!st.experienceCompleted)
            {
                st.experienceCompleted = true;
                Debug.Log($"[Mission] ({ecosystemName}) Punto de experiencia COMPLETADO. standId={standId}");
                Save(); Push(ecosystemName);
                MetricsClient.I?.TrackMisionCompletada(ecosystemName, "Completa punto de experiencia");
            }
            return;
        }

        // Regular (Master / Premier / Excellence)
        bool beforeWasComplete = st.standsCompleted.Count >= Required(ecosystemName);

        if (!st.standsCompleted.Contains(standId))
        {
            st.standsCompleted.Add(standId);
            Debug.Log($"[Mission] ({ecosystemName}) Stand REGULAR COMPLETADO: {standId}. Total={st.standsCompleted.Count}");
            Save(); Push(ecosystemName);

            if (!beforeWasComplete && st.standsCompleted.Count >= Required(ecosystemName))
                MetricsClient.I?.TrackMisionCompletada(ecosystemName, "Completa 4 stands");
        }
    }

    /// Llamar cuando termina un minijuego y ya tenemos estrellas (0..3).
    public void NotifyMinigameResult(string ecosystemName, int stars)
    {
        if (stars < 3) return;

        ecosystemName = NormalizeEcoName(ResolveEcoByIdOrName(ecosystemName, ""));
        if (string.IsNullOrEmpty(ecosystemName)) return;

        var st = GetEco(ecosystemName);
        if (!st.anyMinigame3Stars)
        {
            st.anyMinigame3Stars = true;
            Debug.Log($"[Mission] ({ecosystemName}) Misión: 3★ en minijuego COMPLETADA.");
            Save(); Push(ecosystemName);
            MetricsClient.I?.TrackMisionCompletada(ecosystemName, "3 estrellas en minijuego");
        }
    }

    /// Snapshot del ecosistema (para UI)
    public EcoState GetEcoState(string ecosystemName) => GetEco(NormalizeEcoName(ecosystemName));

    public bool IsComplete_4Stands(string eco) => GetEco(NormalizeEcoName(eco)).standsCompleted.Count >= Required(NormalizeEcoName(eco));
    public bool IsComplete_Experience(string eco) => GetEco(NormalizeEcoName(eco)).experienceCompleted;
    public bool IsComplete_Minigame3Stars(string eco) => GetEco(NormalizeEcoName(eco)).anyMinigame3Stars;

    // ============================ Internals ==================================

    // Normaliza nombres tipo "Ecosistema3", "eco-3" -> "Ecosistema 3"
    static string NormalizeEcoName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        raw = raw.Trim();
        var m = Regex.Match(raw, @"ecosistema\s*[-_ ]?\s*(\d+)", RegexOptions.IgnoreCase);
        if (m.Success) return $"Ecosistema {m.Groups[1].Value}";
        return raw;
    }

    // Mapea strings del NPC a un tipo genérico
    static StandKind ParseStandType(string type)
    {
        if (string.IsNullOrWhiteSpace(type)) return StandKind.Regular;
        var t = type.Trim().ToLowerInvariant();

        // Español
        if (t.Contains("punto de experiencia") || t.Contains("experiencia"))
            return StandKind.Experience;

        // Inglés por si acaso
        if (t == "experience" || t.Contains("experience point"))
            return StandKind.Experience;

        // Master / Premier / Excellence -> Regular
        return StandKind.Regular;
    }

    // Si el nombre viene raro o vacío, intentar resolver por standId en StandCatalog
    string ResolveEcoByIdOrName(string ecosystemName, string standId)
    {
        var eco = NormalizeEcoName(ecosystemName);

        // 1) Si ya coincide con una config, úsalo
        foreach (var c in ecosystems)
            if (string.Equals(c.ecosystemName, eco, StringComparison.OrdinalIgnoreCase))
                return eco;

        // 2) Sino, intenta por catálogo con standId
        if (!string.IsNullOrEmpty(standId) && StandCatalog.I && StandCatalog.I.TryGet(standId, out var entry))
            return NormalizeEcoName(entry.ecosystemName);

        // 3) Fallback: regresa nombre normalizado (puede no existir en config, pero guardamos algo)
        return eco;
    }

    int Required(string eco)
    {
        eco = NormalizeEcoName(eco);
        foreach (var c in ecosystems)
            if (string.Equals(c.ecosystemName, eco, StringComparison.OrdinalIgnoreCase))
                return Mathf.Max(1, c.requiredStandCompletions);
        return 4;
    }

    EcoState GetEco(string eco)
    {
        eco = NormalizeEcoName(eco);
        if (string.IsNullOrEmpty(eco)) eco = "Ecosistema 1"; // fallback suave

        if (!_byEco.TryGetValue(eco, out var st))
        {
            st = new EcoState { eco = eco };
            _byEco[eco] = st;
        }
        return st;
    }

    void Push(string eco)
    {
        eco = NormalizeEcoName(eco);
        OnEcoStateChanged?.Invoke(eco, GetEco(eco));
        AchievementsManager.I?.OnMissionsUpdated();
    }

    void Load()
    {
        _byEco.Clear();
        try
        {
            if (!PlayerPrefs.HasKey(PP_KEY)) return;
            var json = PlayerPrefs.GetString(PP_KEY, "{}");
            var data = JsonUtility.FromJson<SaveData>(json);
            if (data?.ecos != null)
            {
                foreach (var e in data.ecos)
                {
                    if (e != null && !string.IsNullOrWhiteSpace(e.eco))
                        _byEco[NormalizeEcoName(e.eco)] = e;
                }
            }
        }
        catch (Exception ex) { Debug.LogWarning("[MissionManager] Load fail: " + ex.Message); }
    }

    void Save()
    {
        try
        {
            var data = new SaveData { ecos = new List<EcoState>(_byEco.Values) };
            var json = JsonUtility.ToJson(data);
            PlayerPrefs.SetString(PP_KEY, json);
            PlayerPrefs.Save();
        }
        catch (Exception ex) { Debug.LogWarning("[MissionManager] Save fail: " + ex.Message); }
    }

#if UNITY_EDITOR
    [ContextMenu("Reset MISIONS (local)")]
    void ResetMissionsLocal()
    {
        PlayerPrefs.DeleteKey(PP_KEY);
        _byEco.Clear();
        Debug.Log("[Mission] Misiones reseteadas localmente.");
        // Notifica para que la UI refresque
        foreach (var cfg in ecosystems)
            OnEcoStateChanged?.Invoke(cfg.ecosystemName, GetEco(cfg.ecosystemName));
    }
#endif
}
