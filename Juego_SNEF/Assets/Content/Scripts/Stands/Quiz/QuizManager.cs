using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class QuizManager : MonoBehaviour
{
    [Header("Stand (Progress)")]
    [Tooltip("ID del stand al que pertenece este quiz (ej. 'nu', 'mx_bank_01').")]
    public string standId;

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
    [Tooltip("Desactiva este componente mientras el quiz está activo.")]
    public MonoBehaviour playerController;
    [Tooltip("Desactiva este componente mientras el quiz está activo.")]
    public MonoBehaviour cameraController;

    [Header("Tiempo por pregunta")]
    [Tooltip("Segundos por pregunta. 15 por defecto.")]
    public float timePerQuestion = 15f;

    [Header("UI del temporizador")]
    [Tooltip("Panel/Contenedor del temporizador (se mostrará durante la pregunta y se ocultará al ver resultados).")]
    public GameObject barraTiempoPanel;
    [Tooltip("Image de la barrita (llena de izquierda a derecha).")]
    public Image barraTiempoProgreso;
    [Tooltip("Si se acaba el tiempo sin responder, se cuenta como incorrecta.")]
    public bool autoFailOnTimeout = true;

    // ----- Estado interno -----
    int _currentIndex;
    int _score;                 // # de correctas
    Vector2 _questionOrigPos, _optionsOrigPos;

    // Timer por pregunta
    Coroutine _timerCo;
    float _timeLeft;
    bool _awaitingAnswer;

    // Métricas de la sesión del quiz
    float _quizStartRealtime;   // para calcular ms totales

    void Awake()
    {
        _questionOrigPos = questionPanelRT.anchoredPosition;
        _optionsOrigPos = optionsGridRT.anchoredPosition;

        if (quizPanel) quizPanel.SetActive(false);
        if (bgResultado) bgResultado.SetActive(false);
        if (correctFeedback) correctFeedback.gameObject.SetActive(false);
        if (incorrectFeedback) incorrectFeedback.gameObject.SetActive(false);

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
        if (optionButtons != null)
        {
            for (int i = 0; i < optionButtons.Length; i++)
            {
                int idx = i;
                if (optionButtons[i] != null)
                    optionButtons[i].onClick.AddListener(() => StartCoroutine(HandleAnswer(idx)));
            }
        }

        // Retry y Exit
        if (retryButton != null)
        {
            retryButton.onClick.AddListener(() =>
            {
                if (bgResultado) bgResultado.SetActive(false);
                if (questionPanelRT) questionPanelRT.gameObject.SetActive(true);
                if (optionsGridRT) optionsGridRT.gameObject.SetActive(true);
                StartQuiz();
            });
        }

        if (exitButton != null)
        {
            exitButton.onClick.AddListener(CloseQuiz);
        }
    }

    public void StartQuiz()
    {
        if (quizData == null || quizData.questions == null || quizData.questions.Length == 0)
        {
            Debug.LogWarning("[QuizManager] QuizData no asignado o sin preguntas.");
            return;
        }

        // Evento de misión (si tu sistema lo usa)
        MissionManager.I?.NotifyEvent(MissionManager.MissionType.CompleteQuiz);

        // Desactivar control de jugador/cámara
        if (playerController != null) playerController.enabled = false;
        if (cameraController != null) cameraController.enabled = false;

        if (quizPanel) quizPanel.SetActive(true);
        if (bgResultado) bgResultado.SetActive(false);

        if (questionPanelRT) questionPanelRT.gameObject.SetActive(true);
        if (optionsGridRT) optionsGridRT.gameObject.SetActive(true);
        if (questionPanelRT) questionPanelRT.anchoredPosition = _questionOrigPos;
        if (optionsGridRT) optionsGridRT.anchoredPosition = _optionsOrigPos;

        // Mostrar panel del tiempo al iniciar
        if (barraTiempoPanel) barraTiempoPanel.SetActive(true);
        if (barraTiempoProgreso) barraTiempoProgreso.fillAmount = 0f;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        _currentIndex = 0;
        _score = 0;
        _quizStartRealtime = Time.realtimeSinceStartup;

        ShowQuestion(_currentIndex);
    }

    void ShowQuestion(int index)
    {
        var q = quizData.questions[index];
        if (questionText) questionText.text = q.question;

        for (int i = 0; i < optionButtons.Length; i++)
        {
            if (optionButtons[i] == null) continue;

            if (i < q.options.Length)
            {
                optionButtons[i].gameObject.SetActive(true);
                optionButtons[i].interactable = true;

                var txt = optionButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                if (txt) txt.text = q.options[i];
            }
            else
            {
                optionButtons[i].gameObject.SetActive(false);
            }
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
        if (optionButtons != null)
        {
            foreach (var b in optionButtons)
                if (b != null) b.interactable = false;
        }

        var q = quizData.questions[_currentIndex];

        // Si es timeout (chosenIdx == -1), se toma como incorrecto
        bool correct = (chosenIdx >= 0) && (chosenIdx == q.correctIndex);
        if (correct) _score++;

        // Mostrar feedback correspondiente
        var img = correct ? correctFeedback : incorrectFeedback;
        if (img != null)
        {
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
        }

        // Animación slide out
        Vector2 qStart = _questionOrigPos;
        Vector2 oStart = _optionsOrigPos;
        float slide = (questionPanelRT.rect.height + optionsGridRT.rect.height) + 20f;

        float t2 = 0f, dur2 = 0.3f;
        while (t2 < dur2)
        {
            t2 += Time.deltaTime;
            float f = t2 / dur2;
            if (questionPanelRT) questionPanelRT.anchoredPosition = Vector2.Lerp(qStart, qStart - Vector2.up * slide, f);
            if (optionsGridRT) optionsGridRT.anchoredPosition = Vector2.Lerp(oStart, oStart - Vector2.up * slide, f);
            yield return null;
        }

        // Avanzar o terminar
        _currentIndex++;
        if (_currentIndex < quizData.questions.Length)
        {
            if (questionPanelRT) questionPanelRT.anchoredPosition = qStart - Vector2.up * slide;
            if (optionsGridRT) optionsGridRT.anchoredPosition = oStart - Vector2.up * slide;

            ShowQuestion(_currentIndex);

            float durUp = 0.3f; float t3 = 0f;
            while (t3 < durUp)
            {
                t3 += Time.deltaTime;
                float f = t3 / durUp;
                if (questionPanelRT) questionPanelRT.anchoredPosition = Vector2.Lerp(qStart - Vector2.up * slide, qStart, f);
                if (optionsGridRT) optionsGridRT.anchoredPosition = Vector2.Lerp(oStart - Vector2.up * slide, oStart, f);
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
        if (questionPanelRT) questionPanelRT.gameObject.SetActive(false);
        if (optionsGridRT) optionsGridRT.gameObject.SetActive(false);
        if (barraTiempoPanel) barraTiempoPanel.SetActive(false);

        // ----- SINCRONIZACIÓN DE PROGRESO -----
        // Cache local (si tu ProgressCore guarda el resultado):
        ProgressCore.I?.Stand_RecordQuiz(standId, _score, quizData.questions.Length);
        // (Antes hacías SaveNow aquí; lo dejamos como cache local opcional)
        // ProgressCore.I?.SaveNow("stand_quiz_result_" + standId);

        // Remoto granular (si añadiste ProgressRemote al proyecto):
        int total = quizData.questions.Length;
        float elapsed = Mathf.Max(0f, Time.realtimeSinceStartup - _quizStartRealtime);
        int ms = Mathf.RoundToInt(elapsed * 1000f);

        // Calcula estrellas con umbrales típicos (ajusta a tu diseño)
        float ratio = total > 0 ? (float)_score / total : 0f;
        int stars = (ratio >= 0.9f) ? 3 : (ratio >= 0.7f) ? 2 : (ratio >= 0.5f) ? 1 : 0;

        if (ProgressRemote.I != null && !string.IsNullOrEmpty(standId))
        {
            // Resultado del minijuego/quiz
            ProgressRemote.I.PostQuizResult(standId, _score, stars, _score, total, ms);
            // Actualiza fase del stand (Final) y marcar quiz desbloqueado/completado
            ProgressRemote.I.UpdateStand(standId, standType: "master", phase: "Final", screensViewed: null, quizUnlocked: true);
        }

        // ----- UI de resultado -----
        if (resultadoText) resultadoText.text = $"{_score}/{total}";
        if (motivacionalText)
        {
            if (ratio >= 1f) motivacionalText.text = "¡Lo hiciste perfecto!";
            else if (ratio >= 0.6f) motivacionalText.text = "¡Muy bien, sigue así!";
            else motivacionalText.text = "¡Sigue intentando!";
        }

        if (bgResultado) bgResultado.SetActive(true);

        // Notifica a otros sistemas (si existen estos métodos en tus scripts)
        FindObjectOfType<NPCDialogueFlow>()?.OnQuizFinished();
        FindObjectOfType<ArcadeInteractable>()?.UnlockArcade();
    }

    void CloseQuiz()
    {
        StopTimer();
        _awaitingAnswer = false;

        if (quizPanel) quizPanel.SetActive(false);
        if (bgResultado) bgResultado.SetActive(false);

        // Restablece UI para próxima partida
        if (questionPanelRT) questionPanelRT.gameObject.SetActive(true);
        if (optionsGridRT) optionsGridRT.gameObject.SetActive(true);
        if (questionPanelRT) questionPanelRT.anchoredPosition = _questionOrigPos;
        if (optionsGridRT) optionsGridRT.anchoredPosition = _optionsOrigPos;

        // Oculta y resetea el panel/barra del tiempo
        if (barraTiempoPanel) barraTiempoPanel.SetActive(false);
        if (barraTiempoProgreso) barraTiempoProgreso.fillAmount = 0f;

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        if (playerController != null) playerController.enabled = true;
        if (cameraController != null) cameraController.enabled = true;
    }
}
