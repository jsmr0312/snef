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

    [Header("TriggerZone (manual)")]
    public Transform triggerZoneTransform;
    public float triggerRadius = 3f;
    public Transform playerTransform;

    [Header("Datos de diálogo")]
    public DialogueData dialogueData;

    [Header("UI Elements (Screen Space Canvas)")]
    public Canvas dialogCanvas;
    public RectTransform spriteRect;
    public RectTransform panelRect;
    public TextMeshProUGUI dialogText;
    public GameObject nextIcon;        // tu icono "Siguiente"
    public GameObject understoodIcon;  // tu icono "Entendido"

    [Header("Animaciones")]
    public float panelDropDuration = 0.5f;
    public float spriteSlideDuration = 0.5f;

    [Header("Typewriter Effect")]
    public float typeSpeed = 0.03f;

    [Header("Block Movement")]
    [Tooltip("Scripts de movimiento a desactivar durante el diálogo")]
    public MonoBehaviour[] movementScripts;

    // estado
    int _idx = 0;
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

        // 2) Cachear posiciones
        _spriteTarget = spriteRect.anchoredPosition;
        _panelTarget = panelRect.anchoredPosition;
        _spriteStart = _spriteTarget + Vector2.left * spriteRect.rect.width;
        _panelStart = _panelTarget + Vector2.down * panelRect.rect.height;
        spriteRect.anchoredPosition = _spriteStart;
        panelRect.anchoredPosition = _panelStart;
    }

    void Update()
    {
        // Si estamos en TriggerZone y no hemos corrido todavía, comprobamos distancia
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

    // Interactor feedback (solo para Interaction)
    public void OnGazeEnter() { /* opcional: mostrar prompt */ }
    public void OnGazeExit() { /* opcional: ocultar prompt */ }

    // Interact (tecla E sobre el NPC)
    public void Interact()
    {
        if (mode != ActivationMode.Interaction || _running) return;
        StartCoroutine(RunDialogue());
    }

    IEnumerator RunDialogue()
    {
        _running = true;
        _idx = 0;

        // 1) Bloquear movimiento
        foreach (var m in movementScripts) m.enabled = false;

        // 2) Mostrar canvas y animar sprite+panel
        dialogCanvas.gameObject.SetActive(true);
        float t = 0f, dur = Mathf.Max(panelDropDuration, spriteSlideDuration);
        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            spriteRect.anchoredPosition = Vector2.Lerp(_spriteStart, _spriteTarget, t);
            panelRect.anchoredPosition = Vector2.Lerp(_panelStart, _panelTarget, t);
            yield return null;
        }

        // 3) Typewriter + espera de tecla E
        while (_idx < dialogueData.lines.Length)
        {
            // efecto typewriter
            dialogText.text = "";
            foreach (char c in dialogueData.lines[_idx])
            {
                dialogText.text += c;
                yield return new WaitForSeconds(typeSpeed);
            }

            _idx++;

            // Si quedan líneas: mostramos icono NEXT y esperamos E
            if (_idx < dialogueData.lines.Length)
            {
                nextIcon.SetActive(true);
                yield return new WaitUntil(() => Input.GetKeyDown(KeyCode.E));
                nextIcon.SetActive(false);
            }
        }

        // 4) Última pantalla: icono ENTENDIDO + espera E
        understoodIcon.SetActive(true);
        yield return new WaitUntil(() => Input.GetKeyDown(KeyCode.E));
        understoodIcon.SetActive(false);

        // 5) Cerrar diálogo, reacivar movimiento
        dialogCanvas.gameObject.SetActive(false);
        foreach (var m in movementScripts) m.enabled = true;
        _running = false;
    }
}
