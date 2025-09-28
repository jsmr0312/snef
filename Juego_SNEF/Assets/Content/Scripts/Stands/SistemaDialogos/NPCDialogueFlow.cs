using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events; // para UnityAction y reasignar UnityEvents

public class NPCDialogueFlow : MonoBehaviour,
    Interactor.IInteractable,
    Interactor.IInteractableFeedback
{
    public enum Phase { Initial, Waiting, PostScreens, Final }

    [Header("Stand (Progress)")]
    public string standId;
    public string standType = "master";
    public int requiredScreens = 4;

    [Header("Refs")]
    public QuizManager quizManagerRef;

    [Header("Diálogos (Initial inline)")]
    [TextArea(2, 4)] public List<string> initialLines = new List<string>();

    [Header("Diálogos (Assets)")]
    public DialogueData waitingDialogue;
    public DialogueData postScreensDialogue;
    public DialogueData finalDialogue;

    [Header("UI de diálogo (burbuja sobre el NPC)")]
    public GameObject dialogueBubble;
    public TextMeshProUGUI dialogueText;

    [Header("Typewriter")]
    public float typeSpeed = 0.03f;

    [Header("Prompt UI")]
    public GameObject promptUI;
    public bool lookAtCamera = true;

    [Header("Outline (QuickOutline)")]
    public Outline outline;
    public bool enableOutlineOnProximity = true;
    public Outline.Mode outlineModeNear = Outline.Mode.OutlineVisible;
    public Color outlineColorNear = Color.cyan;
    [Range(0, 10f)] public float outlineWidthNear = 4f;
    public bool disableOutlineWhileTalking = true;

    [Header("Pantallas a resaltar (opcional)")]
    public UnifiedScreenDisplay[] screensToHighlight;

    [Header("Iconos de avance (visuales)")]
    public GameObject nextIcon;
    public GameObject openQuizIcon;
    public GameObject understoodIcon;

    [Header("Botones (click) para los iconos")]
    public Button nextButton;
    public Button openQuizButton;
    public Button understoodButton;

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
    public bool autoCreateViewpointIfNull = true;
    public Vector3 autoViewLocalOffset = new Vector3(0.8f, 1.65f, 1.6f);
    public float cameraTransitionDuration = 0.6f;

    [Tooltip("Componentes a desactivar durante el enfoque.")]
    public MonoBehaviour[] controllersToFreeze;

    [Header("Jugador / UI")]
    public GameObject playerUI;
    public GameObject playerRoot;
    public bool disablePlayerRootDuringFocus = false;
    private bool _playerRootPrevActive = false;

    [Tooltip("Botón Cerrar (opcional) que cierra y devuelve la cámara")]
    public Button closeButton;

    public bool allowEscToClose = true;

    [Header("Parches de cámara")]
    public bool detachCameraFromParentDuringFocus = true;
    public bool disableCinemachineDuringFocus = true;
    public Transform reparentFallback;

    [Header("Auto Freezer")]
    public bool autoFreezeFirstParentController = true;
    private MonoBehaviour _autoFrozenController;

    // Propietario global del foco (evita cruces entre NPCs)
    private static NPCDialogueFlow _focusOwner;

    // Estado enfoque / cámara
    private bool _isFocused = false;
    private Vector3 _camOrigPos;
    private Quaternion _camOrigRot;
    private Coroutine _transitionRoutine;
    private Transform _camOrigParent;
    private Behaviour _cineBrain; // CinemachineBrain
    private bool _cineBrainWasEnabled = false;

    // Estado diálogo
    private Phase _phase = Phase.Initial;
    private int _initialIndex;
    private int _waitingIndex;
    private int _postIndex;
    private int _finalIndex;

    private Coroutine _typingRoutine;
    private string _typingFullText = "";
    private bool _inConversation = false;

    // Timers de foco visual
    private bool _lostGaze = false;
    private float _gazeLostAt = 0f;

    void Awake()
    {
        if (promptUI) promptUI.SetActive(false);
        if (dialogueBubble) dialogueBubble.SetActive(false);
        HideAllIcons();

        if (outline == null) outline = GetComponent<Outline>() ?? GetComponentInChildren<Outline>();
        if (outline != null) outline.enabled = false;

        if (!mainCamera) mainCamera = Camera.main;

        if (focusCameraOnTalk && focusViewpoint == null && autoCreateViewpointIfNull)
        {
            var vp = new GameObject("NPC_FocusViewpoint").transform;
            vp.SetParent(transform, false);
            vp.position = transform.position
                        + transform.right * autoViewLocalOffset.x
                        + Vector3.up * autoViewLocalOffset.y
                        + transform.forward * autoViewLocalOffset.z;
            vp.LookAt(transform.position, Vector3.up);
            focusViewpoint = vp;
        }

        // ---- Auto-resolver botones si faltan referencias ----
        if (!nextButton && nextIcon) nextButton = nextIcon.GetComponentInChildren<Button>(true);
        if (!openQuizButton && openQuizIcon) openQuizButton = openQuizIcon.GetComponentInChildren<Button>(true);
        if (!understoodButton && understoodIcon) understoodButton = understoodIcon.GetComponentInChildren<Button>(true);

        // Si no asignaste closeButton explícitamente, intenta encontrarlo bajo el diálogo
        if (!closeButton && dialogueBubble)
        {
            Button best = null;
            foreach (var b in dialogueBubble.GetComponentsInChildren<Button>(true))
            {
                if (b.name.ToLower().Contains("close") || b.name.ToLower().Contains("cerrar"))
                {
                    best = b; break;
                }
                if (!best) best = b;
            }
            closeButton = best;
        }

        // ---- Rewire DURO (borra listeners persistentes y de runtime) ----
        HookButton(nextButton, OnNextClicked);
        HookButton(openQuizButton, OnOpenQuizClicked);
        HookButton(understoodButton, OnUnderstoodClicked);
        HookButton(closeButton, EndConversation);

        if (closeButton) closeButton.gameObject.SetActive(false);
    }

    void Start()
    {
        // Si usas progreso local, hidrata fase (no retrocede)
        SyncPhaseWithProgressForwardOnly();
    }

    void Update()
    {
        if (_inConversation && closeWhenFar && !_isFocused)
        {
            float d = GetNearestPlayerDistance();
            if (d > closeDistance) EndConversation();
        }

        if (_inConversation && closeWhenGazeLost && _lostGaze && !_isFocused)
        {
            if (Time.time - _gazeLostAt >= gazeLostGrace)
                EndConversation();
        }

        // Eliminado: interacción con tecla E (solo botones)
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

    // ---------- progreso → fase ----------
    private bool TryMapPhase(string p, out Phase phase)
    {
        switch (p)
        {
            case "Waiting": phase = Phase.Waiting; return true;
            case "PostScreens": phase = Phase.PostScreens; return true;
            case "Final": phase = Phase.Final; return true;
            case "Initial": phase = Phase.Initial; return true;
            default: phase = _phase; return false;
        }
    }

    private void SyncPhaseWithProgressForwardOnly()
    {
        var p = ProgressCore.I?.Stand_GetPhase(standId);
        if (string.IsNullOrEmpty(p)) return;

        if (TryMapPhase(p, out var phFromProgress))
        {
            int Rank(Phase ph) => ph == Phase.Initial ? 0 :
                                  ph == Phase.Waiting ? 1 :
                                  ph == Phase.PostScreens ? 2 : 3;

            if (Rank(phFromProgress) > Rank(_phase))
                _phase = phFromProgress;
        }
    }

    // =========================================================
    //     Interactor integration
    // =========================================================
    public void OnGazeEnter()
    {
        _lostGaze = false;

        if (!_inConversation)
        {
            if (enableOutlineOnProximity && outline)
            {
                ApplyOutlineSettings();
                outline.enabled = true;
            }
            if (promptUI) promptUI.SetActive(true);
        }
    }

    public void OnGazeExit()
    {
        if (_isFocused) return;

        if (promptUI) promptUI.SetActive(false);

        if (!_inConversation)
        {
            if (dialogueBubble) dialogueBubble.SetActive(false);
            if (outline) outline.enabled = false;
            ResetCurrentPhase();
            HideAllIcons();
            _lostGaze = false;
            return;
        }

        _lostGaze = true;
        _gazeLostAt = Time.time;
    }

    public void Interact()
    {
        // Llamado solo desde botones (no por teclado)
        SyncPhaseWithProgressForwardOnly();

        if (_typingRoutine != null && dialogueText != null)
        {
            StopCoroutine(_typingRoutine);
            _typingRoutine = null;
            dialogueText.text = _typingFullText;
            ShowRelevantIcon();
            return;
        }

        if (promptUI) promptUI.SetActive(false);
        if (dialogueBubble) dialogueBubble.SetActive(true);
        _inConversation = true;
        _lostGaze = false;

        if (disableOutlineWhileTalking && outline) outline.enabled = false;

        if (focusCameraOnTalk && !_isFocused) EnterFocusMode();

        switch (_phase)
        {
            case Phase.Initial:
                if (HasMore(Phase.Initial, _initialIndex))
                    ShowLine(GetLine(Phase.Initial, _initialIndex++));

                if (!HasMore(Phase.Initial, _initialIndex))
                {
                    TriggerScreensHighlight();

                    ProgressCore.I?.Stand_SetPhase(standId, "Waiting", standType);
                    _phase = Phase.Waiting;
                    _waitingIndex = 0;

                    int totalW = GetTotal(Phase.Waiting);
                    if (totalW > 0) ShowLine(GetLine(Phase.Waiting, _waitingIndex++));
                    else ShowLine("¡Termina de ver el contenido!");
                }
                break;

            case Phase.Waiting:
                if (!AllScreensViewed())
                {
                    int total = GetTotal(Phase.Waiting);
                    if (total > 0)
                    {
                        int idx = Mathf.Min(_waitingIndex++, total - 1);
                        ShowLine(GetLine(Phase.Waiting, idx));
                    }
                    else ShowLine(" ");
                }
                else
                {
                    MissionManager.I?.NotifyEvent(MissionManager.MissionType.VisitStand);

                    ProgressCore.I?.Stand_SetPhase(standId, "PostScreens", standType);
                    ProgressCore.I?.Stand_UnlockQuiz(standId);

                    try { ProgressRemote.I?.UpdateStand(standId, standType, phase: "PostScreens"); }
                    catch (System.Exception ex) { Debug.LogWarning("[NPCDialogueFlow] UpdateStand falló: " + ex.Message); }

                    _phase = Phase.PostScreens;
                    _postIndex = 0;

                    if (HasMore(Phase.PostScreens, _postIndex))
                        ShowLine(GetLine(Phase.PostScreens, _postIndex++));
                    else
                        StartQuiz();
                }
                break;

            case Phase.PostScreens:
                if (HasMore(Phase.PostScreens, _postIndex)) ShowLine(GetLine(Phase.PostScreens, _postIndex++));
                else StartQuiz();
                break;

            case Phase.Final:
                if (HasMore(Phase.Final, _finalIndex)) ShowLine(GetLine(Phase.Final, _finalIndex++));
                else StartQuiz(); // reabrir si terminas líneas
                break;
        }
    }

    // =========================================================
    //                     Helpers de flujo
    // =========================================================
    private void ApplyOutlineSettings()
    {
        if (!outline) return;
        outline.OutlineMode = outlineModeNear;
        outline.OutlineColor = outlineColorNear;
        outline.OutlineWidth = outlineWidthNear;
    }

    private void TriggerScreensHighlight()
    {
        if (screensToHighlight == null) return;
        foreach (var s in screensToHighlight) if (s) s.EnableHighlight();
    }

    private bool AllScreensViewed()
    {
        if (screensToHighlight != null && screensToHighlight.Length > 0)
        {
            foreach (var s in screensToHighlight) if (s && !s.Viewed) return false;
            return true;
        }

        var list = ProgressCore.I?.Data?.stands;
        if (list != null)
        {
            var sp = list.Find(x => x.stand_id == standId);
            if (sp != null) return sp.viewed_screens != null && sp.viewed_screens.Count >= requiredScreens;
        }

        return false;
    }

    private void StartQuiz()
    {
        if (quizManagerRef != null) { quizManagerRef.StartQuiz(); return; }
        FindObjectOfType<QuizManager>()?.StartQuiz();
    }

    /// Llamar desde QuizManager cuando se cierre o termine el quiz
    public void OnQuizFinished()
    {
        _phase = Phase.Final;
        _finalIndex = 0;

        ProgressCore.I?.Stand_SetPhase(standId, "Final", standType);
        ProgressCore.I?.SaveNow("stand_quiz_finished_" + standId);

        if (HasMore(Phase.Final, 0))
        {
            ShowLine(GetLine(Phase.Final, 0));
            _finalIndex = 1;
        }
    }

    public void EndConversation()
    {
        _inConversation = false;
        _lostGaze = false;

        if (dialogueBubble) dialogueBubble.SetActive(false);
        HideAllIcons();

        // salida segura siempre
        HardRestoreFocusNow();

        if (enableOutlineOnProximity && outline)
        {
            ApplyOutlineSettings();
            outline.enabled = true;
        }
        if (promptUI) promptUI.SetActive(true);
    }

    private void ResetCurrentPhase()
    {
        switch (_phase)
        {
            case Phase.Initial: _initialIndex = 0; break;
            case Phase.Waiting: _waitingIndex = 0; break;
            case Phase.PostScreens: _postIndex = 0; break;
            case Phase.Final: _finalIndex = 0; break;
        }
    }

    // =========================================================
    //                Typewriter + iconos
    // =========================================================
    private void ShowLine(string line)
    {
        HideAllIcons();

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
        ShowRelevantIcon();
    }

    private void HideAllIcons()
    {
        nextIcon?.SetActive(false);
        openQuizIcon?.SetActive(false);
        understoodIcon?.SetActive(false);
        if (closeButton) closeButton.gameObject.SetActive(false);
    }

    private void ShowRelevantIcon()
    {
        switch (_phase)
        {
            case Phase.PostScreens:
                if (!HasMore(Phase.PostScreens, _postIndex))
                    openQuizIcon?.SetActive(true);
                else
                    nextIcon?.SetActive(true);
                break;

            case Phase.Final:
                if (HasMore(Phase.Final, _finalIndex))
                {
                    nextIcon?.SetActive(true);
                }
                else
                {
                    // Mostrar AMBOS: Abrir Quiz y Entendido (como pediste)
                    openQuizIcon?.SetActive(true);
                    understoodIcon?.SetActive(true);
                }
                break;

            case Phase.Waiting:
                understoodIcon?.SetActive(true);
                break;

            default: // Initial
                if (HasMore(Phase.Initial, _initialIndex))
                    nextIcon?.SetActive(true);
                else
                    understoodIcon?.SetActive(true);
                break;
        }

        if (_isFocused && closeButton != null)
            closeButton.gameObject.SetActive(true);
    }

    // Handlers de botones (click)
    private void OnNextClicked() => Interact();
    private void OnOpenQuizClicked()
    {
        if (_phase == Phase.PostScreens || _phase == Phase.Final)
            StartQuiz();
    }
    private void OnUnderstoodClicked() => EndConversation();

    // =========================================================
    //            Distancia al jugador más cercano
    // =========================================================
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
    //         🎥 FOCUS: Enter/Exit + transiciones
    // =========================================================
    private void EnterFocusMode()
    {
        if (!mainCamera) mainCamera = Camera.main;
        if (mainCamera == null || focusViewpoint == null) return;
        if (_isFocused) return;

        // Tomar propiedad global del foco (libera a cualquier otro NPC activo)
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
            if (_camOrigParent == null) _camOrigParent = mainCamera.transform.parent;
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

        if ((controllersToFreeze == null || controllersToFreeze.Length == 0) && autoFreezeFirstParentController)
        {
            var ctrl = mainCamera.GetComponentInParent<MonoBehaviour>();
            if (ctrl && ctrl != this)
            {
                _autoFrozenController = ctrl;
                _autoFrozenController.enabled = false;
            }
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        if (playerUI) playerUI.SetActive(false);

        StartOrRestartTransition(
            _camOrigPos, focusViewpoint.position,
            _camOrigRot, focusViewpoint.rotation,
            onComplete: () =>
            {
                if (closeButton) closeButton.gameObject.SetActive(true);
            }
        );
    }

    private void ExitFocusMode()
    {
        if (mainCamera == null)
        {
            HardRestoreFocusNow();
            return;
        }

        if (_transitionRoutine != null) StopCoroutine(_transitionRoutine);
        if (closeButton) closeButton.gameObject.SetActive(false);

        StartOrRestartTransition(
            mainCamera.transform.position, _camOrigPos,
            mainCamera.transform.rotation, _camOrigRot,
            onComplete: () =>
            {
                if (detachCameraFromParentDuringFocus)
                {
                    Transform targetParent = _camOrigParent != null ? _camOrigParent : reparentFallback;
                    if (targetParent != null)
                        mainCamera.transform.SetParent(targetParent, true);
                }

                RestoreControlsAndUI();
                _isFocused = false;
                if (_focusOwner == this) _focusOwner = null;
            }
        );
    }

    // Restauración inmediata y segura (sin depender de la transición)
    private void HardRestoreFocusNow()
    {
        if (closeButton) closeButton.gameObject.SetActive(false);
        if (_transitionRoutine != null) { StopCoroutine(_transitionRoutine); _transitionRoutine = null; }

        if (mainCamera != null)
        {
            mainCamera.transform.SetPositionAndRotation(_camOrigPos, _camOrigRot);

            if (detachCameraFromParentDuringFocus && mainCamera.transform.parent == null)
            {
                Transform targetParent = _camOrigParent != null ? _camOrigParent : reparentFallback;
                if (targetParent != null) mainCamera.transform.SetParent(targetParent, true);
            }
        }

        RestoreControlsAndUI();
        _isFocused = false;
        if (_focusOwner == this) _focusOwner = null;
    }

    private void RestoreControlsAndUI()
    {
        if (controllersToFreeze != null)
            foreach (var c in controllersToFreeze) if (c) c.enabled = true;

        if (_autoFrozenController)
        {
            _autoFrozenController.enabled = true;
            _autoFrozenController = null;
        }

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

    private int GetTotal(Phase phase)
    {
        switch (phase)
        {
            case Phase.Initial: return initialLines?.Count ?? 0;
            case Phase.Waiting: return waitingDialogue?.lines?.Length ?? 0;
            case Phase.PostScreens: return postScreensDialogue?.lines?.Length ?? 0;
            case Phase.Final: return finalDialogue?.lines?.Length ?? 0;
        }
        return 0;
    }

    private bool HasMore(Phase phase, int index) => index < GetTotal(phase);

    private string GetLine(Phase phase, int index)
    {
        switch (phase)
        {
            case Phase.Initial:
                return (index >= 0 && index < (initialLines?.Count ?? 0)) ? initialLines[index] : null;
            case Phase.Waiting:
                return SafeAssetLine(waitingDialogue, index);
            case Phase.PostScreens:
                return SafeAssetLine(postScreensDialogue, index);
            case Phase.Final:
                return SafeAssetLine(finalDialogue, index);
        }
        return null;
    }

    private string SafeAssetLine(DialogueData data, int index)
    {
        if (data == null || data.lines == null) return null;
        if (index < 0 || index >= data.lines.Length) return null;
        return data.lines[index];
    }

    // Si algo quedó a medias (otro NPC tomó el foco, etc.), restaura TODO.
    private void ForceReleaseFocusIfStuck()
    {
        bool needRestore = false;

        if (controllersToFreeze != null)
            foreach (var c in controllersToFreeze)
                if (c && !c.enabled) { needRestore = true; break; }

        if (_autoFrozenController && !_autoFrozenController.enabled)
            needRestore = true;

        if (_cineBrain != null && !_cineBrain.enabled)
            needRestore = true;

        if (detachCameraFromParentDuringFocus && _camOrigParent && mainCamera && mainCamera.transform.parent == null)
            needRestore = true;

        if (_isFocused) needRestore = true;

        if (needRestore) HardRestoreFocusNow();
    }

    // --- Utilidad: reemplaza completamente los onClick (incluye persistentes del inspector)
    private void HookButton(Button btn, UnityAction cb)
    {
        if (!btn) return;
        btn.onClick = new Button.ButtonClickedEvent();
        btn.onClick.AddListener(cb);
    }
}
