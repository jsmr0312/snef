using UnityEngine;
using UnityEngine.SceneManagement;

public static class StatsSceneLogger
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnActiveSceneChanged(Scene prev, Scene next)
    {
        Print($"ActiveSceneChanged: {prev.name} ➜ {next.name}");
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Print($"SceneLoaded: {scene.name} ({mode})");
    }

    static void Print(string evt)
    {
        if (Stats.I == null)
        {
            Debug.LogWarning($"[StatsSceneLogger] {evt} | Stats.I = NULL");
            return;
        }

        Debug.Log($"[StatsSceneLogger] {evt} | Presupuesto={Stats.I.Presupuesto}  Puntaje={Stats.I.Puntaje}");
    }
}
