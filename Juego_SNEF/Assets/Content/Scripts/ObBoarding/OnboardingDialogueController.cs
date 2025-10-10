// OnboardingDialogueController.cs — con Candado Global de Interacción
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using StarterAssets;

public class OnboardingDialogueController : MonoBehaviour
{
    public enum ActivationMode { TimeDelay, TriggerZone, InteractZone }

    // ===== CANDADO GLOBAL =====
    // Otros interactuables (portales, arcades, etc.) deben consultar este flag antes de actuar.
    public static bool InteractionBlocked => _blockCount > 0;
    static int _blockCount = 0;
    static void PushBlock() { _blockCount++; }
    static void PopBlock() { _blockCount = Mathf.Max(0, _blockCount - 1); }

    [Header("Activación")]
    public ActivationMode activation = ActivationMode.InteractZone;
    public float delayOnStart = 2f;

    public Transform triggerZoneCenter;
    public float triggerRadius = 3f;

    public Transform playerTransform;
    public bool autoFindPlayer = true;

    [Header("Prompt de Interacción (solo visual)")]
    public GameObject promptUI;
    public KeyCode interactKey = KeyCode.E;

    [System.Serializable] public class Step { [TextArea(2, 4)] public string line; public Sprite pose; }

    [Header("Datos de diálogo")]
    public Step[] steps;
    public bool showDefaultIfEmpty = true;

    [Header("UI")]
    public GameObject rootObject;
    public Image dusefSprite;
    public Image burbujaDialogImage;
    public TextMeshProUGUI dialogueText;
    public Button nextButton;
    public Button entendidoButton;

    [Header("Animación pop (bouncy)")]
    public float popDuration = 0.25f;
    public float popOvershoot = 1.1f;

    [Header("Cursor y Control de Jugador")]
    public bool unlockCursorWhileShowing = true;
    public bool relockCursorOnClose = true;
    public ThirdPersonController[] controllersToFreeze;

    [Header("Progreso (opcional)")]
    public bool activarProgreso = true;
    public string ecosistemaId = "ecosistema_demo";
    public bool marcarComoVistoAlEmpezar = true;

    [Header("InteractZone - línea aleatoria")]
    public bool interactRandomSingleLine = true;
    public bool avoidRepeatLastRandom = true;

    [Header("HUD (Touch)")]
    public Button hudInteractButton;
    public bool useHudButton = true;

    // --- Estado interno ---
    int _index = -1;
    bool _running = false;
    bool _playerInside = false;
    bool _hudWired = false;
    bool _reserveBlockWhileInside = true; // reserva el candado mientras estés dentro del radio (antes de abrir)

    RectTransform _dusefRT, _bubbleRT;
    Vector3 _startScaleSprite, _startScaleBubble;

    bool _useRandomSingleLineStep = false;
    Step _singleStep = null;
    int _lastRandomIndex = -1;

    string ProgressKey => $"onboard::{ecosistemaId}";
    bool HasProgressCore => ProgressCore.I != null;
    bool WasSeen()
    {
        if (!activarProgreso || string.IsNullOrEmpty(ecosistemaId) || !HasProgressCore) return false;
        var list = ProgressCore.I.Data?.achievements;
        if (list == null) return false;
        return list.Exists(a => a != null && a.id == ProgressKey && a.unlocked);
    }
    void MarkSeen()
    {
        if (!activarProgreso || string.IsNullOrEmpty(ecosistemaId) || !HasProgressCore) return;
        ProgressCore.I.UpsertAchievement(ProgressKey, true);
    }

    void Awake()
    {
        if (autoFindPlayer && playerTransform == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) playerTransform = p.transform;
        }

        if (rootObject) rootObject.SetActive(false);
        if (promptUI) promptUI.SetActive(false);

        _dusefRT = dusefSprite ? dusefSprite.rectTransform : null;
        _bubbleRT = burbujaDialogImage ? burbujaDialogImage.rectTransform : null;

        _startScaleSprite = _dusefRT ? _dusefRT.localScale : Vector3.one;
        _startScaleBubble = _bubbleRT ? _bubbleRT.localScale : Vector3.one;

        if (nextButton) nextButton.onClick.AddListener(NextStep);
        if (entendidoButton) entendidoButton.onClick.AddListener(CloseSequence);

        if ((steps == null || steps.Length == 0) && showDefaultIfEmpty)
            steps = new Step[] { new Step { line = "Hola mundo", pose = dusefSprite ? dusefSprite.sprite : null } };
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
        if (_running) return;

        if (activation == ActivationMode.TriggerZone)
        {
            if (!triggerZoneCenter || !playerTransform) return;
            float d2 = (playerTransform.position - triggerZoneCenter.position).sqrMagnitude;
            if (d2 <= triggerRadius * triggerRadius) TryStartSequence();
            return;
        }

        if (activation == ActivationMode.InteractZone)
        {
            if (!triggerZoneCenter || !playerTransform) return;

            float d2 = (playerTransform.position - triggerZoneCenter.position).sqrMagnitude;
            bool inside = d2 <= triggerRadius * triggerRadius;

            if (activarProgreso && WasSeen())
            {
                if (promptUI && promptUI.activeSelf) promptUI.SetActive(false);
                ReserveBlock(false);
                _playerInside = false;
                WireHud(false);
                return;
            }

            if (promptUI && inside != _playerInside)
                promptUI.SetActive(inside);

            _playerInside = inside;

            // Reserva el candado mientras estás dentro del radio (antes de abrir)
            ReserveBlock(_playerInside && !_running && _reserveBlockWhileInside);

            if (useHudButton) WireHud(_playerInside && !_running);

            if (_playerInside && Input.GetKeyDown(interactKey))
                TryStartSequence();
        }
    }

    void ReserveBlock(bool enable)
    {
        // Evita duplicados: si ya está reservado por este controller, no vuelvas a push
        if (enable && !_reserved) { PushBlock(); _reserved = true; }
        else if (!enable && _reserved) { PopBlock(); _reserved = false; }
    }
    bool _reserved = false;

    void WireHud(bool enable)
    {
        if (!useHudButton || hudInteractButton == null) return;

        if (enable && !_hudWired)
        {
            // 👇🏼 Línea clave: me adueño del botón HUD (imposible que dispare otra cosa)
            hudInteractButton.onClick.RemoveAllListeners();
            hudInteractButton.onClick.AddListener(TryStartSequence);
            _hudWired = true;
        }
        else if (!enable && _hudWired)
        {
            hudInteractButton.onClick.RemoveListener(TryStartSequence);
            _hudWired = false;
        }
    }


    void TryStartSequence()
    {
        if (activarProgreso && WasSeen()) return;
        if (_running || steps == null || steps.Length == 0) return;

        _running = true;
        _index = -1;

        if (promptUI) promptUI.SetActive(false);
        WireHud(false);

        // BLOQUEO mientras está abierto
        PushBlock();

        if (rootObject) rootObject.SetActive(true);

        SetFrozen(true);
        if (unlockCursorWhileShowing) { Cursor.visible = true; Cursor.lockState = CursorLockMode.None; }

        if (marcarComoVistoAlEmpezar) MarkSeen();

        _useRandomSingleLineStep = (activation == ActivationMode.InteractZone && interactRandomSingleLine);
        if (_useRandomSingleLineStep)
        {
            int r = Random.Range(0, steps.Length);
            if (avoidRepeatLastRandom && steps.Length > 1 && r == _lastRandomIndex) r = (r + 1) % steps.Length;
            _lastRandomIndex = r; _singleStep = steps[r];
        }

        NextStep();
    }

    void NextStep()
    {
        if (_useRandomSingleLineStep && _singleStep != null)
        {
            ShowStep(_singleStep, true);
            _useRandomSingleLineStep = false;
            return;
        }

        _index++;
        if (_index >= steps.Length) { _index = steps.Length - 1; return; }

        var step = steps[_index];
        bool isLast = (_index == steps.Length - 1);
        ShowStep(step, isLast);
    }

    void ShowStep(Step step, bool isLast)
    {
        if (dialogueText) dialogueText.text = step.line ?? "";
        if (dusefSprite && step.pose != null) dusefSprite.sprite = step.pose;
        if (nextButton) nextButton.gameObject.SetActive(!isLast);
        if (entendidoButton) entendidoButton.gameObject.SetActive(isLast);

        if (_dusefRT) StartCoroutine(Pop(_dusefRT, _startScaleSprite));
        if (_bubbleRT) StartCoroutine(Pop(_bubbleRT, _startScaleBubble));
    }

    void CloseSequence()
    {
        if (!_running) return;
        if (activarProgreso && !marcarComoVistoAlEmpezar) MarkSeen();
        StartCoroutine(CloseCoroutine());
    }

    IEnumerator CloseCoroutine()
    {
        float t = 0f, dur = 0.2f;
        if (_dusefRT || _bubbleRT)
        {
            Vector3 s0 = _dusefRT ? _dusefRT.localScale : Vector3.one;
            Vector3 s1 = _bubbleRT ? _bubbleRT.localScale : Vector3.one;
            while (t < 1f)
            {
                t += Time.deltaTime / dur;
                if (_dusefRT) _dusefRT.localScale = Vector3.Lerp(s0, Vector3.zero, t);
                if (_bubbleRT) _bubbleRT.localScale = Vector3.Lerp(s1, Vector3.zero, t);
                yield return null;
            }
        }

        if (rootObject) rootObject.SetActive(false);
        if (_dusefRT) _dusefRT.localScale = _startScaleSprite;
        if (_bubbleRT) _bubbleRT.localScale = _startScaleBubble;

        SetFrozen(false);
        if (relockCursorOnClose) { Cursor.visible = false; Cursor.lockState = CursorLockMode.Locked; }

        _running = false;

        // Libera el candado al cerrar; si sigues dentro y tienes reserva, la reserva lo reactivará en Update
        PopBlock();

        // Reactiva prompt/HUD si procede
        if (activation == ActivationMode.InteractZone && (!activarProgreso || !WasSeen()) && _playerInside)
        {
            if (promptUI) promptUI.SetActive(true);
            WireHud(true);
        }
    }

    IEnumerator Pop(RectTransform rt, Vector3 baseScale)
    {
        if (rt == null) yield break;
        Vector3 start = baseScale * 0.9f, over = baseScale * popOvershoot, end = baseScale;
        float half = popDuration * 0.55f, t = 0f;
        while (t < half) { t += Time.deltaTime; rt.localScale = Vector3.Lerp(start, over, Mathf.SmoothStep(0, 1, t / half)); yield return null; }
        t = 0f;
        while (t < popDuration - half) { t += Time.deltaTime; rt.localScale = Vector3.Lerp(over, end, Mathf.SmoothStep(0, 1, t / (popDuration - half))); yield return null; }
        rt.localScale = end;
    }

    void SetFrozen(bool value)
    {
        if (controllersToFreeze == null) return;
        foreach (var c in controllersToFreeze) { if (c == null) continue; c.FreezeMovement = value; c.LockCameraPosition = value; }
    }

    void OnDisable() { WireHud(false); ReserveBlock(false); if (_running) { _running = false; PopBlock(); } }
    void OnDestroy() { WireHud(false); ReserveBlock(false); if (_running) { _running = false; PopBlock(); } }

    void OnDrawGizmosSelected()
    {
        if (triggerZoneCenter == null) return;
        Gizmos.color = (activation == ActivationMode.InteractZone) ? Color.green : Color.cyan;
        Gizmos.DrawWireSphere(triggerZoneCenter.position, triggerRadius);
    }
}
