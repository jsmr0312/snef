using UnityEngine;
using TMPro;
using System.Collections;

public class StatsHUD : MonoBehaviour
{
    [Header("UI de ESTA escena")]
    public TextMeshProUGUI presupuestoText;
    public TextMeshProUGUI puntajeText;

    int lastPresupuesto = int.MinValue;
    int lastPuntaje = int.MinValue;
    Coroutine loop;

    void OnEnable()
    {
        loop = StartCoroutine(Loop());
    }

    void OnDisable()
    {
        if (loop != null) StopCoroutine(loop);
        loop = null;
    }

    IEnumerator Loop()
    {
        // Espera a que exista Stats.I
        while (Stats.I == null) yield return null;

        // Pinta inicial
        Paint(Stats.I.Presupuesto, Stats.I.Puntaje);

        // Revisa cambios ~10 veces por segundo
        var wait = new WaitForSecondsRealtime(0.1f);
        while (true)
        {
            int p = Stats.I.Presupuesto;
            int s = Stats.I.Puntaje;
            if (p != lastPresupuesto || s != lastPuntaje)
                Paint(p, s);
            yield return wait;
        }
    }

    void Paint(int presupuesto, int puntaje)
    {
        lastPresupuesto = presupuesto;
        lastPuntaje = puntaje;

        if (presupuestoText) presupuestoText.text = presupuesto.ToString();
        if (puntajeText) puntajeText.text = puntaje.ToString();
        // Debug opcional:
        // Debug.Log($"[HUD] pinta Presupuesto={presupuesto} Puntaje={puntaje}");
    }
}
