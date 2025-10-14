using UnityEngine;

[DefaultExecutionOrder(-7400)] // después del ProgressBootstrapper (-8000) y antes de entrar a minijuegos
public class MinigamesBootstrapper : MonoBehaviour
{
    public bool log = true;

    void Start()
    {
        if (ProgressCore.I == null || Stats.I == null) return;

        // Recorremos lo que ya cargó ProgressCore desde progress-storage
        var list = ProgressCore.I.Data?.minigames;
        if (list == null) return;

        int count = 0;
        foreach (var m in list)
        {
            if (m == null || string.IsNullOrEmpty(m.id)) continue;
            // Solo nos importa que Stats sepa que este minijuego ya tiene N estrellas históricas.
            Stats.I.ImportMinigameBest(m.id, Mathf.Max(0, m.stars));
            count++;
        }
        if (log) Debug.Log($"[MinigamesBootstrapper] Hidratados {count} minijuegos en Stats.");
    }
}
