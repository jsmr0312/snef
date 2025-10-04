using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using StarterAssets; // si no lo usas, puedes quitar esta línea

public class OnboardingDialogueController : MonoBehaviour
{
    public enum ActivationMode { TimeDelay, TriggerZone }

    #region Inspector

    [Header("Activación")]
    public ActivationMode activation = ActivationMode.TimeDelay;
    [Tooltip("Si el modo es TimeDelay, espera este tiempo tras cargar la escena")]
    public float delayOnStart = 2f;

    [Tooltip("Centro de la zona para activar por proximidad (TriggerZone)")]
    public Transform triggerZoneCenter;
    [Tooltip("Radio de la zona de activación (TriggerZone)")]
    public float triggerRadius = 3f;
    [Tooltip("Transform del jugador para medir distancia")]
    public Transform playerTransform;
    [Tooltip("Intenta encontrar al jugador por Tag 'Player' si está vacío")]
    public bool autoFindPlayer = true;

    [System.Serializable]
    public class Step
    {
        [TextArea(2, 4)] public string line;
        public Sprite pose; // sprite/pose del personaje para este paso
    }

    [Header("Datos de diálogo (inspector)")]
    public Step[] steps;
    [Tooltip("Si el array está vacío, muestra un paso de ejemplo")]
    public bool showDefaultIfEmpty = true;

    [Header("UI")]
    [Tooltip("Raíz del UI (p.ej. CanvasDusef) que se activa/desactiva")]
    public GameObject rootObject;

    [Tooltip("Sprite del personaje (DusefSprite)")]
    public Image dusefSprite;
    [Tooltip("Imagen de la burbuja (BurbujaDialogImage)")]
    public Image burbujaDialogImage;

    [Tooltip("Texto TMP del diálogo")]
    public TextMeshProUGUI dialogueText;

    [Tooltip("Botón Siguiente")]
    public Button nextButton;
    [Tooltip("Botón Entendido (solo en el último paso)")]
    public Button entendidoButton;

    [Header("Animación pop (bouncy)")]
    [Tooltip("Duración del pop en segundos")]
    public float popDuration = 0.25f;
    [Tooltip("Escala de overshoot (1.1 = 10% más grande)")]
    public float popOvershoot = 1.1f;

    [Header("Cursor y Control de Jugador")]
    [Tooltip("Al iniciar el onboarding, liberar cursor")]
    public bool unlockCursorWhileShowing = true;
    [Tooltip("Al cerrar, bloquear cursor nuevamente")]
    public bool relockCursorOnClose = true;
    [Tooltip("Opcional: controladores a ‘congelar’/‘descongelar’ (StarterAssets)")]
    public ThirdPersonController[] controllersToFreeze;

    #endregion

    // --- Estado interno ---
    int _index = -1;
    bool _running = false;

    // caches
    RectTransform _dusefRT, _bubbleRT;
    Vector3 _startScaleSprite, _startScaleBubble;

    void Awake()
    {
        if (autoFindPlayer && playerTransform == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player) playerTransform = player.transform;
        }

        if (rootObject != null) rootObject.SetActive(false);

        _dusefRT = dusefSprite ? dusefSprite.rectTransform : null;
        _bubbleRT = burbujaDialogImage ? burbujaDialogImage.rectTransform : null;

        _startScaleSprite = _dusefRT ? _dusefRT.localScale : Vector3.one;
        _startScaleBubble = _bubbleRT ? _bubbleRT.localScale : Vector3.one;

        // Listeners
        if (nextButton) nextButton.onClick.AddListener(NextStep);
        if (entendidoButton) entendidoButton.onClick.AddListener(CloseSequence);

        // Paso dummy si no configuraste aún
        if ((steps == null || steps.Length == 0) && showDefaultIfEmpty)
        {
            steps = new Step[]
            {
                new Step{ line = "Hola mundo", pose = dusefSprite ? dusefSprite.sprite : null }
            };
        }
    }

    void Start()
    {
        if (activation == ActivationMode.TimeDelay)
            StartCoroutine(StartAfterDelay());
    }

    IEnumerator StartAfterDelay()
    {
        yield return new WaitForSeconds(delayOnStart);
        TryStartSequence();
    }

    void Update()
    {
        if (_running || activation != ActivationMode.TriggerZone) return;
        if (triggerZoneCenter == null || playerTransform == null) return;

        float d2 = (playerTransform.position - triggerZoneCenter.position).sqrMagnitude;
        if (d2 <= triggerRadius * triggerRadius)
            TryStartSequence();
    }

    void TryStartSequence()
    {
        if (_running || steps == null || steps.Length == 0) return;

        _running = true;
        _index = -1;

        // Mostrar UI
        if (rootObject) rootObject.SetActive(true);

        // Control/cursor opcional
        SetFrozen(true);
        if (unlockCursorWhileShowing)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        // Primer paso
        NextStep();
    }

    void NextStep()
    {
        _index++;
        if (_index >= steps.Length)
        {
            // Seguridad: si alguien spamea “Siguiente” en el último paso
            _index = steps.Length - 1;
            return;
        }

        var step = steps[_index];

        if (dialogueText) dialogueText.text = step.line ?? "";

        if (dusefSprite && step.pose != null)
            dusefSprite.sprite = step.pose;

        // Mostrar/ocultar botones según sea último
        bool isLast = (_index == steps.Length - 1);
        if (nextButton) nextButton.gameObject.SetActive(!isLast);
        if (entendidoButton) entendidoButton.gameObject.SetActive(isLast);

        // Pop del personaje y la burbuja
        if (_dusefRT) StartCoroutine(Pop(_dusefRT, _startScaleSprite));
        if (_bubbleRT) StartCoroutine(Pop(_bubbleRT, _startScaleBubble));

        // Pop del botón que se muestre en este paso (bonito detalle)
        if (isLast && entendidoButton)
            StartCoroutine(Pop(entendidoButton.transform as RectTransform, Vector3.one));
        else if (!isLast && nextButton)
            StartCoroutine(Pop(nextButton.transform as RectTransform, Vector3.one));
    }

    void CloseSequence()
    {
        if (!_running) return;
        StartCoroutine(CloseCoroutine());
    }

    IEnumerator CloseCoroutine()
    {
        // Animación de salida rápida (shrink)
        float t = 0f, dur = 0.2f;
        if (_dusefRT || _bubbleRT)
        {
            Vector3 s0 = _dusefRT ? _dusefRT.localScale : Vector3.one;
            Vector3 s1 = _bubbleRT ? _bubbleRT.localScale : Vector3.one;

            while (t < 1f)
            {
                t += Time.deltaTime / dur;
                float f = 1f - t; // shrink
                if (_dusefRT) _dusefRT.localScale = Vector3.Lerp(s0, Vector3.zero, t);
                if (_bubbleRT) _bubbleRT.localScale = Vector3.Lerp(s1, Vector3.zero, t);
                yield return null;
            }
        }

        if (rootObject) rootObject.SetActive(false);

        // Restaurar escalas para la próxima vez
        if (_dusefRT) _dusefRT.localScale = _startScaleSprite;
        if (_bubbleRT) _bubbleRT.localScale = _startScaleBubble;

        // Descongelar/bloquear cursor opcional
        SetFrozen(false);
        if (relockCursorOnClose)
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        _running = false;
    }

    IEnumerator Pop(RectTransform rt, Vector3 baseScale)
    {
        if (rt == null) yield break;

        // Pequeño “rebote” tipo overshoot
        Vector3 start = baseScale * 0.9f;
        Vector3 over = baseScale * popOvershoot; // overshoot
        Vector3 end = baseScale;

        float half = popDuration * 0.55f;
        float t = 0f;

        // grow to overshoot
        while (t < half)
        {
            t += Time.deltaTime;
            float f = Mathf.SmoothStep(0f, 1f, t / half);
            rt.localScale = Vector3.Lerp(start, over, f);
            yield return null;
        }

        // settle to final
        t = 0f;
        while (t < popDuration - half)
        {
            t += Time.deltaTime;
            float f = Mathf.SmoothStep(0f, 1f, t / (popDuration - half));
            rt.localScale = Vector3.Lerp(over, end, f);
            yield return null;
        }

        rt.localScale = end;
    }

    void SetFrozen(bool value)
    {
        if (controllersToFreeze == null) return;
        foreach (var c in controllersToFreeze)
        {
            if (c == null) continue;
            // Estos flags existen en tu proyecto (ya los usas en PauseMenuController)
            c.FreezeMovement = value;
            c.LockCameraPosition = value;
        }
    }

    void OnDrawGizmosSelected()
    {
        if (activation != ActivationMode.TriggerZone || triggerZoneCenter == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(triggerZoneCenter.position, triggerRadius);
    }
}
