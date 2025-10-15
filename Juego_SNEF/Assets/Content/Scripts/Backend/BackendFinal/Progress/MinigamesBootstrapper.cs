using UnityEngine;
using System.Collections.Generic;

[DefaultExecutionOrder(-7400)] // corre después de ProgressBootstrapper (-8000)
public class MinigamesBootstrapper : MonoBehaviour
{
    public bool log = true;

    // evitamos repetir importaciones
    private readonly HashSet<string> hydratedIds = new HashSet<string>();

    void OnEnable()
    {
        StartCoroutine(WaitAndHydrateOnce());
        if (ProgressCore.I != null) ProgressCore.I.OnChanged += OnProgressChanged;
    }

    void OnDisable()
    {
        if (ProgressCore.I != null) ProgressCore.I.OnChanged -= OnProgressChanged;
    }

    System.Collections.IEnumerator WaitAndHydrateOnce()
    {
        // Espera breve a que el bootstrap cargue el JSON
        float waited = 0f;
        while (ProgressCore.I == null || ProgressCore.I.Data == null)
        {
            yield return null; // 1 frame
        }

        // Hasta ~2s para que aparezcan los minijuegos
        while (waited < 2f)
        {
            var list = ProgressCore.I.Data.minigames;
            if (list != null && list.Count > 0) break;
            yield return new WaitForSecondsRealtime(0.1f);
            waited += 0.1f;
        }

        TryHydrate();
    }

    void OnProgressChanged(ProgressCore.GameProgressV1 _)
    {
        // Si el host reescribe el progreso o llega tarde, rehidratamos
        TryHydrate();
    }

    void TryHydrate()
    {
        if (Stats.I == null || ProgressCore.I == null || ProgressCore.I.Data == null) return;
        var list = ProgressCore.I.Data.minigames;
        if (list == null) return;

        int added = 0;
        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (m == null || string.IsNullOrEmpty(m.id)) continue;

            if (hydratedIds.Add(m.id))
            {
                // Sube al histórico (sin dar puntos)
                Stats.I.ImportMinigameBest(m.id, Mathf.Clamp(m.stars, 0, 3));
                added++;
            }
        }

        if (added > 0 && log)
            Debug.Log($"[MinigamesBootstrapper] Hidratados {added} (total mapeados={hydratedIds.Count}).");
    }
}
