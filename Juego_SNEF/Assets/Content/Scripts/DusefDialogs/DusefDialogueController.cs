using System.Collections;
using UnityEngine;
using TMPro;
using StarterAssets; // Para ThirdPersonController

public class DusefDialogueController : MonoBehaviour,
    Interactor.IInteractable,
    Interactor.IInteractableFeedback
{
    public enum ActivationMode { Interaction, TriggerZone }

    [Header("Modo de activación")]
    public ActivationMode mode = ActivationMode.Interaction;

    [Header("TriggerZone (solo si usas TriggerZone)")]
    [Tooltip("Centro de la zona de activación")]
    public Transform triggerZoneTransform;
    [Tooltip("Radio de la zona de activación")]
    public float triggerRadius = 3f;
    [Tooltip("Transform del jugador para medir distancia")]
    public Transform playerTransform;

    [Header("Datos de diálogo")]
    [Tooltip("ScriptableObject con .lines[]")]
    public DialogueData dialogueData;

    [Header("UI Elements (Screen Space Canvas)")]
    public Canvas dialogCanvas;    // Canvas con el panel y sprite
    public RectTransform spriteRect;      // RectTransform del sprite (Dusef)
    public RectTransform panelRect;       // RectTransform del panel de texto
    public TextMeshProUGUI dialogText;    // Componente TMP para el texto
    public GameObject nextIcon;         // Icono “Siguiente”
    public GameObject understoodIcon;   // Icono “Entendido”

    [Header("Animaciones")]
    [Tooltip("Duración en s de la animación del panel (sube desde abajo)")]
    public float panelDropDuration = 0.5f;
    [Tooltip("Duración en s de la animación del sprite (desde izquierda)")]
    public float spriteSlideDuration = 0.5f;

    [Header("Typewriter Effect")]
    [Tooltip("Tiempo entre cada carácter")]
    public float typeSpeed = 0.03f;

    [Header("Bloquear durante diálogo")]
    [Tooltip("Los ThirdPersonController a congelar movimiento y cámara")]
    public ThirdPersonController[] controllersToFreeze;

    // estados internos
    Vector2 _spriteStart, _spriteTarget;
    Vector2 _panelStart, _panelTarget;
    int _index = 0;
    bool _running = false;
    bool _triggered = false;

    void Awake()
    {
        // 1) Ocultar UI e iconos
        dialogCanvas.gameObject.SetActive(false);
        nextIcon.SetActive(false);
        understoodIcon.SetActive(false);

        // 2) Cachear posiciones destino (tal cual las sueltas en el Inspector)
        _spriteTarget = spriteRect.anchoredPosition;
        _panelTarget = panelRect.anchoredPosition;

        // 3) Calcular posiciones iniciales “off screen”
        //    - Sprite: a la izquierda
        _spriteStart = _spriteTarget + Vector2.left * spriteRect.rect.width;
        //    - Panel: **debajo** del target, para que suba hacia arriba
        _panelStart = _panelTarget - Vector2.up * panelRect.rect.height;

        // 4) Aplicar posiciones iniciales
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

    // IInteractableFeedback (Interaction mode)
    public void OnGazeEnter() { /* opcional: mostrar prompt “E” */ }
    public void OnGazeExit() { /* opcional: ocultar prompt */ }

    // IInteractable: inicia al presionar E sobre Dusef
    public void Interact()
    {
        if (mode != ActivationMode.Interaction || _running) return;
        StartCoroutine(RunDialogue());
    }

    IEnumerator RunDialogue()
    {
        _running = true;
        _index = 0;

        // 1) Congelar movimiento y cámara
        foreach (var ctrl in controllersToFreeze)
        {
            ctrl.FreezeMovement = true;
            ctrl.LockCameraPosition = true;
        }

        // 2) Mostrar canvas y animar sprite+panel
        dialogCanvas.gameObject.SetActive(true);
        float t = 0f, dur = Mathf.Max(panelDropDuration, spriteSlideDuration);
        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            // Sprite de la izquierda al target
            spriteRect.anchoredPosition = Vector2.Lerp(_spriteStart, _spriteTarget, t);
            // Panel sube desde _panelStart hasta su posición target
            panelRect.anchoredPosition = Vector2.Lerp(_panelStart, _panelTarget, t);
            yield return null;
        }

        // 3) Typewriter + avanzar con tecla E
        while (_index < dialogueData.lines.Length)
        {
            dialogText.text = "";
            foreach (char c in dialogueData.lines[_index])
            {
                dialogText.text += c;
                yield return new WaitForSeconds(typeSpeed);
            }
            _index++;

            if (_index < dialogueData.lines.Length)
            {
                nextIcon.SetActive(true);
                yield return new WaitUntil(() => Input.GetKeyDown(KeyCode.E));
                nextIcon.SetActive(false);
            }
        }

        // 4) Mostrar “Entendido” y esperar E
        understoodIcon.SetActive(true);
        yield return new WaitUntil(() => Input.GetKeyDown(KeyCode.E));
        understoodIcon.SetActive(false);

        // 5) Ocultar diálogo y descongelar todo
        dialogCanvas.gameObject.SetActive(false);
        foreach (var ctrl in controllersToFreeze)
        {
            ctrl.FreezeMovement = false;
            ctrl.LockCameraPosition = false;
        }

        _running = false;
    }
}
