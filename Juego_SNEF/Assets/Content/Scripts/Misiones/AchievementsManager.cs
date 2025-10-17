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

    void OnProgressChanged(ProgressCore.GameProgressV1 _)
    {
        RecheckFromGameState(); // ya tienes este método para revalidar en bloque
    }

    void OnTokenChanged(string _)
    {
        // Si reseteas cache/estado interno de achievements, hazlo aquí.
        // Luego revalidas según progreso actual:
        RecheckFromGameState();
    }

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
        LoadState();
    }

    void Start()
    {
        // Revisa el estado actual (presupuesto, inventario, misiones) y
        // rellena Gamer desde progreso si ya viene con minijuegos jugados.
        Invoke(nameof(RecheckFromGameState), 0.1f);
        RecheckFromGameState();
    }

    // ===================== Notificaciones públicas (llamadas del juego) =====================

    /// Llamar cuando un minijuego se gana (>=1★). baseId = el miniGameId "base" de la Arcade.
    public void NotifyMinigameCompletedType(string baseId)
    {
        if (string.IsNullOrWhiteSpace(baseId)) return;

        string key = baseId.Trim(); // 1 baseId = 1 tipo (ajusta si quieres agrupar)
        if (!_state.completedMinigameTypes.Contains(key))
        {
            _state.completedMinigameTypes.Add(key);
            Debug.Log($"[Achievements] Minijuego tipo completado: {key}. ({_state.completedMinigameTypes.Count}/{gamerRequiredTypes})");
            SaveState();
            TryUnlockGamer();
            Emit();
        }
    }

    /// Llamar tras comprar/obtener un ítem (o al abrir la tienda) para verificar el conteo.
    public void OnInventoryChanged(int ownedNow)
    {
        if (totalCollectibles <= 0) return;
        Debug.Log($"[Achievements] Coleccionables: {ownedNow}/{totalCollectibles}");
        if (!_state.collector && ownedNow >= totalCollectibles)
            UnlockCollector();
    }

    /// Llamar cada vez que cambie el presupuesto actual del jugador.
    public void NotifyBudgetChanged(int newValue)
    {
        if (!_state.saver && newValue >= saverTarget)
            UnlockSaver();
    }

    /// MissionManager llamará esto cuando cambie cualquier misión.
    public void OnMissionsUpdated()
    {
        TryUnlockExpert();
    }

    // ====================== Rehidratación desde progreso (reflexión segura) ======================

    public void RecheckFromGameState()
    {
        // 1) Rellenar tipos de minijuego completados desde el progreso cargado
        try
        {
            var set = new HashSet<string>(_state.completedMinigameTypes ?? new List<string>());
            foreach (var baseId in EnumerateCompletedMinigameBaseIdsFromProgress())
            {
                if (!set.Contains(baseId))
                {
                    set.Add(baseId);
                    NotifyMinigameCompletedType(baseId); // ya guarda y evalúa unlock
                }
            }
        }
        catch (Exception ex) { Debug.LogWarning("[Achievements] Refill Gamer types fail: " + ex.Message); }

        // 2) Presupuesto actual
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

    static string ExtractBaseType(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        int idx = name.IndexOf('_');
        if (idx > 0) return name.Substring(0, idx).Trim();
        return name.Trim();
    }

    IEnumerable<string> EnumerateCompletedMinigameBaseIdsFromProgress()
    {
        var pc = ProgressCore.I;
        if (pc == null) yield break;

        // Reflection: ProgressCore.I.Data.(progress.)stands[*].minigames[*].(minigame_name|id, puntaje|stars)
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

                        // stars/puntaje (>=1 cuenta como jugado; cámbialo a >=3 si quieres exigir 3★)
                        int stars = 0;
                        var pStars = mg.GetType().GetProperty("stars") ?? mg.GetType().GetProperty("puntaje");
                        if (pStars != null)
                        {
                            var val = pStars.GetValue(mg);
                            if (val is int i) stars = i;
                            else if (val is long l) stars = (int)l;
                        }
                        if (stars <= 0) continue;

                        // id/nombre
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

    void LoadState()
    {
        try
        {
            var key = CurrentUserKey();
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
