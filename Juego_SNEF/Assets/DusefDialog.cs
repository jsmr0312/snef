using UnityEngine;
using TMPro; // Solo si usas TextMeshPro

public class NPCDialog : MonoBehaviour
{
    public GameObject dialoguePanel;
    public TextMeshProUGUI dialogueText;
    public string[] messages;
    private int currentMessageIndex = 0;

    void OnMouseDown()
    {
        ShowDialogue();
    }

    void ShowDialogue()
    {
        dialoguePanel.SetActive(true);
        dialogueText.text = messages[currentMessageIndex];
    }

    public void NextMessage()
    {
        currentMessageIndex++;
        if (currentMessageIndex < messages.Length)
        {
            dialogueText.text = messages[currentMessageIndex];
        }
        else
        {
            dialoguePanel.SetActive(false);
            currentMessageIndex = 0; // Reinicia
        }
    }
}
