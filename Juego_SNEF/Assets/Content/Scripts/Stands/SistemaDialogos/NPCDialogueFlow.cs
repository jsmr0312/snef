using System.Collections;
using UnityEngine;
using TMPro;

public class NPCDialogueFlow : MonoBehaviour,
    Interactor.IInteractable,
    Interactor.IInteractableFeedback
{
    public enum Phase { Initial, Waiting, PostScreens, Final }

    [Header("Diálogos")]
    public DialogueData initialDialogue;       // hasta resaltar pantallas
    public DialogueData waitingDialogue;       // mientras ven pantallas
    public DialogueData postScreensDialogue;   // tras ver pantallas (invita quiz)
    public DialogueData finalDialogue;         // tras quiz (invita arcade)

    [Header("UI de diálogo (burbuja sobre el NPC)")]
    [Tooltip("GameObject de la burbuja (Canvas/Contenedor)")]
    public GameObject dialogueBubble;
    public TextMeshProUGUI dialogueText;

    [Header("Typewriter")]
    [Tooltip("Segundos entre caracteres (0 = instantáneo)")]
    public float typeSpeed = 0.03f;

    [Header("Prompt UI (estilo UnifiedScreenDisplay)")]
    [Tooltip("GameObject de ‘Presiona E’")]
    public GameObject promptUI;
    public bool lookAtCamera = true;

    [Header("Outline (QuickOutline)")]
    public Outline outline;                                  // Asignar si ya existe; si no, se auto-busca
    public bool enableOutlineOnProximity = true;             // Encender al enfocar (OnGazeEnter)
    public Outline.Mode outlineModeNear = Outline.Mode.OutlineVisible;
    public Color outlineColorNear = Color.cyan;
    [Range(0, 10f)] public float outlineWidthNear = 4f;
    [Tooltip("Apagar outline mientras estás conversando")]
    public bool disableOutlineWhileTalking = true;

    [Header("Pantallas a resaltar (opcional)")]
    public UnifiedScreenDisplay[] screensToHighlight;        // Para EnableHighlight() y verificación de Viewed

    [Header("Iconos de avance (opcional)")]
    public GameObject nextIcon;        // “Siguiente (E)”
    public GameObject openQuizIcon;    // “Abrir Quiz”
    public GameObject understoodIcon;  // “Entendido”

    [Header("Auto-cierre")]
    [Tooltip("Cerrar burbuja si te alejas demasiado")]
    public bool closeWhenFar = true;
    [Tooltip("Distancia a partir de la cual se cierra la conversación")]
    public float closeDistance = 5f;
    [Tooltip("Cerrar si se pierde el foco visual por más de este tiempo")]
    public bool closeWhenGazeLost = true;
    public float gazeLostGrace = 0.4f;

    [Header("Jugadores (para distancia)")]
    [Tooltip("Lista de personajes/jugadores a considerar para cierre por distancia")]
    public Transform[] playerTransforms;

    // ---------- Estado interno ----------
    private Phase _phase = Phase.Initial;
    private int _initialIndex;
    private int _waitingIndex;
    private int _postIndex;
    private int _finalIndex;

    private Coroutine _typingRoutine;
    private string _typingFullText = "";

    // Conversación viva (evita cerrar por parpadeo de target)
    private bool _inConversation = false;

    // Timers de foco
    private bool _lostGaze = false;
    private float _gazeLostAt = 0f;

    void Awake()
    {
        if (promptUI) promptUI.SetActive(false);
        if (dialogueBubble) dialogueBubble.SetActive(false);
        HideAllIcons();

        // Outline (auto-descubrir si no está asignado)
        if (outline == null) outline = GetComponent<Outline>() ?? GetComponentInChildren<Outline>();
        if (outline != null) outline.enabled = false;
    }

    void Update()
    {
        // ——— Auto-cierre por distancia (múltiples jugadores) ———
        if (_inConversation && closeWhenFar)
        {
            float d = GetNearestPlayerDistance();
            if (d > closeDistance)
            {
                EndConversation();
            }
        }

        // ——— Auto-cierre por pérdida de foco (con “grace”) ———
        if (_inConversation && closeWhenGazeLost && _lostGaze)
        {
            if (Time.time - _gazeLostAt >= gazeLostGrace)
                EndConversation();
        }
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

    // =========================================================
    //     Interactor integration (igual que UnifiedScreenDisplay)
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
        if (promptUI) promptUI.SetActive(false);

        // Si NO estás conversando, apaga todo y resetea índices
        if (!_inConversation)
        {
            if (dialogueBubble) dialogueBubble.SetActive(false);
            if (outline) outline.enabled = false;
            ResetCurrentPhase();
            HideAllIcons();
            _lostGaze = false;
            return;
        }

        // Si SÍ estás conversando, inicia el “grace” por pérdida de foco
        _lostGaze = true;
        _gazeLostAt = Time.time;
    }

    public void Interact()
    {
        // Si estás tipeando y presionas Interact, salta al final de la línea
        if (_typingRoutine != null && dialogueText != null)
        {
            StopCoroutine(_typingRoutine);
            _typingRoutine = null;
            dialogueText.text = _typingFullText;
            ShowRelevantIcon();
            return;
        }

        // Ocultar prompt y activar conversación
        if (promptUI) promptUI.SetActive(false);
        if (dialogueBubble) dialogueBubble.SetActive(true);
        _inConversation = true;
        _lostGaze = false;

        // Outline opcional OFF durante conversación
        if (disableOutlineWhileTalking && outline) outline.enabled = false;

        // Flujo por fases
        switch (_phase)
        {
            case Phase.Initial:
                if (initialDialogue != null && initialDialogue.lines != null && _initialIndex < initialDialogue.lines.Length)
                    ShowLine(initialDialogue.lines[_initialIndex++]);

                if (initialDialogue != null && initialDialogue.lines != null && _initialIndex >= initialDialogue.lines.Length)
                {
                    TriggerScreensHighlight();
                    _phase = Phase.Waiting;
                    _waitingIndex = 0;
                }
                break;

            case Phase.Waiting:
                if (!AllScreensViewed())
                {
                    if (waitingDialogue != null && waitingDialogue.lines != null && waitingDialogue.lines.Length > 0)
                    {
                        int idx = Mathf.Min(_waitingIndex++, waitingDialogue.lines.Length - 1);
                        ShowLine(waitingDialogue.lines[idx]);
                    }
                }
                else
                {
                    if (MissionManager.I != null)
                        MissionManager.I.NotifyEvent(MissionManager.MissionType.VisitStand);

                    _phase = Phase.PostScreens;
                    _postIndex = 0;

                    if (postScreensDialogue != null && postScreensDialogue.lines != null && postScreensDialogue.lines.Length > 0)
                        ShowLine(postScreensDialogue.lines[_postIndex++]);
                    else
                        StartQuiz();
                }
                break;

            case Phase.PostScreens:
                if (postScreensDialogue != null && postScreensDialogue.lines != null && _postIndex < postScreensDialogue.lines.Length)
                {
                    ShowLine(postScreensDialogue.lines[_postIndex++]);
                }
                else
                {
                    StartQuiz();
                }
                break;

            case Phase.Final:
                if (finalDialogue != null && finalDialogue.lines != null && _finalIndex < finalDialogue.lines.Length)
                {
                    ShowLine(finalDialogue.lines[_finalIndex++]);
                }
                else
                {
                    // Fin del flujo. Si quieres ocultar todo aquí, descomenta:
                    // EndConversation();
                }
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
        if (screensToHighlight == null || screensToHighlight.Length == 0) return false;
        foreach (var s in screensToHighlight) if (s && !s.Viewed) return false;
        return true;
    }

    private void StartQuiz()
    {
        var qm = FindObjectOfType<QuizManager>();
        if (qm != null) qm.StartQuiz();
    }

    /// Llamar desde QuizManager.EndQuiz() cuando termine el quiz
    public void OnQuizFinished()
    {
        _phase = Phase.Final;
        _finalIndex = 0;

        if (finalDialogue != null && finalDialogue.lines != null && finalDialogue.lines.Length > 0)
        {
            ShowLine(finalDialogue.lines[0]);
            _finalIndex = 1;
        }
    }

    public void EndConversation()
    {
        _inConversation = false;
        _lostGaze = false;

        if (dialogueBubble) dialogueBubble.SetActive(false);
        HideAllIcons();

        // Si sigues mirando al NPC, vuelve a prender el outline/prompt
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
    //                Typewriter + iconos de hint
    // =========================================================

    private void ShowLine(string line)
    {
        HideAllIcons();

        if (_typingRoutine != null)
            StopCoroutine(_typingRoutine);

        _typingRoutine = StartCoroutine(TypeText(line ?? ""));
    }

    private IEnumerator TypeText(string fullText)
    {
        _typingFullText = fullText ?? "";
        if (dialogueText == null)
        {
            _typingRoutine = null;
            yield break;
        }

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
    }

    private void ShowRelevantIcon()
    {
        switch (_phase)
        {
            case Phase.PostScreens:
                if (postScreensDialogue != null && postScreensDialogue.lines != null &&
                    _postIndex - 1 >= postScreensDialogue.lines.Length - 1)
                    openQuizIcon?.SetActive(true);
                else
                    nextIcon?.SetActive(true);
                break;

            case Phase.Final:
                if (finalDialogue != null && finalDialogue.lines != null && _finalIndex < finalDialogue.lines.Length)
                    nextIcon?.SetActive(true);
                else
                    understoodIcon?.SetActive(true);
                break;

            default: // Initial & Waiting
                if (_phase == Phase.Initial &&
                    initialDialogue != null && initialDialogue.lines != null &&
                    _initialIndex < initialDialogue.lines.Length)
                    nextIcon?.SetActive(true);
                else
                    understoodIcon?.SetActive(true);
                break;
        }
    }

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
        else if (Camera.main) // Fallback si no asignas jugadores
        {
            min = Vector3.Distance(Camera.main.transform.position, transform.position);
        }

        return min;
    }
}
