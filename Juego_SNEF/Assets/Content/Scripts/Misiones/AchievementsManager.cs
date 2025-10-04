using System;
using System.Collections.Generic;
using UnityEngine;

/// Logros globales del juego.
///  - "Gamer": completar N tipos de minijuego (baseId distintos)
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
    public int gamerRequiredTypes = 4; // si hoy tienes 3, bájalo a 3
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
    const string PP_KEY = "SNEF_ACHIEVEMENTS_V1";
    State _state = new State();

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
        LoadState();
    }

    // ===================== Notificaciones (llámalas desde el juego) =====================

    /// Llamar cuando un minijuego se gana (>=1★). baseId = el miniGameId "base" de la Arcade.
    public void NotifyMinigameCompletedType(string baseId)
    {
        if (string.IsNullOrWhiteSpace(baseId)) return;
        if (!_state.completedMinigameTypes.Contains(baseId))
        {
            _state.completedMinigameTypes.Add(baseId);
            Debug.Log($"[Achievements] Minijuego tipo completado: {baseId}. ({_state.completedMinigameTypes.Count}/{gamerRequiredTypes})");
            SaveState();
            TryUnlockGamer();
        }
    }

    /// Llamar tras comprar/obtener un ítem (o al abrir la tienda) para verificar el conteo.
    public void OnInventoryChanged(int ownedNow)
    {
        if (totalCollectibles <= 0) return;
        Debug.Log($"[Achievements] Coleccionables: {ownedNow}/{totalCollectibles}");
        if (!_state.collector && ownedNow >= totalCollectibles) UnlockCollector();
    }

    /// Llamar cada vez que cambie el presupuesto actual del jugador.
    public void NotifyBudgetChanged(int newValue)
    {
        if (!_state.saver && newValue >= saverTarget) UnlockSaver();
    }

    /// MissionManager llamará esto cuando cambie cualquier misión.
    public void OnMissionsUpdated() => TryUnlockExpert();

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
        Debug.Log("[Achievements] LOGRO: Gamer (completados todos los minijuegos).");
        MetricsClient.I?.TrackLogroDesbloqueado(ach_Gamer_Id, "Gamer", "progreso", points_Gamer);
    }
    void UnlockCollector()
    {
        _state.collector = true; SaveState();
        Debug.Log("[Achievements] LOGRO: Coleccionista (todos los coleccionables).");
        MetricsClient.I?.TrackLogroDesbloqueado(ach_Collector_Id, "Coleccionista", "coleccionables", points_Collector);
    }
    void UnlockSaver()
    {
        _state.saver = true; SaveState();
        Debug.Log("[Achievements] LOGRO: Ahorrador (presupuesto objetivo alcanzado).");
        MetricsClient.I?.TrackLogroDesbloqueado(ach_Saver_Id, "Ahorrador", "economia", points_Saver);
    }
    void UnlockExpert()
    {
        _state.expert = true; SaveState();
        Debug.Log("[Achievements] LOGRO: Experto en finanzas (todos los ecosistemas completos).");
        MetricsClient.I?.TrackLogroDesbloqueado(ach_Expert_Id, "Experto en finanzas", "progreso", points_Expert);
    }

    // ================================ Save/Load ==============================

    void LoadState()
    {
        try
        {
            if (PlayerPrefs.HasKey(PP_KEY))
                _state = JsonUtility.FromJson<State>(PlayerPrefs.GetString(PP_KEY));
        }
        catch (Exception ex) { Debug.LogWarning("[Achievements] Load fail: " + ex.Message); }
        if (_state == null) _state = new State();
    }

    void SaveState()
    {
        try
        {
            PlayerPrefs.SetString(PP_KEY, JsonUtility.ToJson(_state));
            PlayerPrefs.Save();
        }
        catch (Exception ex) { Debug.LogWarning("[Achievements] Save fail: " + ex.Message); }
    }
}
