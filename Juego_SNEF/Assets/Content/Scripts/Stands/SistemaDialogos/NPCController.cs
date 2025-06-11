using UnityEngine;

public class NPCController : MonoBehaviour, IInteractable
{
    public DialogueData dialogue;
    private int currentLine = 0;

    public GameObject[] screensToActivate;

    public void Interact()
    {
        if (currentLine < dialogue.lines.Length)
        {
            Debug.Log(dialogue.lines[currentLine]);
            currentLine++;
        }
        else if (currentLine == dialogue.lines.Length)
        {
            Debug.Log("Diálogo terminado. Activando pantallas...");
            ActivateScreens();
            currentLine++;
        }
        else
        {
            Debug.Log("¡Ya viste el diálogo! Ahora ve las pantallas.");
        }
    }

    private void ActivateScreens()
    {
        foreach (GameObject screen in screensToActivate)
        {
            if (screen.TryGetComponent<NumberGenerator>(out var interactable))
                interactable.ShowCanvas(); // activa canvas tipo “Presiona E”
        }
    }
}
