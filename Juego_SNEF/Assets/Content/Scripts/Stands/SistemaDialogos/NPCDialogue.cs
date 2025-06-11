using UnityEngine;
using TMPro;

public class NPCDialogue : MonoBehaviour, IInteractable, IInteractableFeedback
{
    [Header("Diálogo")]
    public DialogueData dialogue;
    private int currentLine = 0;

    [Header("UI de diálogo")]
    public Canvas dialogueBubble;               // Canvas de la burbuja
    public TextMeshProUGUI dialogueText;        // Texto dentro de la burbuja

    [Header("UI de interacción")]
    public Canvas interactionCanvas;            // Canvas de “Presiona E”

    private void Start()
    {
        if (dialogueBubble != null)
            dialogueBubble.gameObject.SetActive(false);

        if (interactionCanvas != null)
            interactionCanvas.gameObject.SetActive(false);
    }

    public void Interact()
    {
        if (dialogue == null || dialogue.lines.Length == 0)
        {
            Debug.LogWarning("No hay diálogo asignado.");
            return;
        }

        if (currentLine < dialogue.lines.Length)
        {
            dialogueText.text = dialogue.lines[currentLine];
            dialogueBubble.gameObject.SetActive(true);
            currentLine++;
        }
        else
        {
            dialogueText.text = "¡Ya viste todo!";
            dialogueBubble.gameObject.SetActive(true);
        }
    }

    private void LateUpdate()
    {
        // La burbuja siempre mira a la cámara
        if (dialogueBubble != null && dialogueBubble.gameObject.activeSelf)
        {
            Transform cam = Camera.main.transform;
            dialogueBubble.transform.LookAt(cam);
            dialogueBubble.transform.Rotate(0, 180, 0); // Para que no esté de espaldas
        }

        if (interactionCanvas != null && interactionCanvas.gameObject.activeSelf)
        {
            Transform cam = Camera.main.transform;
            interactionCanvas.transform.LookAt(cam);
            interactionCanvas.transform.Rotate(0, 180, 0);
        }
    }

    // Canvas de “Presiona E”
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

    public void OnGazeEnter()
    {
        ShowCanvas(); // Presiona E
    }

    public void OnGazeExit()
    {
        HideCanvas();      // Oculta "Presiona E"
        ResetDialogue();   // Oculta burbuja y reinicia
    }

    public void ResetDialogue()
    {
        currentLine = 0;

        if (dialogueBubble != null)
            dialogueBubble.gameObject.SetActive(false);
    }

}
