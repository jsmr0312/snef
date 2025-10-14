using UnityEngine;

[DefaultExecutionOrder(-7000)]
public class StatsProgressSync : MonoBehaviour
{
    public bool log = false;

    void OnEnable()
    {
        if (ProgressCore.I != null) ProgressCore.I.OnChanged += OnProgressChanged; // evento público en tu ProgressCore
        // Sincroniza una vez al entrar
        if (ProgressCore.I != null) OnProgressChanged(ProgressCore.I.Data);
    }

    void OnDisable()
    {
        if (ProgressCore.I != null) ProgressCore.I.OnChanged -= OnProgressChanged;
    }

    void OnProgressChanged(ProgressCore.GameProgressV1 d)
    {
        if (Stats.I == null || d == null) return;
        int deltaBudget = d.progress.presupuesto - Stats.I.Presupuesto;
        int deltaScore = d.progress.puntaje - Stats.I.Puntaje;
        if (deltaBudget != 0) Stats.I.AddPresupuesto(deltaBudget);  // dispara tus eventos de UI y métricas
        if (deltaScore != 0) Stats.I.AddPuntaje(deltaScore);
        Stats.I.ForceRefresh();
        if (log) Debug.Log($"[StatsSync] presupuesto={d.progress.presupuesto} puntaje={d.progress.puntaje}");
    }
}
