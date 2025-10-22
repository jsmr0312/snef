// AppBootstrap.cs
using UnityEngine;

public static class AppBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void EnsureSingletons()
    {
        if (WebGLBridge.I == null)
            new GameObject("WebGLBridge").AddComponent<WebGLBridge>();

        if (TokenLifecycle.I == null)
            new GameObject("TokenLifecycle").AddComponent<TokenLifecycle>();

        if (Object.FindObjectOfType<ProgressBootstrapper>() == null)
            new GameObject("ProgressBootstrapper").AddComponent<ProgressBootstrapper>();

        if (Object.FindObjectOfType<StatsProgressSync>() == null)
            new GameObject("StatsProgressSync").AddComponent<StatsProgressSync>();

        if (MissionManager.I == null)
            new GameObject("MissionManager").AddComponent<MissionManager>(); // ← NUEVO

        if (AchievementsManager.I == null)
            new GameObject("AchievementsManager").AddComponent<AchievementsManager>(); // (opcional si no es singleton)
    }
}
