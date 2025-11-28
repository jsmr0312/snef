using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class NPCDialogueMiniFlow : MonoBehaviour,
    Interactor.IInteractable,
    Interactor.IInteractableFeedback
{
    [System.Serializable]
    public class Choice
    {
        [TextArea(1, 3)] public string playerText;
        [TextArea(2, 4)] public string npcResponse;

        [Header("Acciones opcionales al elegir")]
        public UnityEvent onChosen;

        [Header("Escena opcional a cargar con 'Entendido'")]
        public string sceneToLoadOnUnderstood; // si está vacío usa fallback global
    }

    [Header("Dialogo (Pregunta + 3 opciones)")]
    [TextArea(2, 4)] public string npcQuestion;
    public Choice[] choices = new Choice[3];

    [Header("Scene fallback (si Choice no define escena)")]
    public string fallbackMinigameScene;

    [Header("UI (burbuja + texto)")]
    public GameObject dialogueBubble;
    public TextMeshProUGUI dialogueText;
    public float typeSpeed = 0.03f;

    [Header("UI (Prompt para iniciar)")]
    public GameObject promptUI;
    public Button promptOpenButton;
    public bool lookAtCamera = true;

    [Header("UI (Opciones)")]
    public GameObject optionsRoot;                 // contenedor de los 3 botones
    public Button[] optionButtons = new Button[3]; // 3 botones
    public TextMeshProUGUI[] optionLabels = new TextMeshProUGUI[3];

    [Header("UI (Entendido / Cerrar)")]
    public Button understoodButton;
    public Button closeButton;

    [Header("Outline (QuickOutline)")]
    public global::Outline outline;
    public bool enableOutlineOnProximity = true;
    public global::Outline.Mode outlineModeNear = global::Outline.Mode.OutlineVisible;
    public Color outlineColorNear = Color.cyan;
    [Range(0, 10f)] public float outlineWidthNear = 4f;
    public bool disableOutlineWhileTalking = true;

    [Header("Auto-cierre")]
    public bool closeWhenFar = true;
    public float closeDistance = 5f;
    public bool closeWhenGazeLost = true;
    public float gazeLostGrace = 0.4f;

    [Header("Jugadores (para distancia)")]
    public Transform[] playerTransforms;

    [Header("Focus Camera (opcional)")]
    public bool focusCameraOnTalk = false;
    public Camera mainCamera;
    public Transform focusViewpoint;

    [Header("Auto-create viewpoint (si no asignas focusViewpoint)")]
    public bool autoCreateViewpointIfNull = true;
    public Vector3 autoViewLocalOffset = new Vector3(0.8f, 1.65f, 1.6f);

    public bool detachCameraFromParentDuringFocus = true;
    public bool disableCinemachineDuringFocus = true;
    public Transform reparentFallback;
    public float cameraTransitionDuration = 0.6f;

    [Header("Freeze + UI Player")]
    public MonoBehaviour[] controllersToFreeze;
    public GameObject playerUI;
    public GameObject playerRoot;
    public bool disablePlayerRootDuringFocus = false;

    [Header("Keyboard Interact (opcional)")]
    public bool enableKeyboardInteract = true;
    public KeyCode interactKey = KeyCode.E;
    public bool allowKeyboardWithoutGaze = false;

    [Header("Extras")]
    public bool allowEscToClose = true;

    // ---- estado ----
    private enum Step { Idle, ShowingQuestion, WaitingChoice, ShowingResponse, Done }
    private Step _step = Step.Idle;

    private bool _inConversation = false;
    private int _chosenIndex = -1;

    private Coroutine _typingRoutine;
    private string _typingFullText = "";

    private bool _lostGaze = false;
    private float _gazeLostAt = 0f;

    private bool _canKeyboardInteract = false;

    // ---- focus/cámara ----
    private static NPCDialogueMiniFlow _focusOwner;
    private bool _isFocused = false;
    private Vector3 _camOrigPos;
    private Quaternion _camOrigRot;
    private Transform _camOrigParent;
    private Behaviour _cineBrain;
    private bool _cineBrainWasEnabled = false;
    private bool _playerRootPrevActive = false;
    private Coroutine _transitionRoutine;

    void Awake()
    {
        if (!mainCamera) mainCamera = Camera.main;

        // Auto-crear viewpoint si hace falta (para que la cámara sí se mueva)
        if (focusCameraOnTalk && focusViewpoint == null && autoCreateViewpointIfNull)
        {
            var vp = new GameObject("NPC_FocusViewpoint").transform;
            vp.SetParent(transform, false);
            vp.localPosition = autoViewLocalOffset;

            // Mira hacia el NPC (un poquito arriba para que no apunte a los pies)
            Vector3 lookTarget = transform.position + Vector3.up * 1.2f;
            vp.position = vp.position; // (solo para claridad)
            vp.LookAt(lookTarget, Vector3.up);

            focusViewpoint = vp;
        }

        if (promptUI) promptUI.SetActive(false);
        if (dialogueBubble) dialogueBubble.SetActive(false);
        SetOptionsVisible(false);

        if (!promptOpenButton && promptUI)
            promptOpenButton = promptUI.GetComponentInChildren<Button>(true);

        // auto-resolver botones de opciones si faltan
        AutoResolveOptionButtons();

        // auto-resolver understood/close si no están set
        if (!understoodButton && dialogueBubble)
        {
            foreach (var b in dialogueBubble.GetComponentsInChildren<Button>(true))
            {
                var n = b.name.ToLower();
                if (n.Contains("understood") || n.Contains("entendido")) { understoodButton = b; break; }
            }
        }
        if (!closeButton && dialogueBubble)
        {
            foreach (var b in dialogueBubble.GetComponentsInChildren<Button>(true))
            {
                var n = b.name.ToLower();
                if (n.Contains("close") || n.Contains("cerrar")) { closeButton = b; break; }
            }
        }

        if (outline == null) outline = GetComponent<global::Outline>() ?? GetComponentInChildren<global::Outline>();
        if (outline) outline.enabled = false;

        BindPromptOpen(false);

        HookButton(understoodButton, OnUnderstoodClicked);
        HookButton(closeButton, EndConversation);

        if (understoodButton) understoodButton.gameObject.SetActive(false);
        if (closeButton) closeButton.gameObject.SetActive(false);

        // opciones: listeners limpios
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            HookButton(optionButtons[i], () => OnChoiceClicked(idx));
        }
        RefreshOptionTexts();
    }

    private bool InteractPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null) return false;

        // Soporte básico para tu caso (E). Si cambias de tecla, lo ampliamos.
        return kb.eKey.wasPressedThisFrame;
#else
    return Input.GetKeyDown(interactKey);
#endif
    }


    void Update()
    {
        // ✅ Tecla E (o la que pongas): interactuar
        if (enableKeyboardInteract && InteractPressedThisFrame())

        {
            if (allowKeyboardWithoutGaze || _canKeyboardInteract)
                Interact();
        }

        if (_inConversation && closeWhenFar && !_isFocused)
        {
            if (GetNearestPlayerDistance() > closeDistance)
                EndConversation();
        }

        if (_inConversation && closeWhenGazeLost && _lostGaze && !_isFocused)
        {
            if (Time.time - _gazeLostAt >= gazeLostGrace)
                EndConversation();
        }

        if (_isFocused && allowEscToClose && Input.GetKeyDown(KeyCode.Escape))
            EndConversation();
    }

    void LateUpdate()
    {
        if (!lookAtCamera) return;
        var cam = Camera.main ? Camera.main.transform : null;
        if (!cam) return;

        if (promptUI && promptUI.activeSelf)
        {
            promptUI.transform.LookAt(cam);
            promptUI.transform.Rotate(0, 180, 0);
        }
        if (dialogueBubble && dialogueBubble.activeSelf)
        {
            dialogueBubble.transform.LookAt(cam);
            dialogueBubble.transform.Rotate(0, 180, 0);
        }
    }

    void OnDisable() { ForceReleaseFocusIfStuck(); }
    void OnDestroy() { ForceReleaseFocusIfStuck(); }

    // =========================================================
    //  Interactor feedback
    // =========================================================
    public void OnGazeEnter()
    {
        _lostGaze = false;
        _canKeyboardInteract = true;

        if (!_inConversation)
        {
            if (enableOutlineOnProximity && outline)
            {
                ApplyOutlineSettings();
                outline.enabled = true;
            }
            if (promptUI) promptUI.SetActive(true);
        }

        BindPromptOpen(true);
    }

    public void OnGazeExit()
    {
        BindPromptOpen(false);
        _canKeyboardInteract = false;

        if (_isFocused) return;

        if (promptUI) promptUI.SetActive(false);

        if (!_inConversation)
        {
            if (dialogueBubble) dialogueBubble.SetActive(false);
            if (outline) outline.enabled = false;
            ResetFlow();
            return;
        }

        _lostGaze = true;
        _gazeLostAt = Time.time;
    }

    // =========================================================
    //  Interact (E / botón prompt)
    // =========================================================
    public void Interact()
    {
        // Si estás tipeando: “skip” al full text
        if (_typingRoutine != null && dialogueText != null)
        {
            StopCoroutine(_typingRoutine);
            _typingRoutine = null;
            dialogueText.text = _typingFullText;
            OnTypingFinished();
            return;
        }

        // Si ya estamos esperando elección o ya terminó, no avances con Interact.
        if (_step == Step.WaitingChoice || _step == Step.Done) return;

        StartConversationIfNeeded();

        if (_step == Step.Idle)
        {
            ShowQuestion();
        }
    }

    private void StartConversationIfNeeded()
    {
        if (_inConversation) return;

        _inConversation = true;
        _lostGaze = false;

        if (promptUI) promptUI.SetActive(false);
        if (dialogueBubble) dialogueBubble.SetActive(true);

        if (disableOutlineWhileTalking && outline) outline.enabled = false;

        if (focusCameraOnTalk && !_isFocused) EnterFocusMode();

        if (playerUI) playerUI.SetActive(false);

        if (closeButton) closeButton.gameObject.SetActive(_isFocused);
    }

    // =========================================================
    //  Flujo
    // =========================================================
    private void ShowQuestion()
    {
        _step = Step.ShowingQuestion;
        _chosenIndex = -1;
        if (understoodButton) understoodButton.gameObject.SetActive(false);
        SetOptionsVisible(false);
        ShowLine(npcQuestion);
    }

    private void OnChoiceClicked(int idx)
    {
        if (!_inConversation) return;
        if (_step != Step.WaitingChoice) return;
        if (idx < 0 || idx >= 3) return;

        _chosenIndex = idx;

        var c = GetChoice(idx);
        c?.onChosen?.Invoke();

        SetOptionsVisible(false);
        _step = Step.ShowingResponse;
        ShowLine(c != null ? c.npcResponse : "");
    }

    private void OnUnderstoodClicked()
    {
        string scene = "";
        var c = GetChoice(_chosenIndex);
        if (c != null && !string.IsNullOrWhiteSpace(c.sceneToLoadOnUnderstood))
            scene = c.sceneToLoadOnUnderstood;
        else
            scene = fallbackMinigameScene;

        if (!string.IsNullOrWhiteSpace(scene))
        {
            EndConversation(hardCloseUI: true);
            SceneManager.LoadScene(scene);
        }
        else
        {
            EndConversation();
        }
    }

    private void OnTypingFinished()
    {
        if (_step == Step.ShowingQuestion)
        {
            _step = Step.WaitingChoice;
            SetOptionsVisible(true);
        }
        else if (_step == Step.ShowingResponse)
        {
            _step = Step.Done;
            if (understoodButton) understoodButton.gameObject.SetActive(true);
        }

        if (_isFocused && closeButton) closeButton.gameObject.SetActive(true);
    }

    public void EndConversation() => EndConversation(false);

    public void EndConversation(bool hardCloseUI)
    {
        _inConversation = false;
        _lostGaze = false;

        if (_typingRoutine != null) { StopCoroutine(_typingRoutine); _typingRoutine = null; }

        if (dialogueBubble) dialogueBubble.SetActive(false);
        if (understoodButton) understoodButton.gameObject.SetActive(false);
        SetOptionsVisible(false);

        // ✅ SOLO restaura cámara si realmente estabas en focus
        if (_isFocused) HardRestoreFocusNow();

        if (!hardCloseUI)
        {
            if (enableOutlineOnProximity && outline)
            {
                ApplyOutlineSettings();
                outline.enabled = true;
            }
            if (promptUI) promptUI.SetActive(true);
            BindPromptOpen(true);
        }

        ResetFlow();
    }

    private void ResetFlow()
    {
        _step = Step.Idle;
        _chosenIndex = -1;
        RefreshOptionTexts();
    }

    // =========================================================
    //  Typewriter
    // =========================================================
    private void ShowLine(string line)
    {
        if (_typingRoutine != null) StopCoroutine(_typingRoutine);
        _typingRoutine = StartCoroutine(TypeText(line ?? ""));
    }

    private IEnumerator TypeText(string fullText)
    {
        _typingFullText = fullText ?? "";
        if (dialogueText == null) { _typingRoutine = null; yield break; }

        if (typeSpeed <= 0f)
        {
            dialogueText.text = _typingFullText;
        }
        else
        {
            dialogueText.text = "";
            for (int i = 1; i <= _typingFullText.Length; i++)
            {
                dialogueText.text = _typingFullText.Substring(0, i);
                yield return new WaitForSeconds(typeSpeed);
            }
        }

        _typingRoutine = null;
        OnTypingFinished();
    }

    // =========================================================
    //  UI helpers
    // =========================================================
    private void SetOptionsVisible(bool visible)
    {
        if (optionsRoot) optionsRoot.SetActive(visible);

        for (int i = 0; i < 3; i++)
            if (optionButtons != null && i < optionButtons.Length && optionButtons[i])
                optionButtons[i].gameObject.SetActive(visible);
    }

    private void RefreshOptionTexts()
    {
        for (int i = 0; i < 3; i++)
        {
            var c = GetChoice(i);
            if (optionLabels != null && i < optionLabels.Length && optionLabels[i])
                optionLabels[i].text = c != null ? c.playerText : $"Opción {i + 1}";
        }
    }

    private Choice GetChoice(int i)
    {
        if (choices == null || choices.Length < 3) return null;
        if (i < 0 || i >= choices.Length) return null;
        return choices[i];
    }

    private void BindPromptOpen(bool bind)
    {
        if (!promptOpenButton) return;
        promptOpenButton.onClick.RemoveAllListeners();
        if (bind) promptOpenButton.onClick.AddListener(Interact);
    }

    private void AutoResolveOptionButtons()
    {
        if (optionsRoot == null) return;

        var btns = optionsRoot.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < 3; i++)
        {
            if (!optionButtons[i] && i < btns.Length) optionButtons[i] = btns[i];
        }

        for (int i = 0; i < 3; i++)
        {
            if (optionButtons[i] && !optionLabels[i])
                optionLabels[i] = optionButtons[i].GetComponentInChildren<TextMeshProUGUI>(true);
        }
    }

    private void ApplyOutlineSettings()
    {
        if (!outline) return;
        outline.OutlineMode = outlineModeNear;
        outline.OutlineColor = outlineColorNear;
        outline.OutlineWidth = outlineWidthNear;
    }

    private float GetNearestPlayerDistance()
    {
        float min = float.MaxValue;

        if (playerTransforms != null && playerTransforms.Length > 0)
        {
            for (int i = 0; i < playerTransforms.Length; i++)
            {
                var t = playerTransforms[i];
                if (!t) continue;
                float d = Vector3.Distance(t.position, transform.position);
                if (d < min) min = d;
            }
        }
        else if (Camera.main)
        {
            min = Vector3.Distance(Camera.main.transform.position, transform.position);
        }

        return min;
    }

    // =========================================================
    //  Focus Mode (cámara + freeze)
    // =========================================================
    private void EnterFocusMode()
    {
        if (!mainCamera) mainCamera = Camera.main;

        // Si focusViewpoint seguía null por alguna razón, intenta crearlo aquí también.
        if (focusViewpoint == null && autoCreateViewpointIfNull)
        {
            var vp = new GameObject("NPC_FocusViewpoint").transform;
            vp.SetParent(transform, false);
            vp.localPosition = autoViewLocalOffset;
            vp.LookAt(transform.position + Vector3.up * 1.2f, Vector3.up);
            focusViewpoint = vp;
        }

        if (mainCamera == null || focusViewpoint == null) return;
        if (_isFocused) return;

        if (_focusOwner != null && _focusOwner != this)
            _focusOwner.ForceReleaseFocusIfStuck();
        _focusOwner = this;

        _isFocused = true;

        _camOrigPos = mainCamera.transform.position;
        _camOrigRot = mainCamera.transform.rotation;

        if (disableCinemachineDuringFocus)
        {
            _cineBrain = mainCamera.GetComponent("CinemachineBrain") as Behaviour;
            if (_cineBrain != null) { _cineBrainWasEnabled = _cineBrain.enabled; _cineBrain.enabled = false; }
        }

        if (detachCameraFromParentDuringFocus)
        {
            _camOrigParent = mainCamera.transform.parent;
            if (mainCamera.transform.parent != null)
                mainCamera.transform.SetParent(null, true);
        }

        if (disablePlayerRootDuringFocus && playerRoot != null)
        {
            _playerRootPrevActive = playerRoot.activeSelf;
            playerRoot.SetActive(false);
        }

        if (controllersToFreeze != null)
            foreach (var c in controllersToFreeze) if (c) c.enabled = false;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        StartOrRestartTransition(_camOrigPos, focusViewpoint.position, _camOrigRot, focusViewpoint.rotation, () =>
        {
            if (closeButton) closeButton.gameObject.SetActive(true);
        });
    }

    private void HardRestoreFocusNow()
    {
        if (_transitionRoutine != null) { StopCoroutine(_transitionRoutine); _transitionRoutine = null; }
        if (closeButton) closeButton.gameObject.SetActive(false);

        if (mainCamera != null)
        {
            mainCamera.transform.SetPositionAndRotation(_camOrigPos, _camOrigRot);

            if (detachCameraFromParentDuringFocus && mainCamera.transform.parent == null)
            {
                Transform targetParent = _camOrigParent != null ? _camOrigParent : reparentFallback;
                if (targetParent != null) mainCamera.transform.SetParent(targetParent, true);
            }
        }

        if (controllersToFreeze != null)
            foreach (var c in controllersToFreeze) if (c) c.enabled = true;

        if (disablePlayerRootDuringFocus && playerRoot != null)
            playerRoot.SetActive(_playerRootPrevActive);

        if (_cineBrain != null)
        {
            _cineBrain.enabled = _cineBrainWasEnabled;
            _cineBrain = null;
        }

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        if (playerUI) playerUI.SetActive(true);

        _isFocused = false;
        if (_focusOwner == this) _focusOwner = null;
    }

    private void StartOrRestartTransition(
        Vector3 fromPos, Vector3 toPos,
        Quaternion fromRot, Quaternion toRot,
        System.Action onComplete)
    {
        if (_transitionRoutine != null) StopCoroutine(_transitionRoutine);
        _transitionRoutine = StartCoroutine(Transition(fromPos, toPos, fromRot, toRot, onComplete));
    }

    private IEnumerator Transition(
        Vector3 aPos, Vector3 bPos,
        Quaternion aRot, Quaternion bRot,
        System.Action onDone)
    {
        if (mainCamera == null)
        {
            onDone?.Invoke();
            yield break;
        }

        float elapsed = 0f;
        float dur = Mathf.Max(0.01f, cameraTransitionDuration);

        while (elapsed < dur)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / dur);
            mainCamera.transform.position = Vector3.Lerp(aPos, bPos, t);
            mainCamera.transform.rotation = Quaternion.Slerp(aRot, bRot, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        mainCamera.transform.SetPositionAndRotation(bPos, bRot);
        onDone?.Invoke();
    }

    private void ForceReleaseFocusIfStuck()
    {
        bool needRestore = false;

        if (controllersToFreeze != null)
            foreach (var c in controllersToFreeze)
                if (c && !c.enabled) { needRestore = true; break; }

        if (_cineBrain != null && !_cineBrain.enabled) needRestore = true;
        if (_isFocused) needRestore = true;

        if (needRestore) HardRestoreFocusNow();
    }

    private void HookButton(Button btn, UnityAction cb)
    {
        if (!btn) return;
        btn.onClick = new Button.ButtonClickedEvent();
        btn.onClick.AddListener(cb);
    }
}
