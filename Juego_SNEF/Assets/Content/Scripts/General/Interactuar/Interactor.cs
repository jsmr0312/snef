using UnityEngine;

public class Interactor : MonoBehaviour
{
    [Header("Interactor Sources")]
    public Transform[] interactorSources;
    public Transform fallbackSource;

    [Header("Interaction Settings")]
    [Tooltip("Radio máximo de búsqueda")]
    public float InteractRange = 2.5f;
    [Tooltip("Ángulo de ayuda de mira (grados)")]
    [Range(0, 89)] public float aimAssistDegrees = 55f;
    [Tooltip("Incluye triggers al buscar")]
    public bool includeTriggers = true;
    [Tooltip("Capa(s) de interactuables")]
    public LayerMask interactableMask = ~0;

    [Header("Estabilidad del foco")]
    [Tooltip("Ventaja mínima de score para cambiar de objetivo")]
    [Range(0f, 0.5f)] public float switchHysteresis = 0.08f;
    [Tooltip("Pequeño sesgo a mantener el último target")]
    [Range(0f, 0.2f)] public float stickyBonus = 0.05f;
    [Tooltip("Peso relativo de 'centrado' vs. 'cercanía'")]
    [Range(0f, 1f)] public float centerWeight = 0.65f; // distancia = 1 - centerWeight

    [Header("View-mode compatibility")]
    public bool pauseScanWhenCursorUnlocked = true;

    // buffers
    private readonly Collider[] _buf = new Collider[24];

    // track
    private IInteractableFeedback _lastFeedback;
    private IInteractable _lastInteractable;
    private Transform _lastAnchor; // para re-score



    void Update()
    {

        if (pauseScanWhenCursorUnlocked && Cursor.lockState != CursorLockMode.Locked)
        {
            if (_lastFeedback != null) { _lastFeedback.OnGazeExit(); }
            _lastFeedback = null; _lastInteractable = null; _lastAnchor = null;
            return; // no escanees mientras el cursor está libre (modo vista)
        }
        Transform src = GetActiveSource();
        if (!src) return;

        // --- recolectar candidatos ---
        int count = Physics.OverlapSphereNonAlloc(
            src.position,
            InteractRange,
            _buf,
            interactableMask,
            includeTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore
        );

        int bestIndex = -1;
        float bestScore = -1f;
        IInteractable bestInteract = null;
        IInteractableFeedback bestFeedback = null;
        Transform bestAnchor = null;

        float minDot = Mathf.Cos(aimAssistDegrees * Mathf.Deg2Rad);

        for (int i = 0; i < count; i++)
        {
            var col = _buf[i];
            if (!col || !col.gameObject.activeInHierarchy) continue;

            // tomar componente en este GO o padres
            var feedback = col.GetComponentInParent<IInteractableFeedback>();
            if (feedback == null) continue;
            var interact = col.GetComponentInParent<IInteractable>();

            // punto de anclaje (FocusAnchor opcional; si no, bounds.center)
            Transform anchor = null;
            var fa = col.GetComponentInParent<FocusAnchor>();
            if (fa) anchor = fa.anchor ? fa.anchor : fa.transform;
            Vector3 anchorPos = anchor ? anchor.position : col.bounds.center;

            Vector3 to = anchorPos - src.position;
            float dist = to.magnitude; if (dist <= 0.0001f) dist = 0.0001f;
            Vector3 dir = to / dist;

            float dot = Vector3.Dot(src.forward, dir);
            if (dot < minDot) continue; // fuera del cono de mira

            // score por centrado y cercanía
            float centerScore = Mathf.InverseLerp(minDot, 1f, dot);               // 0..1 (más centrado, mayor)
            float distScore = 1f - Mathf.Clamp01(dist / InteractRange);         // 0..1 (más cerca, mayor)
            float score = centerWeight * centerScore + (1f - centerWeight) * distScore;

            // prioridad opcional
            var prio = col.GetComponentInParent<InteractablePriority>();
            if (prio) score += Mathf.Clamp01(prio.priority) * 0.05f;

            // bonus al target anterior (anti-parpadeo)
            if (feedback == _lastFeedback) score += stickyBonus;

            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
                bestFeedback = feedback;
                bestInteract = interact;
                bestAnchor = anchor;
            }
        }

        // --- histeresis: no cambies si el anterior sigue casi tan bueno ---
        if (_lastFeedback != null && bestFeedback != null && bestFeedback != _lastFeedback)
        {
            float lastScore = ScoreTarget(src, _lastAnchor, _lastFeedback, minDot);
            if (lastScore + switchHysteresis >= bestScore)
            {
                // mantenemos el anterior
                bestFeedback = _lastFeedback;
                bestInteract = _lastInteractable;
                bestAnchor = _lastAnchor;
            }
        }

        // --- aplicar entrada/salida de foco ---
        if (bestFeedback != _lastFeedback)
        {
            _lastFeedback?.OnGazeExit();
            bestFeedback?.OnGazeEnter();
            _lastFeedback = bestFeedback;
            _lastInteractable = bestInteract;
            _lastAnchor = bestAnchor;
        }

        // --- interactuar ---
        if (_lastFeedback != null && Input.GetKeyDown(KeyCode.E))
            _lastInteractable?.Interact();
        else if (_lastFeedback == null && Input.GetKeyDown(KeyCode.E))
            ; // sin objetivo: ignorar
    }

    private float ScoreTarget(Transform src, Transform anchor, IInteractableFeedback fb, float minDot)
    {
        if (fb == null) return -1f;
        // intenta recuperar collider (estimación) para re-score
        var comp = (fb as Component);
        if (!comp) return -1f;
        var col = comp.GetComponentInParent<Collider>();
        if (!col) return -1f;

        Vector3 anchorPos = anchor ? anchor.position : col.bounds.center;
        Vector3 to = anchorPos - src.position;
        float dist = to.magnitude; if (dist <= 0.0001f) dist = 0.0001f;
        Vector3 dir = to / dist;

        float dot = Vector3.Dot(src.forward, dir);
        if (dot < minDot) return -1f;

        float centerScore = Mathf.InverseLerp(minDot, 1f, dot);
        float distScore = 1f - Mathf.Clamp01(dist / InteractRange);
        return centerWeight * centerScore + (1f - centerWeight) * distScore;
    }

    private Transform GetActiveSource()
    {
        if (interactorSources != null)
            for (int i = 0; i < interactorSources.Length; i++)
                if (interactorSources[i] && interactorSources[i].gameObject.activeInHierarchy)
                    return interactorSources[i];
        return fallbackSource;
    }

    // === Interfaces (sin cambios) ===
    public interface IInteractable { void Interact(); }
    public interface IInteractableFeedback { void OnGazeEnter(); void OnGazeExit(); }
}

// --- Helpers opcionales ---
public class InteractablePriority : MonoBehaviour
{
    [Range(0f, 1f)] public float priority = 0f; // usa 1.0 si quieres que gane empates
}

