using UnityEngine;
using TMPro;

public class NPCInteract : MonoBehaviour
{
    public GameObject pressEText;
    public GameObject dialoguePanel;
    public TextMeshProUGUI dialogueText;
    public string[] messages;

    private bool isPlayerNear = false;
    private int messageIndex = 0;

    void Update()
    {
        if (isPlayerNear && Input.GetKeyDown(KeyCode.E))
        {
            dialoguePanel.SetActive(true);
            dialogueText.text = messages[messageIndex];
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            pressEText.SetActive(true);
            isPlayerNear = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            pressEText.SetActive(false);
            dialoguePanel.SetActive(false);
            isPlayerNear = false;
            messageIndex = 0;
        }
    }

    public void NextMessage()
    {
        messageIndex++;
        if (messageIndex < messages.Length)
        {
            dialogueText.text = messages[messageIndex];
        }
        else
        {
            dialoguePanel.SetActive(false);
            messageIndex = 0;
        }
    }
}
