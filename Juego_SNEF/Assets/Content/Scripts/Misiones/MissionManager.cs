using System;
using System.Collections.Generic;
using System.Linq;
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

    // --------- Estado persistente local (para UI y merge) ---------
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
    static string CurrentUserKey()
    {
        string uid = JwtLite.GetUserId(WebGLBridge.Token);
        if (string.IsNullOrEmpty(uid)) uid = "guest";
        return $"{PP_KEY}::{uid}";
    }

    readonly Dictionary<string, EcoState> _byEco =
        new Dictionary<string, EcoState>(StringComparer.OrdinalIgnoreCase);

    public event Action<string, EcoState> OnEcoStateChanged;

    // Guardia para evitar recomputes mientras procesamos eventos locales
    bool _suppressRecompute;

    // Recordar qué clave cargamos (para detectar cambio de usuario)
    string _loadedKey;

    // ============================== Ciclo de vida ==============================

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
        Load();
    }

    public void ResetCurrentUserMissions()
    {
        try
        {
            var key = CurrentUserKey();             // SNEF_MISSIONS_V1::<uid>
            PlayerPrefs.DeleteKey(key);             // borra solo las del usuario ACTUAL
            PlayerPrefs.Save();
        }
        catch { /* seguro */ }

        _byEco.Clear();                             // limpia caché en memoria
        Debug.Log("[Mission] Reset de misiones para el usuario actual.");
        PushAllConfigured();                        // refresca UI/observadores
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
        // Recompute inmediato
        TryRecomputeAndPush();

        // Y otro un poquito después para asegurar que StandCatalog.I ya esté listo
        Invoke(nameof(TryRecomputeAndPush), 0.05f);
    }

    void OnProgressChanged(ProgressCore.GameProgressV1 _)
    {
        if (_suppressRecompute)
        {
            // Programa un recompute en el próximo frame para no perder el evento
            CancelInvoke(nameof(TryRecomputeAndPush));
            Invoke(nameof(TryRecomputeAndPush), 0.01f);
            return;
        }
        TryRecomputeAndPush();
    }

    void OnTokenChanged(string _)
    {
        // Cambió de usuario → carga su estado y recalcula SOLO con progreso remoto
        Load();
        RecomputeFromProgressFromProgressCore(mergeWithLocal: false);
        PushAllConfigured();
    }

    void TryRecomputeAndPush()
    {
        var keyNow = CurrentUserKey();
        bool sameUser = (keyNow == _loadedKey);
        RecomputeFromProgressFromProgressCore(mergeWithLocal: sameUser); // solo fusiona si es el mismo usuario
        PushAllConfigured();
    }

    // ============================== API en vivo ==============================

    public void NotifyStandCompleted(string ecosystemName, string standId, string standType)
    {
        // Evita que un throw deje el flag prendido
        bool prev = _suppressRecompute;
        _suppressRecompute = true;
        try
        {
            ecosystemName = ResolveEcoByIdOrName(ecosystemName, standId);
            if (string.IsNullOrEmpty(ecosystemName))
            {
                Debug.LogWarning($"[Mission] No se pudo resolver ecosistema para standId={standId}. Revisa StandCatalog/EcosystemBootstrap.");
                return;
            }

            var st = GetEco(ecosystemName);

            StandKind kind = ParseStandType(standType);
            if (StandCatalog.I && StandCatalog.I.TryGet(standId, out var entry))
                kind = entry.kind;

            if (kind == StandKind.Experience)
            {
                if (!st.experienceCompleted)
                {
                    st.experienceCompleted = true;
                    Save(); Push(ecosystemName);
                    MetricsClient.I?.TrackMisionCompletada(ecosystemName, "Completa punto de experiencia");
                }
                return;
            }

            bool beforeWasComplete = st.standsCompleted.Count >= Required(ecosystemName);

            if (!st.standsCompleted.Contains(standId))
            {
                st.standsCompleted.Add(standId);
                st.standsCompleted = st.standsCompleted.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                Save(); Push(ecosystemName);

                if (!beforeWasComplete && st.standsCompleted.Count >= Required(ecosystemName))
                    MetricsClient.I?.TrackMisionCompletada(ecosystemName, "Completa 4 stands");
            }
        }
        finally
        {
            _suppressRecompute = prev; // ← SIEMPRE restaurar
        }
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

    public void RecomputeFromProgressFromProgressCore(bool mergeWithLocal = true)
    {
        var pc = ProgressCore.I;
        if (pc == null) return;

        // Construye un estado NUEVO (por ecosistema) a partir del modelo ACTUAL
        var newByEco = new Dictionary<string, EcoState>(StringComparer.OrdinalIgnoreCase);

        // --- A) Derivar por minijuegos ya guardados en ProgressCore (fuente para 3★ + stands regulares)
        if (pc.Data?.minigames != null)
        {
            foreach (var mg in pc.Data.minigames)
            {
                if (mg == null || mg.stars < 3 || string.IsNullOrEmpty(mg.id)) continue;
                // id = "<standId>::<minigameId>"
                var parts = mg.id.Split(new[] { "::" }, StringSplitOptions.None);
                if (parts.Length < 2) continue;
                var standId = parts[0];

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

                if (kind != StandKind.Experience)
                {
                    if (!ecoState.standsCompleted.Contains(standId))
                        ecoState.standsCompleted.Add(standId);
                    ecoState.anyMinigame3Stars = true;
                }
            }
        }

        // --- B) Derivar experiencia completada: usa stands tipados (fase/vistas)
        if (pc.Data?.stands != null)
        {
            foreach (var s in pc.Data.stands)
            {
                if (s == null || string.IsNullOrEmpty(s.stand_id)) continue;

                string ecoName = null;
                StandKind kind = StandKind.Regular;
                if (StandCatalog.I && StandCatalog.I.TryGet(s.stand_id, out var entry))
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

                if (kind == StandKind.Experience)
                {
                    bool anyProgress =
                        (s.viewed_screens != null && s.viewed_screens.Count > 0) ||
                        string.Equals(s.phase, "Final", StringComparison.OrdinalIgnoreCase);
                    if (anyProgress) ecoState.experienceCompleted = true;
                }
            }
        }

        // --- C) **Declarativas desde backend** (pc.Data.missions)
        if (pc.Data?.missions != null)
        {
            foreach (var m in pc.Data.missions)
            {
                if (m == null) continue;

                // id formato: "Ecosistema N::Nombre de misión"
                string ecoRaw = null;
                string name = null;

                if (!string.IsNullOrEmpty(m.id) && m.id.Contains("::"))
                {
                    var parts = m.id.Split(new[] { "::" }, StringSplitOptions.None);
                    ecoRaw = parts.Length > 0 ? parts[0] : null;
                    name = parts.Length > 1 ? parts[1] : null;
                }

                ecoRaw = NormalizeEcoName(ecoRaw);
                if (string.IsNullOrEmpty(ecoRaw)) ecoRaw = "Ecosistema 1";

                if (!newByEco.TryGetValue(ecoRaw, out var ecoState))
                {
                    ecoState = new EcoState { eco = ecoRaw };
                    newByEco[ecoRaw] = ecoState;
                }

                var n = (name ?? "").ToLowerInvariant();
                bool isDone = string.Equals(m.state, "done", StringComparison.OrdinalIgnoreCase);

                if (!isDone) continue;

                if (n.Contains("4 stands"))
                {
                    // Satisface el conteo mínimo con marcadores lógicos si hace falta
                    int req = Required(ecoRaw);
                    while (ecoState.standsCompleted.Count < req)
                        ecoState.standsCompleted.Add($"remote_{ecoState.standsCompleted.Count + 1}");
                }
                else if (n.Contains("punto de experiencia") || n.Contains("experience"))
                {
                    ecoState.experienceCompleted = true;
                }
                else if (n.Contains("3 estrellas") || n.Contains("3★") || n.Contains("3 *") || n.Contains("3*"))
                {
                    ecoState.anyMinigame3Stars = true;
                }
            }
        }

        // --- D) FUSIÓN con el estado local (para no perder sesiones recientes)
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
        Debug.Log("[Mission] Recomputadas desde progreso (merge=" + mergeWithLocal + ").");
    }

    // ============================ Internals ==================================

    public static string NormalizeEcoName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        raw = raw.Trim();
        var m = Regex.Match(raw, @"ecosistema\s*[-_ ]?\s*(\n?\d+|\d+)", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            var digits = Regex.Match(raw, @"(\d+)");
            if (digits.Success) return $"Ecosistema {digits.Groups[1].Value}";
        }
        var m2 = Regex.Match(raw, @"ecosistema\s*(\d+)", RegexOptions.IgnoreCase);
        if (m2.Success) return $"Ecosistema {m2.Groups[1].Value}";
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
            var key = CurrentUserKey();
            _loadedKey = key; // <-- recuerda qué clave cargaste
            if (!PlayerPrefs.HasKey(key)) return;
            var json = PlayerPrefs.GetString(key, "{}");
            var data = JsonUtility.FromJson<SaveData>(json);
            if (data?.ecos != null)
            {
                foreach (var e in data.ecos)
                    if (e != null && !string.IsNullOrWhiteSpace(e.eco))
                        _byEco[NormalizeEcoName(e.eco)] = e;
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
            var key = CurrentUserKey();
            PlayerPrefs.SetString(key, json);
            PlayerPrefs.Save();
        }
        catch (Exception ex) { Debug.LogWarning("[MissionManager] Save fail: " + ex.Message); }
    }

    // ======== Bootstrap declarativo desde DTOs (opcional) ========
    public void ApplyBootstrapMissionsFromDtos(System.Collections.IEnumerable missions)
    {
        if (missions == null) return;

        bool any = false;
        foreach (var m in missions)
        {
            if (m == null) continue;
            var ecoRaw = m.GetType().GetProperty("ecosystem_name")?.GetValue(m)?.ToString();
            var name = m.GetType().GetProperty("mision_name")?.GetValue(m)?.ToString();
            var doneObj = m.GetType().GetProperty("done")?.GetValue(m);
            bool done = (doneObj is bool b && b);
            if (!done) continue;

            var eco = NormalizeEcoName(ecoRaw);
            if (string.IsNullOrEmpty(eco)) eco = "Ecosistema 1";
            var st = GetEco(eco);

            if (string.IsNullOrWhiteSpace(name)) continue;
            var n = name.ToLowerInvariant();

            if (n.Contains("4 stands"))
            {
                int req = Required(eco);
                // crear marcadores lógicos para cumplir el conteo
                while (st.standsCompleted.Count < req)
                    st.standsCompleted.Add($"remote_{st.standsCompleted.Count + 1}");
                any = true;
            }
            else if (n.Contains("punto de experiencia") || n.Contains("experience"))
            {
                st.experienceCompleted = true; any = true;
            }
            else if (n.Contains("3 estrellas") || n.Contains("3★") || n.Contains("3 *") || n.Contains("3*"))
            {
                st.anyMinigame3Stars = true; any = true;
            }
        }

        if (any) { Save(); PushAllConfigured(); }
    }

#if UNITY_EDITOR
    [ContextMenu("Reset MISIONS (local)")]
    void ResetMissionsLocal()
    {
        PlayerPrefs.DeleteKey(CurrentUserKey());
        _byEco.Clear();
        Debug.Log("[Mission] Misiones reseteadas localmente para usuario actual.");
        PushAllConfigured();
    }
#endif
}
