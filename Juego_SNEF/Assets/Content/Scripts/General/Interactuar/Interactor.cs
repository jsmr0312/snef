using UnityEngine;

public class Interactor : MonoBehaviour
{
    public Transform InteractorSource;
    public float InteractRange = 3f;
    public float SphereCastRadius = 0.25f;

    private IInteractable lastInteractable;

    void Update()
    {
        Ray ray = new Ray(InteractorSource.position, InteractorSource.forward);

        // Usamos SphereCast en lugar de Raycast
        if (Physics.SphereCast(ray, SphereCastRadius, out RaycastHit hitInfo, InteractRange))
        {
            if (hitInfo.collider.gameObject.TryGetComponent(out IInteractable interactObj))
            {
                if (interactObj != lastInteractable)
                {
                    if (lastInteractable is IInteractableFeedback oldFb) oldFb.OnGazeExit();
                    if (interactObj is IInteractableFeedback newFb) newFb.OnGazeEnter();
                    lastInteractable = interactObj;
                }

                if (Input.GetKeyDown(KeyCode.E))
                {
                    Debug.Log($"[Interactor] Pressed E on {interactObj}");
                    interactObj.Interact();
                }
                return;
            }
        }

        // Si no hay nada o salimos del objeto
        if (lastInteractable is IInteractableFeedback fb) fb.OnGazeExit();
        lastInteractable = null;
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
