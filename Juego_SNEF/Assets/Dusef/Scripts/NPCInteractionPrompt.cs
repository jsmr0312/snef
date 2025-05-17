using UnityEngine;
using TMPro; // Si usas TextMeshPro. Si usas UnityEngine.UI.Text, cambia esta línea.

[RequireComponent(typeof(Collider))]
public class NPCInteractionPrompt : MonoBehaviour
{
    [Header("Prompt UI")]
    [Tooltip("El objeto que muestra 'Pulsa E'")]
    [SerializeField] private GameObject promptUI;

    [Header("Dialogue UI")]
    [Tooltip("Panel que contiene el texto de diálogo")]
    [SerializeField] private GameObject dialoguePanel;
    [Tooltip("Componente TextMeshProUGUI donde se escriben las líneas")]
    [SerializeField] private TextMeshProUGUI dialogueText;

    [Header("Líneas de diálogo")]
    [TextArea(2, 6)]
    [Tooltip("Cada elemento es una línea que aparecerá al pulsar E")]
    [SerializeField] private string[] dialogueLines;

    private bool playerInRange = false;
    private int currentLine = 0;

    void Start()
    {
        // Asegúrate de ocultar ambos al inicio
        if (promptUI)      promptUI.SetActive(false);
        if (dialoguePanel) dialoguePanel.SetActive(false);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            if (promptUI) promptUI.SetActive(true);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            if (promptUI)      promptUI.SetActive(false);
            if (dialoguePanel) dialoguePanel.SetActive(false);
        }
    }

    void Update()
    {
        if (!playerInRange) return;

        // Detecta la pulsación de E
        if (Input.GetKeyDown(KeyCode.E))
        {
            // Si nunca hemos abierto el diálogo, lo abrimos
            if (!dialoguePanel.activeSelf)
            {
                promptUI.SetActive(false);
                dialoguePanel.SetActive(true);
                currentLine = 0;
                ShowLine();
            }
            else
            {
                // Ya está abierto: avanzamos línea a línea
                currentLine++;
                if (currentLine < dialogueLines.Length)
                {
                    ShowLine();
                }
                else
                {
                    // Terminó el diálogo
                    dialoguePanel.SetActive(false);
                    promptUI.SetActive(true);
                }
            }
        }
    }

    /// <summary>
    /// Actualiza el texto en pantalla con la línea actual.
    /// </summary>
    private void ShowLine()
    {
        if (dialogueText != null && dialogueLines != null && dialogueLines.Length > 0)
        {
            dialogueText.text = dialogueLines[currentLine];
        }
    }
}
