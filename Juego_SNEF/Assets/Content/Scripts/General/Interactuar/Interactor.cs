using UnityEngine;

public class Interactor : MonoBehaviour
{
    public Transform InteractorSource;
    public float InteractRange = 3f;

    private IInteractable lastInteractable;

    void Update()
    {
        Ray r = new Ray(InteractorSource.position, InteractorSource.forward);
        if (Physics.Raycast(r, out RaycastHit hitInfo, InteractRange))
        {
            if (hitInfo.collider.gameObject.TryGetComponent(out IInteractable interactObj))
            {
                if (interactObj != lastInteractable)
                {
                    // Avisar al anterior que lo dejaron de mirar
                    if (lastInteractable is IInteractableFeedback oldFeedback)
                        oldFeedback.OnGazeExit();

                    // Avisar al nuevo que lo están mirando
                    if (interactObj is IInteractableFeedback newFeedback)
                        newFeedback.OnGazeEnter();

                    lastInteractable = interactObj;
                }

                if (Input.GetKeyDown(KeyCode.E))
                    interactObj.Interact();
            }
        }
        else
        {
            if (lastInteractable is IInteractableFeedback feedback)
                feedback.OnGazeExit();

            lastInteractable = null;
        }
    }
}
