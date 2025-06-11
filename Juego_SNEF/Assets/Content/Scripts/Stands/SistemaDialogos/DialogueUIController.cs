using UnityEngine;
using TMPro;

public class DialogueUIController : MonoBehaviour
{
    public GameObject bubble;
    public TextMeshProUGUI dialogueText;

    public void ShowMessage(string message)
    {
        bubble.SetActive(true);
        dialogueText.text = message;
    }

    public void Hide()
    {
        bubble.SetActive(false);
    }
}
