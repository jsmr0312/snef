using UnityEngine;

public class NumberGenerator : MonoBehaviour, IInteractable, IInteractableFeedback
{
    public Canvas interactionCanvas;

    private void Awake()
    {
        if (interactionCanvas != null)
            interactionCanvas.gameObject.SetActive(false);
    }

    public void Interact()
    {
        Debug.Log("Número aleatorio: " + Random.Range(0, 100));
    }

    public void ShowCanvas()
    {
        if (interactionCanvas != null)
            interactionCanvas.gameObject.SetActive(true);
    }

    public void HideCanvas()
    {
        if (interactionCanvas != null)
            interactionCanvas.gameObject.SetActive(false);
    }

    public void OnGazeEnter() => ShowCanvas();
    public void OnGazeExit() => HideCanvas();
}
