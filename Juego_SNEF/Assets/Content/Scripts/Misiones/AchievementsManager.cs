using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

/// Logros globales del juego (derivados de progreso):
///  - "Gamer": completar N TIPOS base de minijuego (baseId distintos)
///  - "Coleccionista": poseer todos los coleccionables (configurable)
///  - "Ahorrador": alcanzar saverTarget de presupuesto
///  - "Experto en finanzas": completar misiones de todos los ecosistemas
public class AchievementsManager : MonoBehaviour
{
    public static AchievementsManager I { get; private set; }

    [Header("IDs y puntos")]
    public string ach_Gamer_Id = "ach_gamer";
    public string ach_Collector_Id = "ach_collector";
    public string ach_Saver_Id = "ach_saver";
    public string ach_Expert_Id = "ach_expert";

    public int points_Gamer = 50;
    public int points_Collector = 50;
    public int points_Saver = 50;
    public int points_Expert = 100;

    [Header("Parámetros")]
    [Tooltip("Cuántos TIPOS base de minijuego se requieren para 'Gamer'")]
    public int gamerRequiredTypes = 4;
    [Tooltip("Total de coleccionables existentes (para 'Coleccionista')")]
    public int totalCollectibles = 0;
    [Tooltip("Meta de presupuesto para 'Ahorrador'")]
    public int saverTarget = 10000;

    // --------- Estado persistente ---------
    [Serializable]
    class State
    {
        public bool gamer, collector, saver, expert;
        public List<string> completedMinigameTypes = new List<string>(); // baseId únicos
    }

    const string PP_BASE_KEY = "SNEF_ACHIEVEMENTS_V1";
    State _state = new State();

    // --------- Evento para UI ---------
    public event Action OnChanged;
    void Emit() { try { OnChanged?.Invoke(); } catch { } }

    // --------- Properties para la UI ---------
    public bool Unlocked_Gamer => _state?.gamer ?? false;
    public bool Unlocked_Collector => _state?.collector ?? false;
    public bool Unlocked_Saver => _state?.saver ?? false;
    public bool Unlocked_Expert => _state?.expert ?? false;
    public int Gamer_TypesCompleted => _state?.completedMinigameTypes?.Count ?? 0;

    // ============================== Ciclo de vida ==============================
    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
        LoadState();
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
        // Evaluación inicial (por si el bootstrap ya corrió)
        Invoke(nameof(RecheckFromGameState), 0.05f);
        RecheckFromGameState();
    }

    void OnProgressChanged(ProgressCore.GameProgressV1 _)
    {
        RecheckFromGameState();
    }
    
    void OnTokenChanged(string _)
    {
        // Cambio de usuario → carga estado del usuario actual y reevalúa desde progreso remoto
        LoadState();
        Emit();
        RecheckFromGameState();
    }

    // ===================== Notificaciones públicas =====================

    /// Marca un tipo base de minijuego como completado (p. ej., "Minijuego1").
    public void NotifyMinigameCompletedType(string baseId)
    {
        if (string.IsNullOrWhiteSpace(baseId)) return;

        string key = baseId.Trim();
        if (!_state.completedMinigameTypes.Contains(key))
        {
            _state.completedMinigameTypes.Add(key);
            Debug.Log($"[Achievements] Minijuego tipo completado: {key}. ({_state.completedMinigameTypes.Count}/{gamerRequiredTypes})");
            SaveState();
            TryUnlockGamer();
            Emit();
        }
    }

    /// Llamar cuando cambie el inventario de coleccionables/tienda.
    public void OnInventoryChanged(int ownedNow)
    {
        if (totalCollectibles <= 0) return;
        Debug.Log($"[Achievements] Coleccionables: {ownedNow}/{totalCollectibles}");
        if (!_state.collector && ownedNow >= totalCollectibles)
            UnlockCollector();
    }

    /// Llamar al cambiar presupuesto (o rehidratar desde Stats).
    public void NotifyBudgetChanged(int newValue)
    {
        if (!_state.saver && newValue >= saverTarget)
            UnlockSaver();
    }

    /// Llamar cuando MissionManager actualice su estado (stands/xp/3★ por ecosistema).
    public void OnMissionsUpdated()
    {
        TryUnlockExpert();
    }

    // ====================== Rehidratación desde progreso ======================

    public void RecheckFromGameState()
    {
        if (CurrentUserKey() != _loadedKey) LoadState();
        // 1) Rellenar tipos de minijuego completados desde el progreso cargado
        try
        {
            var set = new HashSet<string>(_state.completedMinigameTypes ?? new List<string>());
            foreach (var baseId in EnumerateCompletedMinigameBaseIdsFromProgress())
            {
                if (!set.Contains(baseId))
                {
                    set.Add(baseId);
                    // Usa el mismo flujo de notificación para mantener persistencia/eventos
                    NotifyMinigameCompletedType(baseId);
                }
            }
        }
        catch (Exception ex) { Debug.LogWarning("[Achievements] Refill Gamer types fail: " + ex.Message); }

        // 2) Presupuesto actual (si hay Stats)
        if (Stats.I != null)
            NotifyBudgetChanged(Stats.I.Presupuesto);

        // 3) Coleccionables actuales
        int owned = (ProgressCore.I?.Progress?.owned_items != null)
            ? ProgressCore.I.Progress.owned_items.Count
            : 0;
        OnInventoryChanged(owned);

        // 4) Misiones → por si ya están completas cuando abres la pantalla
        OnMissionsUpdated();
    }

    // Extrae el "tipo base" de un id de minijuego como "Minijuego4_E1" → "Minijuego4".
    static string ExtractBaseType(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        int idx = name.IndexOf('_');
        if (idx > 0) return name.Substring(0, idx).Trim();
        return name.Trim();
    }

    /// Recorre el progreso actual y devuelve baseIds de minijuegos con estrellas > 0.
    IEnumerable<string> EnumerateCompletedMinigameBaseIdsFromProgress()
    {
        var pc = ProgressCore.I;
        if (pc == null) yield break;

        // Preferir ProgressCore.Data.minigames (fuente consolidada y estable)
        var minis = pc.Data?.minigames;
        if (minis != null && minis.Count > 0)
        {
            foreach (var m in minis)
            {
                if (m == null || m.stars <= 0 || string.IsNullOrEmpty(m.id)) continue;

                // id esperado: "<standId>::<minigameIdBase>"
                var parts = m.id.Split(new[] { "::" }, StringSplitOptions.None);
                string mg = parts.Length >= 2 ? parts[1] : m.id;
                var baseId = ExtractBaseType(mg);
                if (!string.IsNullOrEmpty(baseId))
                    yield return baseId;
            }
            yield break; // ya cubrimos la fuente principal
        }

        // Fallback: revisar stands[].minigames del bootstrap remoto
        object data = pc.GetType().GetProperty("Data", BindingFlags.Public | BindingFlags.Instance)?.GetValue(pc);
        if (data == null) yield break;

        object standsObj =
            data.GetType().GetProperty("stands")?.GetValue(data) ??
            data.GetType().GetProperty("progress")?.GetValue(data)?.GetType().GetProperty("stands")?.GetValue(
                data.GetType().GetProperty("progress")?.GetValue(data));

        if (standsObj is System.Collections.IEnumerable standsEnum)
        {
            foreach (var st in standsEnum)
            {
                var minigamesObj = st?.GetType().GetProperty("minigames")?.GetValue(st);
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
                            if (val is int i) stars = i;
                            else if (val is long l) stars = (int)l;
                        }
                        if (stars <= 0) continue;

                        string id = null;
                        var pName = mg.GetType().GetProperty("minigame_name") ?? mg.GetType().GetProperty("id") ?? mg.GetType().GetProperty("name");
                        if (pName != null)
                        {
                            id = pName.GetValue(mg)?.ToString();
                            var baseId = ExtractBaseType(id);
                            if (!string.IsNullOrEmpty(baseId))
                                yield return baseId;
                        }
                    }
                }
            }
        }
    }

    // ========================= Chequeos individuales =========================

    void TryUnlockGamer()
    {
        if (!_state.gamer && _state.completedMinigameTypes.Count >= Mathf.Max(1, gamerRequiredTypes))
            UnlockGamer();
    }

    void TryUnlockExpert()
    {
        if (_state.expert) return;
        if (MissionManager.I == null) return;

        int ok = 0;
        foreach (var cfg in MissionManager.I.ecosystems)
        {
            var eco = cfg.ecosystemName;
            bool standsOK = MissionManager.I.IsComplete_4Stands(eco);
            bool xpOK = MissionManager.I.IsComplete_Experience(eco);
            if (standsOK && xpOK) ok++;
        }

        Debug.Log($"[Achievements] Expert progress: {ok}/{MissionManager.I.ecosystems.Length} ecos OK.");
        if (ok >= MissionManager.I.ecosystems.Length)
            UnlockExpert();
    }

    // ============================ Unlock helpers =============================

    void UnlockGamer()
    {
        _state.gamer = true; SaveState();
        Debug.Log("[Achievements] LOGRO: Gamer (tipos de minijuego cumplidos).");
        MetricsClient.I?.TrackLogroDesbloqueado(ach_Gamer_Id, "Gamer", "progreso", points_Gamer);
        Emit();
    }

    void UnlockCollector()
    {
        _state.collector = true; SaveState();
        Debug.Log("[Achievements] LOGRO: Coleccionista (todos los coleccionables).");
        MetricsClient.I?.TrackLogroDesbloqueado(ach_Collector_Id, "Coleccionista", "coleccionables", points_Collector);
        Emit();
    }

    void UnlockSaver()
    {
        _state.saver = true; SaveState();
        Debug.Log("[Achievements] LOGRO: Ahorrador (presupuesto objetivo alcanzado).");
        MetricsClient.I?.TrackLogroDesbloqueado(ach_Saver_Id, "Ahorrador", "economia", points_Saver);
        Emit();
    }

    void UnlockExpert()
    {
        _state.expert = true; SaveState();
        Debug.Log("[Achievements] LOGRO: Experto en finanzas (todos los ecosistemas completos).");
        MetricsClient.I?.TrackLogroDesbloqueado(ach_Expert_Id, "Experto en finanzas", "progreso", points_Expert);
        Emit();
    }

    // ================================ Save/Load ==============================

    static string CurrentUserKey()
    {
        string uid = JwtLite.GetUserId(WebGLBridge.Token);
        if (string.IsNullOrEmpty(uid)) uid = "guest";
        return $"{PP_BASE_KEY}::{uid}";
    }

    string _loadedKey;

    void LoadState()
    {
        try
        {
            var key = CurrentUserKey();
            _loadedKey = key; // <-- recuerda
            if (PlayerPrefs.HasKey(key))
                _state = JsonUtility.FromJson<State>(PlayerPrefs.GetString(key));
        }
        catch (Exception ex) { Debug.LogWarning("[Achievements] Load fail: " + ex.Message); }
        if (_state == null) _state = new State();
    }

    void SaveState()
    {
        try
        {
            var key = CurrentUserKey();
            PlayerPrefs.SetString(key, JsonUtility.ToJson(_state));
            PlayerPrefs.Save();
        }
        catch (Exception ex) { Debug.LogWarning("[Achievements] Save fail: " + ex.Message); }
    }

    public void ResetCurrentUserAchievements()
    {
        try
        {
            string key = CurrentUserKey();
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            _state = new State();
            Emit();
            Debug.Log("[Achievements] Reset de logros para el usuario actual.");
        }
        catch (Exception ex) { Debug.LogWarning("[Achievements] Reset fail: " + ex.Message); }
    }

    // ======== NUEVO: Aplicar bootstrap declarativo de logros ========
    public void ApplyBootstrapAchievementsFromDtos(System.Collections.IEnumerable list)
    {
        if (list == null) return;

        bool changed = false;
        foreach (var a in list)
        {
            if (a == null) continue;
            string id = a.GetType().GetProperty("achievement_id")?.GetValue(a)?.ToString();
            string nm = a.GetType().GetProperty("name")?.GetValue(a)?.ToString();
            bool on = (a.GetType().GetProperty("status")?.GetValue(a) is bool b && b);
            if (!on)
            {
                var atProp = a.GetType().GetProperty("at") ?? a.GetType().GetProperty("unlocked_at");
                var atVal = atProp?.GetValue(a)?.ToString();
                if (!string.IsNullOrEmpty(atVal)) on = true;
            }
            if (!on)
            {
                var puntosProp = a.GetType().GetProperty("puntos");
                int pv = 0; if (puntosProp != null) { var v = puntosProp.GetValue(a); if (v is int i) pv = i; else if (v is long l) pv = (int)l; }
                if (pv > 0) on = true;
            }
            if (!on) continue;
            id = string.IsNullOrEmpty(id) ? (nm ?? "").ToLowerInvariant() : id.ToLowerInvariant();

            if ((id.Contains("gamer")) && !_state.gamer) { _state.gamer = true; changed = true; }
            if ((id.Contains("saver") || id.Contains("ahorrador")) && !_state.saver) { _state.saver = true; changed = true; }
            if ((id.Contains("collector") || id.Contains("coleccionista")) && !_state.collector) { _state.collector = true; changed = true; }
            if ((id.Contains("expert") || id.Contains("experto")) && !_state.expert) { _state.expert = true; changed = true; }
        }
        if (changed) { SaveState(); Emit(); }
    }

#if UNITY_EDITOR
    [ContextMenu("Reset ACHIEVEMENTS (local)")]
    void ResetAchievementsLocal()
    {
        PlayerPrefs.DeleteKey(CurrentUserKey());
        _state = new State();
        Debug.Log("[Achievements] Estado reseteado localmente.");
        Emit();
    }
#endif
}
