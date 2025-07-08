using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class QuizManager : MonoBehaviour
{
    [Header("Datos del Quiz")]
    public QuizData quizData;

    [Header("Referencias UI")]
    public GameObject quizPanel;              // Panel raíz (incluye fondo oscuro)
    public RectTransform questionPanelRT;     // Panel que contiene pregunta (para animar)
    public RectTransform optionsGridRT;       // Grid de botones de opción
    public GameObject bgResultado;            // Panel de resultado final

    public TextMeshProUGUI questionText;      // Texto de la pregunta
    public Button[] optionButtons;            // Array de 2–4 botones de opción

    [Header("Feedback Icons")]
    public Image correctFeedback;             // Imagen ✓ en el centro
    public Image incorrectFeedback;           // Imagen ✕ en el centro

    [Header("Resultado UI")]
    public TextMeshProUGUI resultadoText;     // Texto "2/3"
    public TextMeshProUGUI motivacionalText;  // Texto motivacional
    public Button retryButton;                // Botón Reintentar
    public Button exitButton;                 // Botón Salir

    [Header("Control de Jugador y Cámara")]
    public MonoBehaviour playerController;
    public MonoBehaviour cameraController;

    // estado interno
    int _currentIndex;
    int _score;
    Vector2 _questionOrigPos, _optionsOrigPos;

    void Awake()
    {
        // cachea posiciones originales
        _questionOrigPos = questionPanelRT.anchoredPosition;
        _optionsOrigPos = optionsGridRT.anchoredPosition;

        // oculta todo al inicio
        quizPanel.SetActive(false);
        bgResultado.SetActive(false);
        correctFeedback.gameObject.SetActive(false);
        incorrectFeedback.gameObject.SetActive(false);

        // listeners de opciones
        for (int i = 0; i < optionButtons.Length; i++)
        {
            int idx = i;
            optionButtons[i].onClick.AddListener(() => StartCoroutine(HandleAnswer(idx)));
        }

        // retry y exit
        retryButton.onClick.AddListener(() =>
        {
            bgResultado.SetActive(false);
            // reactivamos preguntas/opciones
            questionPanelRT.gameObject.SetActive(true);
            optionsGridRT.gameObject.SetActive(true);
            // reiniciamos el quiz
            StartQuiz();
        });
        exitButton.onClick.AddListener(CloseQuiz);
    }

    public void StartQuiz()
    {
        if (quizData == null || quizData.questions.Length == 0)
        {
            Debug.LogWarning("QuizData no asignado o sin preguntas.");
            return;
        }

        // misión complete quiz
        MissionManager.I?.NotifyEvent(MissionManager.MissionType.CompleteQuiz);

        // desactiva controles
        if (playerController != null) playerController.enabled = false;
        if (cameraController != null) cameraController.enabled = false;

        // muestra panel de quiz
        quizPanel.SetActive(true);
        bgResultado.SetActive(false);

        // asegúrate de que pregunta/opciones estén activos y en su lugar
        questionPanelRT.gameObject.SetActive(true);
        optionsGridRT.gameObject.SetActive(true);
        questionPanelRT.anchoredPosition = _questionOrigPos;
        optionsGridRT.anchoredPosition = _optionsOrigPos;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        _currentIndex = 0;
        _score = 0;

        ShowQuestion(_currentIndex);
    }

    void ShowQuestion(int index)
    {
        var q = quizData.questions[index];
        questionText.text = q.question;
        for (int i = 0; i < optionButtons.Length; i++)
        {
            if (i < q.options.Length)
            {
                optionButtons[i].gameObject.SetActive(true);
                optionButtons[i].interactable = true;
                optionButtons[i].GetComponentInChildren<TextMeshProUGUI>().text = q.options[i];
            }
            else
            {
                optionButtons[i].gameObject.SetActive(false);
            }
        }
    }

    IEnumerator HandleAnswer(int chosenIdx)
    {
        // bloquea botones
        foreach (var b in optionButtons)
            b.interactable = false;

        // comprueba
        var q = quizData.questions[_currentIndex];
        bool correct = chosenIdx == q.correctIndex;
        if (correct) _score++;

        // muestra el icono correcto/incorrecto
        var img = correct ? correctFeedback : incorrectFeedback;
        img.transform.localScale = Vector3.zero;
        img.transform.rotation = Quaternion.identity;
        img.gameObject.SetActive(true);

        // anima crecer + rotar
        float t = 0f, dur = 0.3f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float f = t / dur;
            img.transform.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, f);
            img.transform.rotation = Quaternion.Euler(0, 0, 360f * f);
            yield return null;
        }
        img.transform.localScale = Vector3.one;
        img.transform.rotation = Quaternion.identity;

        // espera
        yield return new WaitForSeconds(1f);
        img.gameObject.SetActive(false);

        // desliza pregunta/opciones hacia abajo
        Vector2 qStart = _questionOrigPos;
        Vector2 oStart = _optionsOrigPos;
        float slide = questionPanelRT.rect.height + optionsGridRT.rect.height + 20f;
        t = 0f; dur = 0.3f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float f = t / dur;
            questionPanelRT.anchoredPosition = Vector2.Lerp(qStart, qStart - Vector2.up * slide, f);
            optionsGridRT.anchoredPosition = Vector2.Lerp(oStart, oStart - Vector2.up * slide, f);
            yield return null;
        }

        // siguiente
        _currentIndex++;
        if (_currentIndex < quizData.questions.Length)
        {
            // reposiciona abajo y vuelve a subir
            questionPanelRT.anchoredPosition = qStart - Vector2.up * slide;
            optionsGridRT.anchoredPosition = oStart - Vector2.up * slide;
            ShowQuestion(_currentIndex);

            t = 0f; dur = 0.3f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float f = t / dur;
                questionPanelRT.anchoredPosition = Vector2.Lerp(qStart - Vector2.up * slide, qStart, f);
                optionsGridRT.anchoredPosition = Vector2.Lerp(oStart - Vector2.up * slide, oStart, f);
                yield return null;
            }
            // reactiva botones
            foreach (var b in optionButtons)
                b.interactable = true;
        }
        else
        {
            EndQuiz();
        }
    }

    void EndQuiz()
    {
        // oculta pregunta/opciones
        questionPanelRT.gameObject.SetActive(false);
        optionsGridRT.gameObject.SetActive(false);

        // calcula resultado
        resultadoText.text = $"{_score}/{quizData.questions.Length}";
        float ratio = (float)_score / quizData.questions.Length;
        if (ratio >= 1f) motivacionalText.text = "¡Lo hiciste perfecto!";
        else if (ratio >= 0.6f) motivacionalText.text = "¡Muy bien, sigue así!";
        else motivacionalText.text = "¡Sigue intentando!";

        // muestra panel de resultados
        bgResultado.SetActive(true);

        // notifica NPC y desbloquea arcade
        FindObjectOfType<NPCDialogueFlow>()?.OnQuizFinished();
        FindObjectOfType<ArcadeInteractable>()?.UnlockArcade();
    }

    void CloseQuiz()
    {
        // cierra todo
        quizPanel.SetActive(false);
        bgResultado.SetActive(false);

        // restablece pregunta + grid para la próxima partida
        questionPanelRT.gameObject.SetActive(true);
        optionsGridRT.gameObject.SetActive(true);
        questionPanelRT.anchoredPosition = _questionOrigPos;
        optionsGridRT.anchoredPosition = _optionsOrigPos;

        // reactiva controles y oculta cursor
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        if (playerController != null) playerController.enabled = true;
        if (cameraController != null) cameraController.enabled = true;
    }
}
