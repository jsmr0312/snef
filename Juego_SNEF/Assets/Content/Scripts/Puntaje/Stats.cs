// Stats.cs (REEMPLAZA por esta versión extendida)
using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class Stats : MonoBehaviour
{
    public event System.Action<int> OnBudgetChanged;
    public static Stats I { get; private set; }

    [Header("Valores")]
    [SerializeField] private int presupuesto = 0;
    [SerializeField] private int puntaje = 0;

    // --- Progreso por minijuego ---
    [Serializable]
    public class MinigameProgress
    {
        public int bestStars = 0;              // 0..3
        public float bestTime = float.PositiveInfinity; // menor es mejor
    }

    // No hace falta serializar en disco por ahora; vive en memoria entre escenas
    private readonly Dictionary<string, MinigameProgress> progresoMinijuegos =
        new Dictionary<string, MinigameProgress>();

    // Eventos para que la UI de cada escena se actualice
    public event Action<int> OnPresupuestoChanged;
    public event Action<int> OnPuntajeChanged;

    public int Presupuesto => presupuesto;
    public int Puntaje => puntaje;

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
        ForceRefresh();
    }



    // ---------- Presupuesto / Puntaje ----------
    public void AddPresupuesto(int cantidad)
    {
        if (cantidad == 0) return;
        presupuesto += cantidad;
        if (presupuesto < 0) presupuesto = 0;
        Debug.Log($"[Stats] AddPresupuesto({cantidad}) => {presupuesto}");
        OnPresupuestoChanged?.Invoke(presupuesto);
        AchievementsManager.I?.NotifyBudgetChanged(Presupuesto);

    }

    public void AddPuntaje(int cantidad)
    {
        if (cantidad == 0) return;
        puntaje += cantidad;
        if (puntaje < 0) puntaje = 0;
        Debug.Log($"[Stats] AddPuntaje({cantidad}) => {puntaje}");
        OnPuntajeChanged?.Invoke(puntaje);
        // dentro de Stats.AddPuntaje(int cantidad), después de actualizar 'puntaje':
        MetricsClient.I?.TrackPuntajeGeneralJugador(puntaje, cantidad);

    }

    public void ForceRefresh()
    {
        Debug.Log($"[Stats] ForceRefresh | Presupuesto={presupuesto}  Puntaje={puntaje}");
        OnPresupuestoChanged?.Invoke(presupuesto);
        OnPuntajeChanged?.Invoke(puntaje);
    }

    // ---------- Progreso de minijuegos ----------
    public MiniggameSnapshot GetProgress(string id)
    {
        var p = GetOrCreate(id);
        return new MiniggameSnapshot { bestStars = p.bestStars, bestTime = p.bestTime };
    }

    public struct MiniggameSnapshot
    {
        public int bestStars;
        public float bestTime;
    }

    MinigameProgress GetOrCreate(string id)
    {
        if (!progresoMinijuegos.TryGetValue(id, out var p))
        {
            p = new MinigameProgress();
            progresoMinijuegos[id] = p;
        }
        return p;
    }

    /// <summary>
    /// Registra el resultado de un minijuego y devuelve cuántos puntos/monedas
    /// se deben acreditar (solo la diferencia respecto al mejor histórico).
    /// puntosPorTotalEstrellas: array como [0, 100, 200, 300]
    /// </summary>
    public int RegisterMinigameResult(
        string id,
        int stars,
        float timeSeconds,
        int[] puntosPorTotalEstrellas,
        out bool improvedStars,
        out bool improvedTime)
    {
        var p = GetOrCreate(id);

        int prevStars = p.bestStars;
        float prevTime = p.bestTime;

        improvedStars = stars > prevStars;
        improvedTime = timeSeconds < prevTime;

        // Actualiza récords
        if (improvedStars) p.bestStars = stars;
        if (improvedTime) p.bestTime = timeSeconds;

        // Acredita solo la diferencia entre total nuevo y total anterior (por estrellas)
        int totalPrev = puntosPorTotalEstrellas[Mathf.Clamp(prevStars, 0, puntosPorTotalEstrellas.Length - 1)];
        int totalBestNow = puntosPorTotalEstrellas[Mathf.Clamp(p.bestStars, 0, puntosPorTotalEstrellas.Length - 1)];
        int delta = Mathf.Max(0, totalBestNow - totalPrev);
        return delta;
    }

    // Mantiene el HUD coherente en cada escena
    private void OnActiveSceneChanged(Scene prev, Scene next) => ForceRefresh();
    void OnEnable() { SceneManager.activeSceneChanged += OnActiveSceneChanged; }
    void OnDisable() { SceneManager.activeSceneChanged -= OnActiveSceneChanged; }
}
