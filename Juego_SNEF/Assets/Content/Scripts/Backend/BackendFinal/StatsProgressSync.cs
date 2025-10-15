using UnityEngine;
using System.Collections;

[DefaultExecutionOrder(-7000)]
public class StatsProgressSync : MonoBehaviour
{
    [Tooltip("Logs de depuración")]
    public bool log = false;

    // se hace exactamente una vez por habilitación
    bool _initialized = false;

    void OnEnable()
    {
        if (ProgressCore.I != null) ProgressCore.I.OnChanged += OnProgressChanged;
        // Fallback: si por timing no recibimos OnChanged, hacemos un pull activo.
        StartCoroutine(LateEnsureSync());
    }

    void OnDisable()
    {
        if (ProgressCore.I != null) ProgressCore.I.OnChanged -= OnProgressChanged;
    }

    // 1) Camino “normal”: me entero por el primer OnChanged después del bootstrap
    void OnProgressChanged(ProgressCore.GameProgressV1 d)
    {
        if (_initialized || d == null || Stats.I == null) return;

        ApplyTotals(d.progress.presupuesto, d.progress.puntaje, "OnChanged");
    }

    // 2) Respaldo: si no llegó OnChanged o llegó antes de suscribirme, jalo desde ProgressCore
    IEnumerator LateEnsureSync()
    {
        // espera 1 frame para que ProgressBootstrapper pueda aplicar el JSON
        yield return null;

        float t = 0f;
        while (!_initialized && t < 2f) // ventana corta de 2s
        {
            if (Stats.I != null && ProgressCore.I != null && ProgressCore.I.Data != null)
            {
                int bud = Mathf.Max(0, ProgressCore.I.Data.progress.presupuesto);
                int pts = Mathf.Max(0, ProgressCore.I.Data.progress.puntaje);

                // Sólo si hay diferencia con lo que tiene Stats
                if (bud != Stats.I.Presupuesto || pts != Stats.I.Puntaje)
                {
                    ApplyTotals(bud, pts, "StartupPull");
                    yield break;
                }
            }

            yield return new WaitForSecondsRealtime(0.05f);
            t += 0.05f;
        }

        // Si aquí no había diferencia, de todos modos lo dejamos como inicializado
        _initialized = true;
        if (log) Debug.Log("[StatsSync] Nothing to pull (already matching or no data).");
        if (ProgressCore.I != null) ProgressCore.I.OnChanged -= OnProgressChanged;
    }

    void ApplyTotals(int presupuesto, int puntaje, string reason)
    {
        if (Stats.I == null) return;

        Stats.I.SetTotalsSilently(presupuesto, puntaje);   // sin métricas ni deltas
        _initialized = true;

        if (log) Debug.Log($"[StatsSync] Init via {reason} -> Presupuesto={presupuesto} Puntaje={puntaje}");

        // Ya no necesitamos seguir escuchando cambios iniciales
        if (ProgressCore.I != null) ProgressCore.I.OnChanged -= OnProgressChanged;
    }
}
