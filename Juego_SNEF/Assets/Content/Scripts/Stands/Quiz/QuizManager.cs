using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class QuizManager : MonoBehaviour
{
    [Header("Datos del Quiz")]
    public QuizData quizData;

    [Header("Referencias UI")]
    public GameObject quizPanel;              // Panel raíz (incluye fondo oscuro)
    public TextMeshProUGUI questionText;      // Texto de la pregunta
    public Button[] optionButtons;            // Array de 2–4 botones de opción
    public Button closeButton;                // Botón “Cerrar”

    [Header("Control de Jugador")]
    [Tooltip("El componente que controla el movimiento del jugador")]
    public MonoBehaviour playerController;

    // Estado interno
    private int _currentIndex;
    private int _score;

    void Awake()
    {
        // Arranca oculto
        quizPanel.SetActive(false);

        // Conectar listeners de opciones
        for (int i = 0; i < optionButtons.Length; i++)
        {
            int idx = i;
            optionButtons[i].onClick.AddListener(() => OnOptionSelected(idx));
        }

        // Listener de cerrar
        closeButton.onClick.AddListener(CancelQuiz);
    }

    /// <summary> Inicia el quiz </summary>
    public void StartQuiz()
    {
        if (quizData == null || quizData.questions.Length == 0)
        {
            Debug.LogWarning("QuizData no asignado o sin preguntas.");
            return;
        }

        // Desactiva movimiento del jugador
        if (playerController != null) playerController.enabled = false;

        // Muestra y libera cursor
        quizPanel.SetActive(true);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        _currentIndex = 0;
        _score = 0;
        closeButton.gameObject.SetActive(false);

        ShowQuestion(_currentIndex);
    }

    private void ShowQuestion(int index)
    {
        var q = quizData.questions[index];
        questionText.text = q.question;

        // Mostrar solo las opciones necesarias
        for (int i = 0; i < optionButtons.Length; i++)
        {
            if (i < q.options.Length)
            {
                optionButtons[i].gameObject.SetActive(true);
                optionButtons[i]
                  .GetComponentInChildren<TextMeshProUGUI>()
                  .text = q.options[i];
            }
            else
            {
                optionButtons[i].gameObject.SetActive(false);
            }
        }
    }

    private void OnOptionSelected(int chosenIdx)
    {
        var q = quizData.questions[_currentIndex];
        if (chosenIdx == q.correctIndex) _score++;

        int next = _currentIndex + 1;
        if (next < quizData.questions.Length)
        {
            _currentIndex = next;
            ShowQuestion(_currentIndex);
        }
        else
        {
            EndQuiz();
        }
    }

    private void EndQuiz()
    {
        // Mostrar resultado final
        questionText.text = $"¡Has terminado!\nPuntuación: {_score}/{quizData.questions.Length}";

        // Ocultar botones de opción
        foreach (var b in optionButtons)
            b.gameObject.SetActive(false);

        // Mostrar el botón de cerrar
        closeButton.gameObject.SetActive(true);
        FindObjectOfType<NPCDialogueFlow>()?.OnQuizFinished();


        // ——— NUEVO: desbloquear la máquina de arcade ———
        var arcade = FindObjectOfType<ArcadeInteractable>();
        if (arcade != null)
            arcade.UnlockArcade();
    }


    private void CancelQuiz()
    {
        // Cierra el panel
        quizPanel.SetActive(false);

        // Oculta cursor y bloquea de nuevo
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        // Reactiva movimiento del jugador
        if (playerController != null) playerController.enabled = true;
    }
}
