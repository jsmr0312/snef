using UnityEngine;

public static class StatsBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void EnsureStats()
    {
        if (Stats.I == null)
        {
            var go = new GameObject("Stats");
            go.AddComponent<Stats>(); // Awake hará DontDestroyOnLoad
        }
    }
}
