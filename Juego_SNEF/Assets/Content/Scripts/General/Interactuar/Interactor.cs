using UnityEngine;

public class Interactor : MonoBehaviour
{
    [Header("Interactor Sources")]
    [Tooltip("Transforms to use as ray origins; the first active one will be used")]
    public Transform[] interactorSources;
    [Tooltip("Fallback if no source is active")]
    public Transform fallbackSource;

    [Header("Interaction Settings")]
    [Tooltip("Max distance for interaction")]
    public float InteractRange = 2f;
    [Tooltip("Radius of the spherecast")]
    public float SphereCastRadius = 0.25f;

    // Tracks the last hovered feedback component
    private IInteractableFeedback _lastFeedback;

    void Update()
    {
        // 1) Pick the active source transform
        Transform source = GetActiveSource();
        if (source == null) return;

        IInteractableFeedback found = null;

        // 2) Try spherecast forward
        Ray ray = new Ray(source.position, source.forward);
        if (Physics.SphereCast(ray, SphereCastRadius, out RaycastHit hit, InteractRange))
        {
            hit.collider.TryGetComponent(out found);
        }
        else
        {
            // 3) Fallback: overlap sphere around the source
            Collider[] cols = Physics.OverlapSphere(source.position, InteractRange);
            foreach (var c in cols)
                if (c.TryGetComponent<IInteractableFeedback>(out found))
                    break;
        }

        // 4) Show/Hide feedback canvas
        if (found != _lastFeedback)
        {
            _lastFeedback?.OnGazeExit();
            found?.OnGazeEnter();
            _lastFeedback = found;
        }
        else if (found == null && _lastFeedback != null)
        {
            _lastFeedback.OnGazeExit();
            _lastFeedback = null;
        }

        // 5) Interact on E
        if (found != null && Input.GetKeyDown(KeyCode.E))
            (found as IInteractable)?.Interact();
    }

    /// <summary>
    /// Returns the first interactorSource whose GameObject is active; otherwise fallback.
    /// </summary>
    private Transform GetActiveSource()
    {
        if (interactorSources != null)
        {
            foreach (var src in interactorSources)
                if (src != null && src.gameObject.activeInHierarchy)
                    return src;
        }
        return fallbackSource;
    }

    void OnDrawGizmosSelected()
    {
        Transform source = GetActiveSource();
        if (source != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(source.position, InteractRange);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(source.position, SphereCastRadius);
        }
    }

    public interface IInteractable
    {
        void Interact();
    }

    public interface IInteractableFeedback
    {
        void OnGazeEnter();
        void OnGazeExit();
    }
}
