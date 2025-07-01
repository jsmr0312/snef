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

    [Header("UI de diálogo")]
    public Canvas dialogueBubble;
    public TextMeshProUGUI dialogueText;

    [Header("Typewriter Effect")]
    [Tooltip("Segundos entre cada carácter")]
    public float typeSpeed = 0.03f;

    [Header("Prompt UI")]
    public Canvas promptCanvas;
    public bool lookAtCamera = true;

    [Header("Pantallas a resaltar")]
    public UnifiedScreenDisplay[] screensToHighlight;

    [Header("Button Icons")]
    [Tooltip("Sprite ’Siguiente’ (presiona E)")]
    public GameObject nextIcon;
    [Tooltip("Sprite ’Abrir Quiz’")]
    public GameObject openQuizIcon;
    [Tooltip("Sprite ’Entendido’")]
    public GameObject understoodIcon;

    // Estado interno
    private Phase _phase = Phase.Initial;
    private int _initialIndex;
    private int _waitingIndex;
    private int _postIndex;
    private int _finalIndex;

    // Corrutina de tipeo
    private Coroutine _typingRoutine;

    public void Interact()
    {
        // 1) Ocultar promptUI si está asignado
        if (promptCanvas != null)
            promptCanvas.gameObject.SetActive(false);

        // 2) Registrar misión VisitStand en la primera línea de la fase Initial
        if (_phase == Phase.Initial && _initialIndex == 0 && MissionManager.I != null)
        {
            MissionManager.I.NotifyEvent(MissionManager.MissionType.VisitStand);
        }

        // 3) Mostrar diálogo
        if (dialogueBubble != null)
            dialogueBubble.gameObject.SetActive(true);

        // 4) Lógica por fases
        switch (_phase)
        {
            case Phase.Initial:
                if (_initialIndex < initialDialogue.lines.Length)
                    ShowLine(initialDialogue.lines[_initialIndex++]);

                if (_initialIndex >= initialDialogue.lines.Length)
                {
                    _phase = Phase.Waiting;
                    TriggerScreensHighlight();
                }
                break;

            case Phase.Waiting:
                if (!AllScreensViewed())
                {
                    int idx = Mathf.Min(_waitingIndex++, waitingDialogue.lines.Length - 1);
                    ShowLine(waitingDialogue.lines[idx]);
                }
                else
                {
                    _phase = Phase.PostScreens;
                    _postIndex = 0;
                }
                break;

            case Phase.PostScreens:
                if (_postIndex < postScreensDialogue.lines.Length)
                    ShowLine(postScreensDialogue.lines[_postIndex++]);
                else
                    StartQuiz();
                break;

            case Phase.Final:
                if (_finalIndex < finalDialogue.lines.Length)
                    ShowLine(finalDialogue.lines[_finalIndex++]);
                break;
        }
    }

    private void TriggerScreensHighlight()
    {
        foreach (var screen in screensToHighlight)
            screen.EnableHighlight();
    }

    private bool AllScreensViewed()
    {
        foreach (var screen in screensToHighlight)
            if (!screen.Viewed) return false;
        return true;
    }

    private void StartQuiz()
    {
        FindObjectOfType<QuizManager>().StartQuiz();
    }

    /// <summary>
    /// Llamar desde QuizManager.EndQuiz() justo después de UnlockArcade()
    /// </summary>
    public void OnQuizFinished()
    {
        _phase = Phase.Final;
        _finalIndex = 0;
        ShowLine(finalDialogue.lines.Length > 0 ? finalDialogue.lines[0] : "");
        _finalIndex = 1;
    }

    public void OnGazeEnter()
    {
        if (promptCanvas != null)
            promptCanvas.gameObject.SetActive(true);
    }

    public void OnGazeExit()
    {
        if (promptCanvas != null)
            promptCanvas.gameObject.SetActive(false);
        if (dialogueBubble != null)
            dialogueBubble.gameObject.SetActive(false);

        ResetCurrentPhase();
        HideAllIcons();
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

    private void LateUpdate()
    {
        if (!lookAtCamera) return;
        var cam = Camera.main.transform;
        if (promptCanvas != null && promptCanvas.gameObject.activeSelf)
        {
            promptCanvas.transform.LookAt(cam);
            promptCanvas.transform.Rotate(0, 180, 0);
        }
        if (dialogueBubble != null && dialogueBubble.gameObject.activeSelf)
        {
            dialogueBubble.transform.LookAt(cam);
            dialogueBubble.transform.Rotate(0, 180, 0);
        }
    }

    // --------------------
    // Typewriter + Icons
    // --------------------

    private void ShowLine(string line)
    {
        HideAllIcons();
        if (_typingRoutine != null)
            StopCoroutine(_typingRoutine);
        _typingRoutine = StartCoroutine(TypeText(line));
    }

    private IEnumerator TypeText(string fullText)
    {
        dialogueText.text = "";
        for (int i = 1; i <= fullText.Length; i++)
        {
            dialogueText.text = fullText.Substring(0, i);
            yield return new WaitForSeconds(typeSpeed);
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
                if (_postIndex - 1 >= postScreensDialogue.lines.Length - 1)
                    openQuizIcon?.SetActive(true);
                else
                    nextIcon?.SetActive(true);
                break;

            case Phase.Final:
                understoodIcon?.SetActive(true);
                break;

            default: // Initial & Waiting
                if (_phase == Phase.Initial && _initialIndex < initialDialogue.lines.Length)
                    nextIcon?.SetActive(true);
                else
                    understoodIcon?.SetActive(true);
                break;
        }
    }
}
