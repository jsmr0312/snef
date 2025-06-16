using UnityEngine;
using TMPro;

public class NPCDialogue : MonoBehaviour,
    Interactor.IInteractable,
    Interactor.IInteractableFeedback
{
    [Header("Diálogo")]
    public DialogueData dialogue;
    private int currentLine = 0;

    [Header("UI de diálogo")]
    public Canvas dialogueBubble;
    public TextMeshProUGUI dialogueText;

    [Header("Prompt UI")]
    public Canvas promptCanvas;
    public bool lookAtCamera = true;

    public void Interact()
    {
        if (dialogue == null || dialogue.lines.Length == 0)
        {
            Debug.LogWarning("No hay diálogo asignado.");
            return;
        }

        if (currentLine < dialogue.lines.Length)
        {
            // Muestra la línea actual
            dialogueText.text = dialogue.lines[currentLine];
            dialogueBubble.gameObject.SetActive(true);

            currentLine++;

            // Si justo acabamos de mostrar la última línea, lanzamos el brillo
            if (currentLine == dialogue.lines.Length)
            {
                TriggerScreensHighlight();
            }
        }
        else
        {
            // Mensaje final si vuelves a presionar E
            dialogueText.text = "¡Ya viste todo!";
            dialogueBubble.gameObject.SetActive(true);
        }
    }

    private void TriggerScreensHighlight()
    {
        // Encuentra todas las pantallas y activa su brillo
        var screens = FindObjectsOfType<UnifiedScreenDisplay>();
        foreach (var screen in screens)
        {
            screen.EnableHighlight();
        }
    }

    // IInteractableFeedback
    public void OnGazeEnter()
    {
        if (promptCanvas != null)
            promptCanvas.gameObject.SetActive(true);
    }

    public void OnGazeExit()
    {
        if (promptCanvas != null)
            promptCanvas.gameObject.SetActive(false);
        ResetDialogue();
    }

    private void ResetDialogue()
    {
        currentLine = 0;
        if (dialogueBubble != null)
            dialogueBubble.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        Transform cam = Camera.main.transform;

        // Mantener el prompt mirando a cámara
        if (lookAtCamera && promptCanvas != null && promptCanvas.gameObject.activeSelf)
        {
            promptCanvas.transform.LookAt(cam);
            promptCanvas.transform.Rotate(0, 180, 0);
        }

        // Mantener la burbuja mirando a cámara
        if (dialogueBubble != null && dialogueBubble.gameObject.activeSelf)
        {
            dialogueBubble.transform.LookAt(cam);
            dialogueBubble.transform.Rotate(0, 180, 0);
        }
    }
}
