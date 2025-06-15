using UnityEngine;
using TMPro;

public class NPCDialogue : MonoBehaviour, Interactor.IInteractable, Interactor.IInteractableFeedback
{
    [Header("Diálogo")]
    public DialogueData dialogue;
    private int currentLine = 0;

    [Header("Burbuja de diálogo")]
    public Canvas dialogueBubble;
    public TextMeshProUGUI dialogueText;

    [Header("Canvas 'Presiona E' (opcional)")]
    public Canvas promptCanvas;
    public bool lookAtCamera = true;

    [Header("Pantallas a resaltar")]
    public ScreenHighlighter[] pantallasDestacadas;

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
            ActivarPantallas();
        }
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

        ResetDialogue();
    }

    public void ResetDialogue()
    {
        currentLine = 0;

        if (dialogueBubble != null)
            dialogueBubble.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        Transform cam = Camera.main.transform;

        if (dialogueBubble != null && dialogueBubble.gameObject.activeSelf)
        {
            dialogueBubble.transform.LookAt(cam);
            dialogueBubble.transform.Rotate(0, 180, 0);
        }

        if (lookAtCamera && promptCanvas != null && promptCanvas.gameObject.activeSelf)
        {
            promptCanvas.transform.LookAt(cam);
            promptCanvas.transform.Rotate(0, 180, 0);
        }
    }

    private void ActivarPantallas()
    {
        foreach (var pantalla in pantallasDestacadas)
        {
            pantalla?.ActivateHighlight();
        }
    }
}
