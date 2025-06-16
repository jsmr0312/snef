using UnityEngine;
using TMPro;

public class NPCDialogueFlow : MonoBehaviour,
    Interactor.IInteractable,
    Interactor.IInteractableFeedback
{
    public enum Phase { Initial, Waiting, PostScreens, Final }

    [Header("Diálogos")]
    [Tooltip("Líneas iniciales hasta resaltar pantallas")]
    public DialogueData initialDialogue;
    [Tooltip("Líneas mientras esperas a que vean las pantallas")]
    public DialogueData waitingDialogue;
    [Tooltip("Líneas tras ver todas las pantallas (invita al quiz)")]
    public DialogueData postScreensDialogue;
    [Tooltip("Líneas finales tras completar el quiz (invita a la arcade)")]
    public DialogueData finalDialogue;

    [Header("UI de diálogo")]
    public Canvas dialogueBubble;
    public TextMeshProUGUI dialogueText;

    [Header("Prompt UI")]
    public Canvas promptCanvas;
    public bool lookAtCamera = true;

    [Header("Pantallas a resaltar")]
    [Tooltip("Asignar aquí sólo las pantallas de este stand")]
    public UnifiedScreenDisplay[] screensToHighlight;

    // Estado interno
    private Phase _phase = Phase.Initial;
    private int _initialIndex;
    private int _waitingIndex;
    private int _postIndex;
    private int _finalIndex;

    public void Interact()
    {
        dialogueBubble.gameObject.SetActive(true);

        switch (_phase)
        {
            case Phase.Initial:
                if (_initialIndex < initialDialogue.lines.Length)
                    dialogueText.text = initialDialogue.lines[_initialIndex++];
                if (_initialIndex >= initialDialogue.lines.Length)
                {
                    _phase = Phase.Waiting;
                    TriggerScreensHighlight();
                }
                break;

            case Phase.Waiting:
                if (!AllScreensViewed())
                {
                    // recorre waitingDialogue
                    if (_waitingIndex < waitingDialogue.lines.Length)
                        dialogueText.text = waitingDialogue.lines[_waitingIndex++];
                    else
                        dialogueText.text = waitingDialogue.lines[waitingDialogue.lines.Length - 1];
                }
                else
                {
                    _phase = Phase.PostScreens;
                    _postIndex = 0;
                }
                break;

            case Phase.PostScreens:
                if (_postIndex < postScreensDialogue.lines.Length)
                {
                    dialogueText.text = postScreensDialogue.lines[_postIndex++];
                }
                else
                {
                    // lanza quiz y pasamos a "en quiz"
                    StartQuiz();
                    // no cambiamos _phase aquí: aguardamos OnQuizFinished()
                }
                break;

            case Phase.Final:
                if (_finalIndex < finalDialogue.lines.Length)
                    dialogueText.text = finalDialogue.lines[_finalIndex++];
                // si quieres que se quede en la última línea, no hagas más
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
    /// Debe llamarse desde QuizManager.EndQuiz(), justo después de desbloquear la arcade.
    /// </summary>
    public void OnQuizFinished()
    {
        _phase = Phase.Final;
        _finalIndex = 0;
        // Mostrar inmediatamente la primera línea de finalDialogue:
        dialogueBubble.gameObject.SetActive(true);
        dialogueText.text = finalDialogue.lines.Length > 0 ? finalDialogue.lines[0] : "";
        _finalIndex = 1;
    }

    public void OnGazeEnter()
    {
        promptCanvas?.gameObject.SetActive(true);
    }

    public void OnGazeExit()
    {
        promptCanvas?.gameObject.SetActive(false);
        dialogueBubble?.gameObject.SetActive(false);
        ResetCurrentPhase();
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
        var cam = Camera.main.transform;
        if (lookAtCamera)
        {
            if (promptCanvas.gameObject.activeSelf)
            {
                promptCanvas.transform.LookAt(cam);
                promptCanvas.transform.Rotate(0, 180, 0);
            }
            if (dialogueBubble.gameObject.activeSelf)
            {
                dialogueBubble.transform.LookAt(cam);
                dialogueBubble.transform.Rotate(0, 180, 0);
            }
        }
    }
}
