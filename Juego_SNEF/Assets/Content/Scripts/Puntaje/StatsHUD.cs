using UnityEngine;
using TMPro;
using System.Collections;

public class StatsHUD : MonoBehaviour
{
    [Header("UI de ESTA escena")]
    public TextMeshProUGUI presupuestoText;
    public TextMeshProUGUI puntajeText;

    [Header("Contador animado")]
    [Tooltip("Duración objetivo de la animación (segundos) sin importar la magnitud del cambio).")]
    public float countDuration = 0.4f;
    [Tooltip("Máximo de 'ticks' (pasos) por animación. Limita pasos para saltos grandes.")]
    public int maxTicks = 30;
    [Tooltip("SFX opcional que suena en cada tick.")]
    public AudioSource tickSfx;

    // estado
    int lastPresupuesto = int.MinValue;
    int lastPuntaje = int.MinValue;

    int shownPresupuesto; // lo que se está mostrando actualmente
    int shownPuntaje;

    Coroutine loopCo;
    Coroutine presupuestoCo;
    Coroutine puntajeCo;

    void OnEnable()
    {
        loopCo = StartCoroutine(Loop());
    }

    void OnDisable()
    {
        if (loopCo != null) StopCoroutine(loopCo);
        loopCo = null;

        if (presupuestoCo != null) StopCoroutine(presupuestoCo);
        if (puntajeCo != null) StopCoroutine(puntajeCo);
        presupuestoCo = puntajeCo = null;
    }

    IEnumerator Loop()
    {
        while (Stats.I == null) yield return null;

        // Inicializa mostrando el valor actual sin animación
        lastPresupuesto = shownPresupuesto = Stats.I.Presupuesto;
        lastPuntaje = shownPuntaje = Stats.I.Puntaje;

        if (presupuestoText) presupuestoText.text = shownPresupuesto.ToString();
        if (puntajeText) puntajeText.text = shownPuntaje.ToString();

        var wait = new WaitForSecondsRealtime(0.1f);
        while (true)
        {
            int p = Stats.I.Presupuesto;
            int s = Stats.I.Puntaje;

            if (p != lastPresupuesto)
            {
                lastPresupuesto = p;
                if (presupuestoCo != null) StopCoroutine(presupuestoCo);
                presupuestoCo = StartCoroutine(CountTo(presupuestoText, shownPresupuesto, p, v => shownPresupuesto = v));
            }

            if (s != lastPuntaje)
            {
                lastPuntaje = s;
                if (puntajeCo != null) StopCoroutine(puntajeCo);
                puntajeCo = StartCoroutine(CountTo(puntajeText, shownPuntaje, s, v => shownPuntaje = v));
            }

            yield return wait;
        }
    }

    IEnumerator CountTo(TextMeshProUGUI label, int from, int to, System.Action<int> setShown)
    {
        if (label == null) yield break;

        if (from == to)
        {
            label.text = to.ToString();
            setShown(to);
            yield break;
        }

        int diff = to - from;
        int steps = Mathf.Abs(diff);

        // Limitar número de ticks para saltos grandes
        int ticks = Mathf.Clamp(steps, 1, Mathf.Max(1, maxTicks));
        // tamaño del paso (redondeado hacia el signo del diff)
        int step = Mathf.Max(1, Mathf.RoundToInt(steps / (float)ticks)) * (diff > 0 ? 1 : -1);

        // recalcula ticks reales con ese tamaño de paso
        ticks = Mathf.CeilToInt(steps / (float)Mathf.Abs(step));

        float total = Mathf.Max(0.0001f, countDuration);
        float perTick = total / ticks;

        int current = from;

        for (int i = 0; i < ticks; i++)
        {
            // Último tick aterriza exactamente en 'to'
            if (i == ticks - 1) current = to;
            else current += step;

            label.text = current.ToString();
            setShown(current);

            if (tickSfx) tickSfx.Play();

            yield return new WaitForSecondsRealtime(perTick);
        }

        // aseguramos valor final
        label.text = to.ToString();
        setShown(to);
    }
}
