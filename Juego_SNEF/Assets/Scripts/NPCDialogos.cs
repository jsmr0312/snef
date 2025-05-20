using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider))]
public class NPCDialogos : MonoBehaviour
{
    [Header("Prompt UI")]
    [SerializeField] private GameObject promptUI;

    [Header("Dialogue UI")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TextMeshProUGUI dialogueText;
    [TextArea(2,6)]
    [SerializeField] private string[] dialogueLines;

    [Header("Choice Buttons")]
    [SerializeField] private Button yesButton;      // "Jugar"
    [SerializeField] private Button noButton;       // "No, en otro momento"

    [Header("Escena a cargar")]
    [SerializeField] private string sceneToLoad;    // nombre exacto, sin ".unity"

    private bool playerInRange = false;
    private int currentLine = 0;

    void Start()
    {
        // Ocultamos todo al inicio
        promptUI.SetActive(false);
        dialoguePanel.SetActive(false);
        yesButton.gameObject.SetActive(false);
        noButton.gameObject.SetActive(false);

        // Conectamos clicks
        yesButton.onClick.AddListener(OnYesClicked);
        noButton.onClick.AddListener(OnNoClicked);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            promptUI.SetActive(true);
            currentLine = 0;  // resetea línea al entrar
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            promptUI.SetActive(false);
            dialoguePanel.SetActive(false);
            yesButton.gameObject.SetActive(false);
            noButton.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        if (!playerInRange) return;

        // 1) Si el diálogo está cerrado, E lo abre
        if (!dialoguePanel.activeSelf)
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                promptUI.SetActive(false);
                dialoguePanel.SetActive(true);
                ShowLine();
            }
            return;
        }

        // 2) Si estamos antes de la última línea, E avanza
        if (currentLine < dialogueLines.Length - 1)
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                currentLine++;
                ShowLine();
            }
            return;
        }

        // 3) Última línea: activamos botones (si aún no se han activado)
        if (!yesButton.gameObject.activeSelf)
        {
            yesButton.gameObject.SetActive(true);
            noButton.gameObject.SetActive(true);
        }

        // E = confirmar, F = cancelar
        if (Input.GetKeyDown(KeyCode.E))
        {
            OnYesClicked();
        }
        else if (Input.GetKeyDown(KeyCode.F))
        {
            OnNoClicked();
        }
    }

    private void ShowLine()
    {
        dialogueText.text = dialogueLines[currentLine];
    }

    private void OnYesClicked()
    {
        SceneManager.LoadScene(sceneToLoad);
    }

    private void OnNoClicked()
    {
        // Cerrar diálogo y volver al prompt
        dialoguePanel.SetActive(false);
        yesButton.gameObject.SetActive(false);
        noButton.gameObject.SetActive(false);
        promptUI.SetActive(true);
        currentLine = 0;
    }
}
