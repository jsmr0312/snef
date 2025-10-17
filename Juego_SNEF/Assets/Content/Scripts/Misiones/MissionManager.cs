using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

/// Misiones por ecosistema (independientes y DERIVADAS):
///  - Completar N stands REGULARES (Master/Premier/Excellence)
///  - Completar el Punto de Experiencia (Experience)
///  - Conseguir 3★ en un minijuego (cualquiera del ecosistema)
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

    // --------- Estado persistente local (opcional para recordar UI) ---------
    [Serializable]
    public class EcoState
    {
        public string eco;
        public List<string> standsCompleted = new List<string>(); // REGULARES (ids únicos)
        public bool experienceCompleted = false;                   // Punto de experiencia
        public bool anyMinigame3Stars = false;                     // >=3★ en cualquier minijuego del eco
    }

    [Serializable]
    class SaveData { public List<EcoState> ecos = new List<EcoState>(); }

    const string PP_KEY = "SNEF_MISSIONS_V1";
    readonly Dictionary<string, EcoState> _byEco =
        new Dictionary<string, EcoState>(StringComparer.OrdinalIgnoreCase);

    public event Action<string, EcoState> OnEcoStateChanged;

    // Guardia para evitar recomputes mientras estamos procesando eventos locales
    bool _suppressRecompute;

    // ============================== Ciclo de vida ==============================

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
        Load();
    }

    void OnEnable()
    {
        if (ProgressCore.I != null)
            ProgressCore.I.OnChanged += OnProgressChanged;

        WebGLBridge.OnTokenChanged += OnTokenChanged;
    }

    void OnDisable()
    {
        if (ProgressCore.I != null)
            ProgressCore.I.OnChanged -= OnProgressChanged;

        WebGLBridge.OnTokenChanged -= OnTokenChanged;
    }

    void Start()
    {
        // Primer frame: refleja lo que ya haya (por si el bootstrap corrió antes)
        TryRecomputeAndPush();
    }

    void OnProgressChanged(ProgressCore.GameProgressV1 _)
    {
        if (_suppressRecompute) return;
        TryRecomputeAndPush(); // ahora fusiona, no borra
    }

    void OnTokenChanged(string _)
    {
        // Cambió de usuario → recomputar limpio desde Data y refrescar UI
        TryRecomputeAndPush();
    }

    void TryRecomputeAndPush()
    {
        RecomputeFromProgressFromProgressCore(mergeWithLocal: true);
        PushAllConfigured();
    }

    // ============================== API en vivo ==============================

    public void NotifyStandCompleted(string ecosystemName, string standId, string standType)
    {
        _suppressRecompute = true;

        ecosystemName = ResolveEcoByIdOrName(ecosystemName, standId);
        if (string.IsNullOrEmpty(ecosystemName))
        {
            Debug.LogWarning($"[Mission] No se pudo resolver ecosistema para standId={standId}. Revisa StandCatalog/EcosystemBootstrap.");
            _suppressRecompute = false;
            return;
        }

        var st = GetEco(ecosystemName);

        // Resolver tipo: catálogo → texto → regular
        StandKind kind = ParseStandType(standType);
        if (StandCatalog.I && StandCatalog.I.TryGet(standId, out var entry))
            kind = entry.kind;

        if (kind == StandKind.Experience)
        {
            if (!st.experienceCompleted)
            {
                st.experienceCompleted = true;
                Debug.Log($"[Mission] ({ecosystemName}) Punto de experiencia COMPLETADO. standId={standId}");
                Save(); Push(ecosystemName);
                MetricsClient.I?.TrackMisionCompletada(ecosystemName, "Completa punto de experiencia");
            }
            _suppressRecompute = false;
            return;
        }

        bool beforeWasComplete = st.standsCompleted.Count >= Required(ecosystemName);

        if (!st.standsCompleted.Contains(standId))
        {
            st.standsCompleted.Add(standId);
            // Asegura unicidad por si algo raro pasó
            st.standsCompleted = st.standsCompleted.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            Debug.Log($"[Mission] ({ecosystemName}) Stand REGULAR COMPLETADO: {standId}. Total={st.standsCompleted.Count}");
            Save(); Push(ecosystemName);

            if (!beforeWasComplete && st.standsCompleted.Count >= Required(ecosystemName))
                MetricsClient.I?.TrackMisionCompletada(ecosystemName, "Completa 4 stands");
        }

        _suppressRecompute = false;
    }

    public void NotifyMinigameResultByStand(string standId, int stars)
    {
        if (stars < 3) return; // la misión exige 3★

        // Catálogo → fallback StandContext
        string eco = null;
        if (StandCatalog.I && StandCatalog.I.TryGet(standId, out var entry) && !string.IsNullOrWhiteSpace(entry.ecosystemName))
            eco = entry.ecosystemName;
        else if (StandContext.I != null && !string.IsNullOrWhiteSpace(StandContext.I.ecosystemName))
            eco = StandContext.I.ecosystemName;

        eco = NormalizeEcoName(eco);

        if (string.IsNullOrEmpty(eco))
        {
            Debug.LogWarning($"[Mission] No se pudo resolver ecosistema para standId={standId} (3★). Revisa StandCatalog o coloca EcosystemBootstrap en la escena.");
            return;
        }

        var st = GetEco(eco);
        if (!st.anyMinigame3Stars)
        {
            st.anyMinigame3Stars = true;
            Debug.Log($"[Mission] ({eco}) Misión: 3★ en minijuego COMPLETADA.");
            Save();
            Push(eco);
            MetricsClient.I?.TrackMisionCompletada(eco, "3 estrellas en minijuego");
        }
    }

    /// Snapshot del ecosistema (para UI)
    public EcoState GetEcoState(string ecosystemName) => GetEco(NormalizeEcoName(ecosystemName));

    public bool IsComplete_4Stands(string eco) => GetEco(NormalizeEcoName(eco)).standsCompleted.Count >= Required(NormalizeEcoName(eco));
    public bool IsComplete_Experience(string eco) => GetEco(NormalizeEcoName(eco)).experienceCompleted;
    public bool IsComplete_Minigame3Stars(string eco) => GetEco(NormalizeEcoName(eco)).anyMinigame3Stars;

    // ====================== Recomputar TODO desde el progreso (bootstrap) ======================

    /// Reconstruye a partir de ProgressCore.I.Data (o Data.progress).
    /// IMPORTANTE: si mergeWithLocal=true, fusiona con lo que ya esté en memoria
    /// para no perder los avances recién hechos que aún no aparecen en el JSON.
    public void RecomputeFromProgressFromProgressCore(bool mergeWithLocal = true)
    {
        var pc = ProgressCore.I;
        if (pc == null) { return; }

        object data = pc.GetType().GetProperty("Data", BindingFlags.Public | BindingFlags.Instance)?.GetValue(pc);
        if (data == null) { return; }

        object progressNode = data;
        var pProgress = data.GetType().GetProperty("progress");
        if (pProgress != null) progressNode = pProgress.GetValue(data) ?? data;

        var pStands = progressNode.GetType().GetProperty("stands") ?? data.GetType().GetProperty("stands");
        object standsObj = pStands?.GetValue(progressNode) ?? pStands?.GetValue(data);

        // Construye un estado NUEVO (por ecosistema) a partir del JSON
        var newByEco = new Dictionary<string, EcoState>(StringComparer.OrdinalIgnoreCase);

        if (standsObj is System.Collections.IEnumerable standsEnum)
        {
            foreach (var st in standsEnum)
            {
                if (st == null) continue;

                string standId = st.GetType().GetProperty("stand_id")?.GetValue(st)?.ToString();
                if (string.IsNullOrWhiteSpace(standId)) continue;

                // Resolver entrada de catálogo
                string ecoName = null;
                StandKind kind = StandKind.Regular;
                if (StandCatalog.I && StandCatalog.I.TryGet(standId, out var entry))
                {
                    ecoName = entry.ecosystemName;
                    kind = entry.kind;
                }
                else if (StandContext.I != null && !string.IsNullOrWhiteSpace(StandContext.I.ecosystemName))
                {
                    ecoName = StandContext.I.ecosystemName; // fallback
                }

                ecoName = NormalizeEcoName(ecoName);
                if (string.IsNullOrEmpty(ecoName)) ecoName = "Ecosistema 1";

                if (!newByEco.TryGetValue(ecoName, out var ecoState))
                {
                    ecoState = new EcoState { eco = ecoName };
                    newByEco[ecoName] = ecoState;
                }

                // ¿Stand "completado"? Regla: algún minijuego con 3★
                bool completed = false;
                bool any3 = false;

                var minigamesObj = st.GetType().GetProperty("minigames")?.GetValue(st);
                if (minigamesObj is System.Collections.IEnumerable mgEnum)
                {
                    foreach (var mg in mgEnum)
                    {
                        if (mg == null) continue;
                        int stars = 0;
                        var pStars = mg.GetType().GetProperty("stars") ?? mg.GetType().GetProperty("puntaje");
                        if (pStars != null)
                        {
                            var val = pStars.GetValue(mg);
                            if (val is int i) stars = i; else if (val is long l) stars = (int)l;
                        }
                        if (stars >= 3) any3 = true;
                        if (stars >= 3) completed = true;
                    }
                }

                if (kind == StandKind.Experience)
                {
                    // XP completo si hubo cualquier progreso
                    bool anyProgress = completed
                        || (st.GetType().GetProperty("trivias")?.GetValue(st) is System.Collections.IEnumerable te && te.GetEnumerator().MoveNext())
                        || (st.GetType().GetProperty("assets")?.GetValue(st) is System.Collections.IEnumerable ae && ae.GetEnumerator().MoveNext());

                    if (anyProgress) ecoState.experienceCompleted = true;
                }
                else
                {
                    if (completed && !ecoState.standsCompleted.Contains(standId))
                        ecoState.standsCompleted.Add(standId);
                    if (any3) ecoState.anyMinigame3Stars = true;
                }
            }
        }

        // FUSIÓN con el estado local (para no perder sesiones recientes)
        foreach (var cfg in ecosystems)
        {
            string eco = NormalizeEcoName(cfg.ecosystemName);

            var fromJson = newByEco.TryGetValue(eco, out var nj) ? nj : new EcoState { eco = eco };
            var local = _byEco.TryGetValue(eco, out var lc) ? lc : new EcoState { eco = eco };

            var merged = new EcoState { eco = eco };

            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (mergeWithLocal && local.standsCompleted != null)
                foreach (var id in local.standsCompleted) set.Add(id);
            if (fromJson.standsCompleted != null)
                foreach (var id in fromJson.standsCompleted) set.Add(id);

            merged.standsCompleted = set.ToList();
            merged.experienceCompleted = (mergeWithLocal && local.experienceCompleted) || fromJson.experienceCompleted;
            merged.anyMinigame3Stars = (mergeWithLocal && local.anyMinigame3Stars) || fromJson.anyMinigame3Stars;

            _byEco[eco] = merged;
        }

        Save();
        Debug.Log("[Mission] Recomputadas (fusionadas) desde progreso.");
    }

    // ============================ Internals ==================================

    static string NormalizeEcoName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        raw = raw.Trim();
        var m = Regex.Match(raw, @"ecosistema\s*[-_ ]?\s*(\d+)", RegexOptions.IgnoreCase);
        if (m.Success) return $"Ecosistema {m.Groups[1].Value}";
        return raw;
    }

    static StandKind ParseStandType(string type)
    {
        if (string.IsNullOrWhiteSpace(type)) return StandKind.Regular;
        var t = type.Trim().ToLowerInvariant();
        if (t.Contains("punto de experiencia") || t.Contains("experiencia")) return StandKind.Experience;
        if (t == "experience" || t.Contains("experience point")) return StandKind.Experience;
        return StandKind.Regular; // Master / Premier / Excellence -> Regular
    }

    string ResolveEcoByIdOrName(string ecosystemName, string standId)
    {
        // 1) Si viene nombre válido y coincide con config, úsalo
        var eco = NormalizeEcoName(ecosystemName);
        foreach (var c in ecosystems)
            if (string.Equals(c.ecosystemName, eco, StringComparison.OrdinalIgnoreCase))
                return eco;

        // 2) Catálogo por id
        if (!string.IsNullOrEmpty(standId) && StandCatalog.I && StandCatalog.I.TryGet(standId, out var entry)
            && !string.IsNullOrWhiteSpace(entry.ecosystemName))
            return NormalizeEcoName(entry.ecosystemName);

        // 3) Fallback al contexto de escena
        if (StandContext.I != null && !string.IsNullOrWhiteSpace(StandContext.I.ecosystemName))
            return NormalizeEcoName(StandContext.I.ecosystemName);

        return eco; // puede venir vacío → lo manejarán los defaults
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
        if (string.IsNullOrEmpty(eco)) eco = "Ecosistema 1";
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

    void PushAllConfigured()
    {
        foreach (var cfg in ecosystems)
            Push(cfg.ecosystemName);
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
        PushAllConfigured();
    }
#endif
}
