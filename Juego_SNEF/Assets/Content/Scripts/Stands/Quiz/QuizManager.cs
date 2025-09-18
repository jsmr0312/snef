using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class QuizManager : MonoBehaviour
{
    [Header("Datos del Quiz")]
    public QuizData quizData;

    [Header("Referencias UI")]
    public GameObject quizPanel;
    public RectTransform questionPanelRT;
    public RectTransform optionsGridRT;
    public GameObject bgResultado;

    public TextMeshProUGUI questionText;
    public Button[] optionButtons;

    [Header("Feedback Icons")]
    public Image correctFeedback;
    public Image incorrectFeedback;

    [Header("Resultado UI")]
    public TextMeshProUGUI resultadoText;
    public TextMeshProUGUI motivacionalText;
    public Button retryButton;
    public Button exitButton;

    [Header("Control de Jugador y Cámara")]
    public MonoBehaviour playerController;
    public MonoBehaviour cameraController;

    [Header("Tiempo por pregunta")]
    [Tooltip("Segundos por pregunta. 15 por defecto.")]
    public float timePerQuestion = 15f;

    [Header("UI del temporizador")]
    [Tooltip("Panel/Contenedor del temporizador (se mostrará durante la pregunta y se ocultará al ver resultados).")]
    public GameObject barraTiempoPanel;
    [Tooltip("Image de la barrita azul (BarraTiempoProgresoImagen).")]
    public Image barraTiempoProgreso;
    [Tooltip("Si se acaba el tiempo sin responder, se cuenta como incorrecta.")]
    public bool autoFailOnTimeout = true;

    // estado interno
    int _currentIndex;
    int _score;
    Vector2 _questionOrigPos, _optionsOrigPos;

    // timer
    Coroutine _timerCo;
    float _timeLeft;
    bool _awaitingAnswer;

    void Awake()
    {
        _questionOrigPos = questionPanelRT.anchoredPosition;
        _optionsOrigPos = optionsGridRT.anchoredPosition;

        quizPanel.SetActive(false);
        bgResultado.SetActive(false);
        correctFeedback.gameObject.SetActive(false);
        incorrectFeedback.gameObject.SetActive(false);

        // Panel del tiempo oculto mientras el quiz está oculto
        if (barraTiempoPanel) barraTiempoPanel.SetActive(false);

        // Configurar la barra de tiempo para llenar de izq→der
        if (barraTiempoProgreso != null)
        {
            barraTiempoProgreso.type = Image.Type.Filled;
            barraTiempoProgreso.fillMethod = Image.FillMethod.Horizontal;
            barraTiempoProgreso.fillOrigin = (int)Image.OriginHorizontal.Left;
            barraTiempoProgreso.fillAmount = 0f;
        }

        // Listeners de opciones
        for (int i = 0; i < optionButtons.Length; i++)
        {
            int idx = i;
            optionButtons[i].onClick.AddListener(() => StartCoroutine(HandleAnswer(idx)));
        }

        // Retry y Exit
        retryButton.onClick.AddListener(() =>
        {
            bgResultado.SetActive(false);
            questionPanelRT.gameObject.SetActive(true);
            optionsGridRT.gameObject.SetActive(true);
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

        // Cuenta abrir el quiz como progreso de misión (según tu diseño actual)
        MissionManager.I?.NotifyEvent(MissionManager.MissionType.CompleteQuiz);

        // Desactivar control de jugador/cámara
        if (playerController != null) playerController.enabled = false;
        if (cameraController != null) cameraController.enabled = false;

        quizPanel.SetActive(true);
        bgResultado.SetActive(false);

        questionPanelRT.gameObject.SetActive(true);
        optionsGridRT.gameObject.SetActive(true);
        questionPanelRT.anchoredPosition = _questionOrigPos;
        optionsGridRT.anchoredPosition = _optionsOrigPos;

        // Mostrar panel del tiempo al iniciar
        if (barraTiempoPanel) barraTiempoPanel.SetActive(true);
        if (barraTiempoProgreso) barraTiempoProgreso.fillAmount = 0f;

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
            else optionButtons[i].gameObject.SetActive(false);
        }

        // Asegura que el panel del tiempo esté visible por cada pregunta
        if (barraTiempoPanel) barraTiempoPanel.SetActive(true);

        // Reiniciar barra y arrancar timer
        if (barraTiempoProgreso) barraTiempoProgreso.fillAmount = 0f;
        StopTimer();
        _timerCo = StartCoroutine(TimerRoutine());
    }

    IEnumerator TimerRoutine()
    {
        _awaitingAnswer = true;
        _timeLeft = Mathf.Max(0.01f, timePerQuestion);

        while (_timeLeft > 0f && _awaitingAnswer)
        {
            _timeLeft -= Time.deltaTime;
            float normalized = Mathf.Clamp01(1f - (_timeLeft / timePerQuestion)); // 0→1
            if (barraTiempoProgreso) barraTiempoProgreso.fillAmount = normalized;
            yield return null;
        }

        if (_awaitingAnswer && autoFailOnTimeout)
        {
            // Tiempo agotado → marcar como incorrecta igual que un error
            yield return StartCoroutine(HandleAnswer(-1)); // -1 = timeout
        }
    }

    void StopTimer()
    {
        if (_timerCo != null)
        {
            StopCoroutine(_timerCo);
            _timerCo = null;
        }
    }

    IEnumerator HandleAnswer(int chosenIdx)
    {
        if (!_awaitingAnswer) yield break;
        _awaitingAnswer = false;

        StopTimer();
        foreach (var b in optionButtons) b.interactable = false;

        var q = quizData.questions[_currentIndex];

        // Si es timeout (chosenIdx == -1), se toma como incorrecto
        bool correct = (chosenIdx >= 0) && (chosenIdx == q.correctIndex);
        if (correct) _score++;

        // Mostrar feedback correspondiente
        var img = correct ? correctFeedback : incorrectFeedback;
        img.transform.localScale = Vector3.zero;
        img.transform.rotation = Quaternion.identity;
        img.gameObject.SetActive(true);

        // Animación de feedback
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

        yield return new WaitForSeconds(1f);
        img.gameObject.SetActive(false);

        // Animación slide out
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

        // Avanzar o terminar
        _currentIndex++;
        if (_currentIndex < quizData.questions.Length)
        {
            questionPanelRT.anchoredPosition = qStart - Vector2.up * slide;
            optionsGridRT.anchoredPosition = oStart - Vector2.up * slide;

            ShowQuestion(_currentIndex);

            float durUp = 0.3f; t = 0f;
            while (t < durUp)
            {
                t += Time.deltaTime;
                float f = t / durUp;
                questionPanelRT.anchoredPosition = Vector2.Lerp(qStart - Vector2.up * slide, qStart, f);
                optionsGridRT.anchoredPosition = Vector2.Lerp(oStart - Vector2.up * slide, oStart, f);
                yield return null;
            }
        }
        else
        {
            EndQuiz();
        }
    }

    void EndQuiz()
    {
        StopTimer();
        _awaitingAnswer = false;

        // Oculta pregunta/opciones y panel del tiempo
        questionPanelRT.gameObject.SetActive(false);
        optionsGridRT.gameObject.SetActive(false);
        if (barraTiempoPanel) barraTiempoPanel.SetActive(false);

        // Resultado
        resultadoText.text = $"{_score}/{quizData.questions.Length}";
        float ratio = (float)_score / quizData.questions.Length;
        if (ratio >= 1f) motivacionalText.text = "¡Lo hiciste perfecto!";
        else if (ratio >= 0.6f) motivacionalText.text = "¡Muy bien, sigue así!";
        else motivacionalText.text = "¡Sigue intentando!";

        bgResultado.SetActive(true);

        FindObjectOfType<NPCDialogueFlow>()?.OnQuizFinished();
        FindObjectOfType<ArcadeInteractable>()?.UnlockArcade();
    }

    void CloseQuiz()
    {
        StopTimer();
        _awaitingAnswer = false;

        quizPanel.SetActive(false);
        bgResultado.SetActive(false);

        // Restablece UI para próxima partida
        questionPanelRT.gameObject.SetActive(true);
        optionsGridRT.gameObject.SetActive(true);
        questionPanelRT.anchoredPosition = _questionOrigPos;
        optionsGridRT.anchoredPosition = _optionsOrigPos;

        // Oculta y resetea el panel/barra del tiempo
        if (barraTiempoPanel) barraTiempoPanel.SetActive(false);
        if (barraTiempoProgreso) barraTiempoProgreso.fillAmount = 0f;

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        if (playerController != null) playerController.enabled = true;
        if (cameraController != null) cameraController.enabled = true;
    }
}
