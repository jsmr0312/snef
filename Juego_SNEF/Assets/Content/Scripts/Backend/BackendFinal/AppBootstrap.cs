using UnityEngine;

public static class AppBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void EnsureSingletons()
    {
        // Stats ya lo garantizas con StatsBootstrap.cs

        if (WebGLBridge.I == null)
            new GameObject("WebGLBridge").AddComponent<WebGLBridge>();

        if (TokenLifecycle.I == null)
            new GameObject("TokenLifecycle").AddComponent<TokenLifecycle>();

        if (Object.FindObjectOfType<ProgressBootstrapper>() == null)
            new GameObject("ProgressBootstrapper").AddComponent<ProgressBootstrapper>();

        if (Object.FindObjectOfType<StatsProgressSync>() == null)
            new GameObject("StatsProgressSync").AddComponent<StatsProgressSync>();

        if (Object.FindObjectOfType<MinigamesBootstrapper>() == null)
            new GameObject("MinigamesBootstrapper").AddComponent<MinigamesBootstrapper>();
    }
}
