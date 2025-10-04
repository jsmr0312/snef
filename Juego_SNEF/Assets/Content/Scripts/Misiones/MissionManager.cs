using System;
using System.Collections.Generic;
using UnityEngine;

/// Misiones por ecosistema (independientes)
/// - Visita y completa N stands (no 'experience')  -> "Completa 4 stands"
/// - Visita y completa el punto de experiencia     -> "Completa punto de experiencia"
/// - Juega y consigue 3★ en un minijuego          -> "3 estrellas en minijuego"
public class MissionManager : MonoBehaviour
{
    public static MissionManager I { get; private set; }

    [Serializable]
    public class EcosystemConfig
    {
        public string ecosystemName = "Ecosistema 1";
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

    // --------- Estado ---------
    [Serializable]
    public class EcoState
    {
        public string eco;
        public List<string> standsCompleted = new List<string>();
        public bool experienceCompleted = false;
        public bool anyMinigame3Stars = false;
    }

    [Serializable]
    class SaveData { public List<EcoState> ecos = new List<EcoState>(); }

    const string PP_KEY = "SNEF_MISSIONS_V1";
    readonly Dictionary<string, EcoState> _byEco = new Dictionary<string, EcoState>(StringComparer.OrdinalIgnoreCase);

    public event Action<string, EcoState> OnEcoStateChanged;

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
        Load();
    }

    // ============================== API ======================================

    public void NotifyStandCompleted(string ecosystemName, string standId, string standType)
    {
        if (string.IsNullOrWhiteSpace(ecosystemName) || string.IsNullOrWhiteSpace(standId)) return;
        var st = GetEco(ecosystemName);

        if (string.Equals(standType, "experience", StringComparison.OrdinalIgnoreCase))
        {
            if (!st.experienceCompleted)
            {
                st.experienceCompleted = true;
                Debug.Log($"[Mission] ({ecosystemName}) Punto de experiencia COMPLETADO.");
                Save(); Push(ecosystemName);

                // Evento canónico
                MetricsClient.I?.TrackMisionCompletada(ecosystemName, "Completa punto de experiencia");
            }
            return;
        }

        bool beforeWasComplete = st.standsCompleted.Count >= Required(ecosystemName);

        if (!st.standsCompleted.Contains(standId))
        {
            st.standsCompleted.Add(standId);
            Debug.Log($"[Mission] ({ecosystemName}) Stand completado: {standId}. Total={st.standsCompleted.Count}");
            Save(); Push(ecosystemName);

            // Si acabamos de alcanzar el umbral -> misión completa
            if (!beforeWasComplete && st.standsCompleted.Count >= Required(ecosystemName))
                MetricsClient.I?.TrackMisionCompletada(ecosystemName, "Completa 4 stands");
        }
    }

    public void NotifyMinigameResult(string ecosystemName, int stars)
    {
        if (string.IsNullOrWhiteSpace(ecosystemName)) return;
        if (stars < 3) return;

        var st = GetEco(ecosystemName);
        if (!st.anyMinigame3Stars)
        {
            st.anyMinigame3Stars = true;
            Debug.Log($"[Mission] ({ecosystemName}) Misión: 3★ en minijuego COMPLETADA.");
            Save(); Push(ecosystemName);

            MetricsClient.I?.TrackMisionCompletada(ecosystemName, "3 estrellas en minijuego");
        }
    }

    public EcoState GetEcoState(string ecosystemName) => GetEco(ecosystemName);
    public bool IsComplete_4Stands(string eco) => GetEco(eco).standsCompleted.Count >= Required(eco);
    public bool IsComplete_Experience(string eco) => GetEco(eco).experienceCompleted;
    public bool IsComplete_Minigame3Stars(string eco) => GetEco(eco).anyMinigame3Stars;

    // ============================ Internals ==================================

    int Required(string eco)
    {
        foreach (var c in ecosystems)
            if (string.Equals(c.ecosystemName, eco, StringComparison.OrdinalIgnoreCase))
                return Mathf.Max(1, c.requiredStandCompletions);
        return 4;
    }

    EcoState GetEco(string eco)
    {
        if (!_byEco.TryGetValue(eco, out var st))
        {
            st = new EcoState { eco = eco };
            _byEco[eco] = st;
        }
        return st;
    }

    void Push(string eco)
    {
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
                foreach (var e in data.ecos)
                    if (e != null && !string.IsNullOrWhiteSpace(e.eco)) _byEco[e.eco] = e;
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
}
