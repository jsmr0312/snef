using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DusefDialogueController : MonoBehaviour,
    Interactor.IInteractable,
    Interactor.IInteractableFeedback
{
    public enum ActivationMode { Interaction, TriggerZone }

    [Header("Modo de activación")]
    public ActivationMode mode = ActivationMode.Interaction;

    [Header("TriggerZone (solo si usas TriggerZone)")]
    public Transform triggerZoneTransform;
    public float triggerRadius = 3f;
    public Transform playerTransform;

    [Header("Datos de diálogo")]
    public DialogueData dialogueData;

    [Header("UI Elements (Screen Space Canvas)")]
    public Canvas dialogCanvas;   // Canvas con el panel y sprite
    public RectTransform spriteRect;     // RectTransform del sprite (Dusef)
    public RectTransform panelRect;      // RectTransform del panel de texto
    public TextMeshProUGUI dialogText;     // Componente TMP para el texto
    public GameObject nextIcon;       // Icono “Siguiente”
    public GameObject understoodIcon; // Icono “Entendido”

    [Header("Animaciones")]
    public float panelDropDuration = 0.5f;
    public float spriteSlideDuration = 0.5f;

    [Header("Typewriter Effect")]
    public float typeSpeed = 0.03f;

    [Header("Bloquear durante diálogo")]
    [Tooltip("Scripts de movimiento + Animator + PlayerInput, etc.")]
    public Behaviour[] behavioursToBlock;

    // Estado interno
    int _index = 0;
    bool _running = false;
    bool _triggered = false;
    Vector2 _spriteStart, _spriteTarget;
    Vector2 _panelStart, _panelTarget;

    void Awake()
    {
        // 1) Ocultar UI e iconos
        dialogCanvas.gameObject.SetActive(false);
        nextIcon.SetActive(false);
        understoodIcon.SetActive(false);

        // 2) Cachear posiciones destino
        _spriteTarget = spriteRect.anchoredPosition;
        _panelTarget = panelRect.anchoredPosition;

        // 3) Calcular posiciones iniciales “off-screen”
        _spriteStart = _spriteTarget + Vector2.left * spriteRect.rect.width;
        _panelStart = _panelTarget + Vector2.down * panelRect.rect.height;

        spriteRect.anchoredPosition = _spriteStart;
        panelRect.anchoredPosition = _panelStart;
    }

    void Update()
    {
        // TriggerZone manual
        if (mode == ActivationMode.TriggerZone && !_running && !_triggered)
        {
            if (triggerZoneTransform != null && playerTransform != null)
            {
                float d2 = (playerTransform.position - triggerZoneTransform.position).sqrMagnitude;
                if (d2 <= triggerRadius * triggerRadius)
                {
                    _triggered = true;
                    StartCoroutine(RunDialogue());
                }
            }
        }
    }

    // IInteractableFeedback (para Interaction mode)
    public void OnGazeEnter() { /* opcional: mostrar prompt */ }
    public void OnGazeExit() { /* opcional: ocultar prompt */ }

    // IInteractable (tecla E sobre Dusef)
    public void Interact()
    {
        if (mode != ActivationMode.Interaction || _running) return;
        StartCoroutine(RunDialogue());
    }

    IEnumerator RunDialogue()
    {
        _running = true;
        _index = 0;

        // 1) Bloquear todos los behaviours (incluye Animator, input, controlador, etc.)
        foreach (var b in behavioursToBlock)
            b.enabled = false;

        // 2) Mostrar canvas y animar sprite + panel
        dialogCanvas.gameObject.SetActive(true);
        float t = 0f, dur = Mathf.Max(panelDropDuration, spriteSlideDuration);
        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            spriteRect.anchoredPosition = Vector2.Lerp(_spriteStart, _spriteTarget, t);
            panelRect.anchoredPosition = Vector2.Lerp(_panelStart, _panelTarget, t);
            yield return null;
        }

        // 3) Typewriter + avanzar con E
        while (_index < dialogueData.lines.Length)
        {
            // escribe cada carácter
            dialogText.text = "";
            foreach (char c in dialogueData.lines[_index])
            {
                dialogText.text += c;
                yield return new WaitForSeconds(typeSpeed);
            }
            _index++;

            // si quedan más líneas: mostrar NEXT y esperar E
            if (_index < dialogueData.lines.Length)
            {
                nextIcon.SetActive(true);
                yield return new WaitUntil(() => Input.GetKeyDown(KeyCode.E));
                nextIcon.SetActive(false);
            }
        }

        // 4) Al finalizar todas las líneas: mostrar ENTENDIDO y esperar E
        understoodIcon.SetActive(true);
        yield return new WaitUntil(() => Input.GetKeyDown(KeyCode.E));
        understoodIcon.SetActive(false);

        // 5) Cerrar diálogo y reactivar behaviours
        dialogCanvas.gameObject.SetActive(false);
        foreach (var b in behavioursToBlock)
            b.enabled = true;

        _running = false;
    }
}
