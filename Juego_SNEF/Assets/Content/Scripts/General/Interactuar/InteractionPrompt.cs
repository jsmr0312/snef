using UnityEngine;

public class InteractionPrompt : MonoBehaviour, IInteractableFeedback
{
    [Tooltip("Canvas que dice 'Presiona E para interactuar'")]
    public Canvas promptCanvas;

    private void Start()
    {
        if (promptCanvas != null)
            promptCanvas.gameObject.SetActive(false);
    }

    public void OnGazeEnter()
    {
        if (promptCanvas != null)
            promptCanvas.gameObject.SetActive(true);
    }

    public void OnGazeExit()
    {
        if (promptCanvas != null)
            promptCanvas.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (promptCanvas != null && promptCanvas.gameObject.activeSelf)
        {
            Transform cam = Camera.main.transform;
            promptCanvas.transform.LookAt(cam);
            promptCanvas.transform.Rotate(0, 180, 0);
        }
    }
}
