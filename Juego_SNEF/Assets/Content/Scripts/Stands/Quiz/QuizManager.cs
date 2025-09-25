using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class QuizManager : MonoBehaviour
{
    [Header("Stand (Progress)")]
    [Tooltip("ID del stand al que pertenece este quiz (ej. 'nu', 'mx_bank_01').")]
    public string standId;
    [Tooltip("Tipo de stand para Progress (master/premier/excellence/punto).")]
    public string standType = "master";

    [Header("NPC dueño (opcional)")]
    [Tooltip("NPC que lanzó/posee este quiz. Si no lo asignas, se intentará encontrar por standId.")]
    public NPCDialogueFlow npcOwner;

    [Header("Datos del Quiz")]
    public QuizData quizData;

    [Header("UI principal del Quiz")]
    public GameObject quizPanel;
    public RectTransform questionPanelRT;
    public RectTransform optionsGridRT;

    [Header("UI: Temporizador de PREGUNTA")]
    [Tooltip("Panel/Contenedor del temporizador de la pregunta.")]
    public GameObject barraTiempoPanel;
    [Tooltip("Image de la barrita (llena de izquierda a derecha) para la pregunta.")]
    public Image barraTiempoProgreso;
    [Tooltip("Segundos por pregunta. 15 por defecto.")]
    public float timePerQuestion = 15f;
    [Tooltip("Si se acaba el tiempo sin responder, se cuenta como incorrecta con animación (sin justificación).")]
    public bool autoFailOnTimeout = true;

    [Header("UI: Panel RESPUESTA INCORRECTA (Justificación)")]
    [Tooltip("Panel que aparece para mostrar la respuesta elegida y la justificación (solo si se equivocó por clic, NO por tiempo).")]
    public GameObject panelRespuestaIncorrecta;
    [Tooltip("Texto para mostrar la respuesta que eligió el jugador.")]
    public TextMeshProUGUI textoPreguntaElegida;
    [Tooltip("Texto con la explicación/justificación de por qué es incorrecta.")]
    public TextMeshProUGUI textoJustificacion;

    [Header("Mensajes por defecto")]
    [Tooltip("Si una opción incorrecta no tiene justificación, se usará este texto.")]
    [TextArea(2, 3)]
    public string defaultJustificationText = "Puedes probar viendo otra vez los contenidos del stand :D";


    [Tooltip("Panel/Contenedor del temporizador del panel de justificación.")]
    public GameObject barraTiempoIncorrectaPanel;
    [Tooltip("Image de progreso para el panel de justificación.")]
    public Image barraTiempoIncorrectaProgreso;
    [Tooltip("Segundos que permanece visible el panel de justificación.")]
    public float timeIncorrectPanel = 4f;

    [Header("Feedback Icons (solo para 'correcta' y 'timeout')")]
    public Image correctFeedback;
    public Image incorrectFeedback;

    [Header("Pantalla de RESULTADO")]
    public GameObject bgResultado;
    public TextMeshProUGUI resultadoText;
    public TextMeshProUGUI motivacionalText;
    public Button retryButton;
    public Button exitButton;

    [Header("Redes del sponsor (opcional)")]
    [Tooltip("Contenedor general de la sección de redes (se ocultará si no hay ninguna).")]
    public GameObject redesContainer;
    [Tooltip("Contenedor del texto/botón del sitio web (se ocultará si no hay link).")]
    public GameObject sitioWebContainer;

    public Button instagramButton;
    public Button facebookButton;
    public Button xButton;          // Twitter/X
    public Button linkedinButton;
    public Button sitioWebButton;   // Texto/botón que redirige al sitio

    [Tooltip("URLs asignables desde el inspector. Si están vacías, se ocultará su botón.")]
    public string instagramURL;
    public string facebookURL;
    public string xURL;
    public string linkedinURL;
    public string sitioWebURL;

    [Header("Control de Jugador y Cámara")]
    [Tooltip("Desactiva este componente mientras el quiz está activo.")]
    public MonoBehaviour playerController;
    [Tooltip("Desactiva este componente mientras el quiz está activo.")]
    public MonoBehaviour cameraController;

    [Header("Gestión de estado (opcional)")]
    [Tooltip("Si está activo, el QuizManager desactiva/activa los controladores al abrir/cerrar.")]
    public bool manageControllers = true;
    [Tooltip("Si está activo, el QuizManager gestiona cursor visible/lock al abrir/cerrar.")]
    public bool manageCursor = true;

    // ----- Estado interno -----
    int _currentIndex;
    int _score;                 // # de correctas
    Vector2 _questionOrigPos, _optionsOrigPos;

    // Timer por pregunta
    Coroutine _timerQuestionCo;
    float _timeLeft;
    bool _awaitingAnswer;

    // Timer del panel incorrecto
    Coroutine _timerIncorrectCo;

    // Métricas de la sesión del quiz
    float _quizStartRealtime;

    // Snapshot de estado previo (para restaurar)
    bool _prevPlayerCtrlEnabled;
    bool _prevCamCtrlEnabled;
    bool _prevCursorVisible;
    CursorLockMode _prevCursorLock;

    void Awake()
    {
        _questionOrigPos = questionPanelRT ? questionPanelRT.anchoredPosition : Vector2.zero;
        _optionsOrigPos = optionsGridRT ? optionsGridRT.anchoredPosition : Vector2.zero;

        if (quizPanel) quizPanel.SetActive(false);
        if (bgResultado) bgResultado.SetActive(false);
        if (panelRespuestaIncorrecta) panelRespuestaIncorrecta.SetActive(false);

        if (correctFeedback) correctFeedback.gameObject.SetActive(false);
        if (incorrectFeedback) incorrectFeedback.gameObject.SetActive(false);

        if (barraTiempoPanel) barraTiempoPanel.SetActive(false);
        if (barraTiempoProgreso != null)
        {
            barraTiempoProgreso.type = Image.Type.Filled;
            barraTiempoProgreso.fillMethod = Image.FillMethod.Horizontal;
            barraTiempoProgreso.fillOrigin = (int)Image.OriginHorizontal.Left;
            barraTiempoProgreso.fillAmount = 0f;
        }

        if (barraTiempoIncorrectaPanel) barraTiempoIncorrectaPanel.SetActive(false);
        if (barraTiempoIncorrectaProgreso != null)
        {
            barraTiempoIncorrectaProgreso.type = Image.Type.Filled;
            barraTiempoIncorrectaProgreso.fillMethod = Image.FillMethod.Horizontal;
            barraTiempoIncorrectaProgreso.fillOrigin = (int)Image.OriginHorizontal.Left;
            barraTiempoIncorrectaProgreso.fillAmount = 0f;
        }

        // Hook a las opciones
        if (quizData == null || quizData.questions == null) return;
        if (quizData.questions.Length == 0) return;

        // Los botones se asignan en el inspector (mismo orden que las opciones)
        // Si hay más botones que opciones, los extra se ocultan en tiempo de ejecución.
        // Si hay menos, las opciones adicionales se ignorarán.
        // Conectamos los onClick aquí para evitar duplicidad.
        // (Se asignan de nuevo en StartQuiz() por seguridad si hiciste cambios en play mode).
    }

    void OnEnable()
    {
        // Conectar listeners aquí garantiza que al reactivar el GO no dupliquemos handlers.
        WireOptionButtons();
        WireResultButtons();
    }

    void WireOptionButtons()
    {
        if (questionPanelRT == null || optionsGridRT == null) return;
        if (quizData == null || quizData.questions == null) return;

        // Busca todos los Buttons hijos de optionsGridRT si no se configuró un array fijo
        // (si ya los tienes en un array público, comenta esta parte y deja tu asignación manual).
        var localButtons = optionsGridRT.GetComponentsInChildren<Button>(true);
        // Evita listeners duplicados
        foreach (var b in localButtons)
        {
            b.onClick.RemoveAllListeners();
        }
        for (int i = 0; i < localButtons.Length; i++)
        {
            int idx = i;
            localButtons[i].onClick.AddListener(() =>
            {
                StartCoroutine(HandleAnswer(idx));
            });
        }
    }

    void WireResultButtons()
    {
        if (retryButton != null)
        {
            retryButton.onClick.RemoveAllListeners();
            retryButton.onClick.AddListener(() =>
            {
                if (bgResultado) bgResultado.SetActive(false);
                if (panelRespuestaIncorrecta) panelRespuestaIncorrecta.SetActive(false);

                if (questionPanelRT) questionPanelRT.gameObject.SetActive(true);
                if (optionsGridRT) optionsGridRT.gameObject.SetActive(true);

                StartQuiz();
            });
        }

        if (exitButton != null)
        {
            exitButton.onClick.RemoveAllListeners();
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

        // Vincular NPC si no está asignado
        if (npcOwner == null && !string.IsNullOrEmpty(standId))
        {
            var npcs = FindObjectsOfType<NPCDialogueFlow>();
            foreach (var n in npcs)
            {
                if (n != null && n.standId == standId) { npcOwner = n; break; }
            }
        }

        // Marcar Fase Final desde primera apertura del quiz
        ProgressCore.I?.Stand_SetPhase(standId, "Final", standType);
        try { ProgressRemote.I?.UpdateStand(standId, standType: standType, phase: "Final"); }
        catch (System.Exception ex) { Debug.LogWarning("[QuizManager] UpdateStand(Final) al abrir quiz: " + ex.Message); }

        MissionManager.I?.NotifyEvent(MissionManager.MissionType.CompleteQuiz);

        // Snapshot del estado actual (para restaurar al cerrar)
        _prevPlayerCtrlEnabled = playerController ? playerController.enabled : false;
        _prevCamCtrlEnabled = cameraController ? cameraController.enabled : false;
        _prevCursorVisible = Cursor.visible;
        _prevCursorLock = Cursor.lockState;

        if (manageControllers)
        {
            if (playerController != null) playerController.enabled = false;
            if (cameraController != null) cameraController.enabled = false;
        }

        if (quizPanel) quizPanel.SetActive(true);
        if (bgResultado) bgResultado.SetActive(false);
        if (panelRespuestaIncorrecta) panelRespuestaIncorrecta.SetActive(false);

        if (questionPanelRT) questionPanelRT.gameObject.SetActive(true);
        if (optionsGridRT) optionsGridRT.gameObject.SetActive(true);
        if (questionPanelRT) questionPanelRT.anchoredPosition = _questionOrigPos;
        if (optionsGridRT) optionsGridRT.anchoredPosition = _optionsOrigPos;

        if (barraTiempoPanel) barraTiempoPanel.SetActive(true);
        if (barraTiempoProgreso) barraTiempoProgreso.fillAmount = 0f;

        if (manageCursor)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        _currentIndex = 0;
        _score = 0;
        _quizStartRealtime = Time.realtimeSinceStartup;

        ShowQuestion(_currentIndex);
    }

    void ShowQuestion(int index)
    {
        var q = quizData.questions[index];

        // Pintar pregunta
        if (questionPanelRT)
        {
            var qLabel = questionPanelRT.GetComponentInChildren<TextMeshProUGUI>(true);
            if (qLabel) qLabel.text = q.question;
        }

        // Pintar opciones (busca los botones hijos y rellena su TMP)
        var localButtons = optionsGridRT.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < localButtons.Length; i++)
        {
            if (i < q.options.Length)
            {
                localButtons[i].gameObject.SetActive(true);
                localButtons[i].interactable = true;

                var txt = localButtons[i].GetComponentInChildren<TextMeshProUGUI>(true);
                if (txt) txt.text = q.options[i];
            }
            else
            {
                localButtons[i].gameObject.SetActive(false);
            }
        }

        if (barraTiempoPanel) barraTiempoPanel.SetActive(true);
        if (barraTiempoProgreso) barraTiempoProgreso.fillAmount = 0f;

        StopQuestionTimer();
        _timerQuestionCo = StartCoroutine(TimerQuestionRoutine());
    }

    IEnumerator TimerQuestionRoutine()
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
            // -1 = timeout (NO panel de justificación; sí anim de incorrecto)
            yield return StartCoroutine(HandleAnswer(-1));
        }
    }

    void StopQuestionTimer()
    {
        if (_timerQuestionCo != null)
        {
            StopCoroutine(_timerQuestionCo);
            _timerQuestionCo = null;
        }
    }

    void StopIncorrectTimer()
    {
        if (_timerIncorrectCo != null)
        {
            StopCoroutine(_timerIncorrectCo);
            _timerIncorrectCo = null;
        }
    }

    IEnumerator HandleAnswer(int chosenIdx)
    {
        if (!_awaitingAnswer) yield break;
        _awaitingAnswer = false;

        StopQuestionTimer();

        // Deshabilitar botones de opciones
        var localButtons = optionsGridRT ? optionsGridRT.GetComponentsInChildren<Button>(true) : null;
        if (localButtons != null)
        {
            foreach (var b in localButtons) if (b) b.interactable = false;
        }

        var q = quizData.questions[_currentIndex];
        bool byTimeout = (chosenIdx < 0);
        bool correct = (!byTimeout && chosenIdx == q.correctIndex);
        if (correct) _score++;

        // ===== Caso A: CORRECTA  ||  TIMEOUT =====
        //   -> Usa animación de feedback (palomita/incorrecto) y pasa a la siguiente.
        if (correct || byTimeout)
        {
            var img = correct ? correctFeedback : incorrectFeedback;
            if (img != null)
            {
                img.transform.localScale = Vector3.zero;
                img.transform.rotation = Quaternion.identity;
                img.gameObject.SetActive(true);

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

            // Oculta temporizador de pregunta
            if (barraTiempoPanel) barraTiempoPanel.SetActive(false);

            // Avanzar
            yield return NextStepOrEnd();
            yield break;
        }

        // ===== Caso B: INCORRECTA por CLIC =====
        //   -> Ocultamos panel de preguntas/opciones, mostramos panelRespuestaIncorrecta con:
        //      - Texto de la opción que eligió
        //      - Justificación específica (si existe); si no, texto genérico
        if (questionPanelRT) questionPanelRT.gameObject.SetActive(false);
        if (optionsGridRT) optionsGridRT.gameObject.SetActive(false);
        if (barraTiempoPanel) barraTiempoPanel.SetActive(false);

        // Rellenar textos
        if (textoPreguntaElegida) textoPreguntaElegida.text = q.options != null && chosenIdx < q.options.Length && chosenIdx >= 0
            ? q.options[chosenIdx]
            : "(opción)";

        string just = GetJustificationSafe(q, chosenIdx);
        if (textoJustificacion) textoJustificacion.text = just;

        // Mostrar panel + timer propio
        if (panelRespuestaIncorrecta) panelRespuestaIncorrecta.SetActive(true);
        if (barraTiempoIncorrectaPanel) barraTiempoIncorrectaPanel.SetActive(true);
        if (barraTiempoIncorrectaProgreso) barraTiempoIncorrectaProgreso.fillAmount = 0f;

        StopIncorrectTimer();
        _timerIncorrectCo = StartCoroutine(TimerIncorrectPanelRoutine());
        yield return _timerIncorrectCo; // esperar a que termine

        // Ocultar panel incorrecto y avanzar
        if (panelRespuestaIncorrecta) panelRespuestaIncorrecta.SetActive(false);
        if (barraTiempoIncorrectaPanel) barraTiempoIncorrectaPanel.SetActive(false);

        yield return NextStepOrEnd();
    }

    IEnumerator TimerIncorrectPanelRoutine()
    {
        float t = 0f;
        float dur = Mathf.Max(0.01f, timeIncorrectPanel);

        while (t < dur)
        {
            t += Time.deltaTime;
            float normalized = Mathf.Clamp01(t / dur);
            if (barraTiempoIncorrectaProgreso) barraTiempoIncorrectaProgreso.fillAmount = normalized;
            yield return null;
        }
    }

    string GetJustificationSafe(QuizQuestion qq, int chosenIdx)
    {
        // Si existe justificación específica y no está vacía, úsala
        if (qq != null && qq.optionJustifications != null &&
            chosenIdx >= 0 && chosenIdx < qq.optionJustifications.Length)
        {
            var j = qq.optionJustifications[chosenIdx];
            if (!string.IsNullOrWhiteSpace(j)) return j;
        }

        // Fallback configurable desde el inspector
        return string.IsNullOrWhiteSpace(defaultJustificationText)
            ? "Puedes probar viendo otra vez los contenidos del stand :D"
            : defaultJustificationText;
    }


    IEnumerator NextStepOrEnd()
    {
        _currentIndex++;

        if (_currentIndex < quizData.questions.Length)
        {
            // Preparar siguiente pregunta
            if (questionPanelRT) questionPanelRT.gameObject.SetActive(true);
            if (optionsGridRT) optionsGridRT.gameObject.SetActive(true);

            if (questionPanelRT) questionPanelRT.anchoredPosition = _questionOrigPos;
            if (optionsGridRT) optionsGridRT.anchoredPosition = _optionsOrigPos;

            ShowQuestion(_currentIndex);
        }
        else
        {
            EndQuiz();
        }

        yield break;
    }

    void EndQuiz()
    {
        StopQuestionTimer();
        StopIncorrectTimer();
        _awaitingAnswer = false;

        // Oculta paneles de pregunta
        if (questionPanelRT) questionPanelRT.gameObject.SetActive(false);
        if (optionsGridRT) optionsGridRT.gameObject.SetActive(false);
        if (barraTiempoPanel) barraTiempoPanel.SetActive(false);
        if (panelRespuestaIncorrecta) panelRespuestaIncorrecta.SetActive(false);
        if (barraTiempoIncorrectaPanel) barraTiempoIncorrectaPanel.SetActive(false);

        // ----- PROGRESO -----
        ProgressCore.I?.Stand_RecordQuiz(standId, _score, quizData.questions.Length);

        int total = quizData.questions.Length;
        float elapsed = Mathf.Max(0f, Time.realtimeSinceStartup - _quizStartRealtime);
        int ms = Mathf.RoundToInt(elapsed * 1000f);

        float ratio = total > 0 ? (float)_score / total : 0f;
        int stars = (ratio >= 0.9f) ? 3 : (ratio >= 0.7f) ? 2 : (ratio >= 0.5f) ? 1 : 0;

        try
        {
            if (ProgressRemote.I != null && !string.IsNullOrEmpty(standId))
            {
                ProgressRemote.I.PostQuizResult(standId, _score, stars, _score, total, ms);
                ProgressRemote.I.UpdateStand(standId, standType: standType, phase: "Final", screensViewed: null, quizUnlocked: true);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[QuizManager] ProgressRemote falló: " + ex.Message);
        }

        // ----- UI de resultado -----
        if (resultadoText) resultadoText.text = $"{_score}/{total}";
        if (motivacionalText)
        {
            if (ratio >= 1f) motivacionalText.text = "¡Lo hiciste perfecto!";
            else if (ratio >= .6) motivacionalText.text = "¡Muy bien, sigue así!";
            else motivacionalText.text = "¡Sigue intentando!";
        }

        SetupSponsorLinksUI(); // activar/ocultar botones según URLs

        if (bgResultado) bgResultado.SetActive(true);

        // Notificar a sistemas
        (npcOwner != null ? npcOwner : FindObjectOfType<NPCDialogueFlow>())?.OnQuizFinished();
        FindObjectOfType<ArcadeInteractable>()?.UnlockArcade();
    }

    void SetupSponsorLinksUI()
    {
        // Sitio web
        if (sitioWebContainer) sitioWebContainer.SetActive(!string.IsNullOrWhiteSpace(sitioWebURL));
        if (sitioWebButton != null)
        {
            sitioWebButton.onClick.RemoveAllListeners();
            if (!string.IsNullOrWhiteSpace(sitioWebURL))
                sitioWebButton.onClick.AddListener(() => Application.OpenURL(sitioWebURL));
        }

        int activeCount = 0;

        // Instagram
        if (instagramButton)
        {
            bool has = !string.IsNullOrWhiteSpace(instagramURL);
            instagramButton.gameObject.SetActive(has);
            instagramButton.onClick.RemoveAllListeners();
            if (has) { instagramButton.onClick.AddListener(() => Application.OpenURL(instagramURL)); activeCount++; }
        }

        // Facebook
        if (facebookButton)
        {
            bool has = !string.IsNullOrWhiteSpace(facebookURL);
            facebookButton.gameObject.SetActive(has);
            facebookButton.onClick.RemoveAllListeners();
            if (has) { facebookButton.onClick.AddListener(() => Application.OpenURL(facebookURL)); activeCount++; }
        }

        // X / Twitter
        if (xButton)
        {
            bool has = !string.IsNullOrWhiteSpace(xURL);
            xButton.gameObject.SetActive(has);
            xButton.onClick.RemoveAllListeners();
            if (has) { xButton.onClick.AddListener(() => Application.OpenURL(xURL)); activeCount++; }
        }

        // LinkedIn
        if (linkedinButton)
        {
            bool has = !string.IsNullOrWhiteSpace(linkedinURL);
            linkedinButton.gameObject.SetActive(has);
            linkedinButton.onClick.RemoveAllListeners();
            if (has) { linkedinButton.onClick.AddListener(() => Application.OpenURL(linkedinURL)); activeCount++; }
        }

        // Oculta contenedor de redes si no hay ninguna
        if (redesContainer) redesContainer.SetActive(activeCount > 0);
    }

    void CloseQuiz()
    {
        StopQuestionTimer();
        StopIncorrectTimer();
        _awaitingAnswer = false;

        if (quizPanel) quizPanel.SetActive(false);
        if (bgResultado) bgResultado.SetActive(false);
        if (panelRespuestaIncorrecta) panelRespuestaIncorrecta.SetActive(false);

        // Restablece UI para próxima partida
        if (questionPanelRT) questionPanelRT.gameObject.SetActive(true);
        if (optionsGridRT) optionsGridRT.gameObject.SetActive(true);
        if (questionPanelRT) questionPanelRT.anchoredPosition = _questionOrigPos;
        if (optionsGridRT) optionsGridRT.anchoredPosition = _optionsOrigPos;

        if (barraTiempoPanel) barraTiempoPanel.SetActive(false);
        if (barraTiempoProgreso) barraTiempoProgreso.fillAmount = 0f;
        if (barraTiempoIncorrectaPanel) barraTiempoIncorrectaPanel.SetActive(false);
        if (barraTiempoIncorrectaProgreso) barraTiempoIncorrectaProgreso.fillAmount = 0f;

        // Restaurar estado previo (convivir con focus del NPC)
        if (manageCursor)
        {
            Cursor.visible = _prevCursorVisible;
            Cursor.lockState = _prevCursorLock;
        }
        if (manageControllers)
        {
            if (playerController != null) playerController.enabled = _prevPlayerCtrlEnabled;
            if (cameraController != null) cameraController.enabled = _prevCamCtrlEnabled;
        }

        // Re-vincular NPC si hace falta y notificar
        if (npcOwner == null && !string.IsNullOrEmpty(standId))
        {
            var npcs = FindObjectsOfType<NPCDialogueFlow>();
            foreach (var n in npcs)
            {
                if (n != null && n.standId == standId) { npcOwner = n; break; }
            }
        }
        npcOwner?.OnQuizFinished();
    }
}
