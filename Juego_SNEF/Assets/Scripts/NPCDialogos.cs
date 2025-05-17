using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Collider))]
public class NPCQuizInteraction : MonoBehaviour
{
    private enum Phase { Waiting, Intro, ConfirmStart, Quiz }
    private Phase phase = Phase.Waiting;

    [Header("Player Movement")]
    [Tooltip("Arrastra tu script de control de jugador aquí para desactivarlo durante el quiz")]
    [SerializeField] private MonoBehaviour playerMovementScript;

    [Header("Prompt UI")]
    [Tooltip("Canvas World-Space que muestra “Pulsa E”")]
    [SerializeField] private GameObject promptUI;

    [Header("Dialogue UI")]
    [Tooltip("Panel (burbuja) donde salen todos los textos")]
    [SerializeField] private GameObject dialoguePanel;
    [Tooltip("TextMeshProUGUI dentro del panel para mostrar diálogos y preguntas")]
    [SerializeField] private TextMeshProUGUI dialogueText;

    [Header("Quiz Buttons")]
    [Tooltip("Botón opción 1")]
    [SerializeField] private Button optionButton1;
    [Tooltip("Botón opción 2")]
    [SerializeField] private Button optionButton2;
    [Tooltip("Botón opción 3")]
    [SerializeField] private Button optionButton3;

    [Header("Content Arrays")]
    [Tooltip("Diálogos iniciales. El último debe ser la pregunta de confirmación (E = sí / F = no)")]
    [SerializeField] private string[] introLines;
    [Tooltip("Preguntas del quiz")]
    [SerializeField] private string[] questions;
    [Tooltip("Respuestas de opción múltiple: debe tener questions.Length * 3 elementos")]
    [SerializeField] private string[] answers;

    private int introIndex;
    private int currentQuestion;
    private bool playerInRange;

    void Start()
    {
        // Inicialmente solo el prompt
        promptUI.SetActive(false);
        dialoguePanel.SetActive(false);
        SetQuizButtonsActive(false);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && phase == Phase.Waiting)
        {
            playerInRange = true;
            promptUI.SetActive(true);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            ResetAll();
        }
    }

    void Update()
    {
        if (!playerInRange) return;

        switch (phase)
        {
            case Phase.Waiting:
                if (Input.GetKeyDown(KeyCode.E))
                    StartIntro();
                break;

            case Phase.Intro:
                if (Input.GetKeyDown(KeyCode.E))
                    AdvanceIntro();
                break;

            case Phase.ConfirmStart:
                if (Input.GetKeyDown(KeyCode.E))
                    BeginQuiz();
                else if (Input.GetKeyDown(KeyCode.F))
                    CancelQuiz();
                break;
        }
    }

    // --- Intro Sequence ---

    private void StartIntro()
    {
        phase = Phase.Intro;
        promptUI.SetActive(false);
        dialoguePanel.SetActive(true);
        introIndex = 0;
        dialogueText.text = introLines[introIndex];
    }

    private void AdvanceIntro()
    {
        if (introIndex < introLines.Length - 1)
        {
            introIndex++;
            dialogueText.text = introLines[introIndex];
        }
        else
        {
            ShowConfirm();
        }
    }

    private void ShowConfirm()
    {
        phase = Phase.ConfirmStart;
        dialogueText.text = introLines[introLines.Length - 1];
        // aquí esperamos E o F
    }

    private void CancelQuiz()
    {
        ResetAll();
    }

    // --- Quiz Sequence ---

    private void BeginQuiz()
    {
        phase = Phase.Quiz;
        // bloquear movimiento y mostrar cursor
        if (playerMovementScript) playerMovementScript.enabled = false;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        dialoguePanel.SetActive(true);
        currentQuestion = 0;
        ShowQuestion();
    }

    private void ShowQuestion()
    {
        // muestra pregunta
        dialogueText.text = questions[currentQuestion];

        // prepara botones
        string a0 = answers[currentQuestion*3 + 0];
        string a1 = answers[currentQuestion*3 + 1];
        string a2 = answers[currentQuestion*3 + 2];

        SetupOption(optionButton1, a0, 0);
        SetupOption(optionButton2, a1, 1);
        SetupOption(optionButton3, a2, 2);

        SetQuizButtonsActive(true);
    }

    private void SetupOption(Button btn, string text, int choiceIndex)
    {
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => OnOptionSelected(choiceIndex));
        btn.GetComponentInChildren<TextMeshProUGUI>().text = text;
    }

    private void OnOptionSelected(int choice)
    {
        // aquí podrías checar la respuesta elegida
        currentQuestion++;
        if (currentQuestion < questions.Length)
        {
            ShowQuestion();
        }
        else
        {
            FinishQuiz();
        }
    }

    private void FinishQuiz()
    {
        // ocultar quiz UI
        SetQuizButtonsActive(false);
        dialoguePanel.SetActive(false);

        // restaurar movimiento y cursor
        if (playerMovementScript) playerMovementScript.enabled = true;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        ResetAll();
    }

    // --- Helpers ---

    private void SetQuizButtonsActive(bool active)
    {
        optionButton1.gameObject.SetActive(active);
        optionButton2.gameObject.SetActive(active);
        optionButton3.gameObject.SetActive(active);
    }

    private void ResetAll()
    {
        phase = Phase.Waiting;
        playerInRange = false;
        promptUI.SetActive(true);
        dialoguePanel.SetActive(false);
        SetQuizButtonsActive(false);
    }
}
