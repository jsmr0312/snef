using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using StarterAssets;

public class OnboardingDialogueController : MonoBehaviour
{
    public enum ActivationMode { TimeDelay, TriggerZone }

    #region Inspector

    [Header("Activación")]
    public ActivationMode activation = ActivationMode.TimeDelay;
    public float delayOnStart = 2f;
    public Transform triggerZoneCenter;
    public float triggerRadius = 3f;
    public Transform playerTransform;
    public bool autoFindPlayer = true;

    [System.Serializable]
    public class Step { [TextArea(2, 4)] public string line; public Sprite pose; }

    [Header("Datos de diálogo (inspector)")]
    public Step[] steps;
    public bool showDefaultIfEmpty = true;

    [Header("UI")]
    public GameObject rootObject;
    public Image dusefSprite;           // DusefSprite
    public Image burbujaDialogImage;    // BurbujaDialogImage
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

    // === NUEVO: Integración con progreso ===
    [Header("Progreso (opcional)")]
    [Tooltip("Si está activo, el onboarding solo aparece la primera vez que se ve en este ecosistema.")]
    public bool activarProgreso = true;

    [Tooltip("Identificador único del ecosistema (ej. 'seguridad', 'resiliencia', 'control', 'libertad').")]
    public string ecosistemaId = "ecosistema_demo";

    [Tooltip("Marca como visto apenas inicia el onboarding (recomendado). Si lo desactivas, marca al cerrar.")]
    public bool marcarComoVistoAlEmpezar = true;

    #endregion

    // --- Estado interno ---
    int _index = -1;
    bool _running = false;

    RectTransform _dusefRT, _bubbleRT;
    Vector3 _startScaleSprite, _startScaleBubble;

    // ===== Helpers de progreso =====
    string ProgressKey => $"onboard::{ecosistemaId}";
    bool HasProgressCore => ProgressCore.I != null;

    bool WasSeen()
    {
        if (!activarProgreso || string.IsNullOrEmpty(ecosistemaId)) return false; // no bloquear
        if (!HasProgressCore) return false;

        var list = ProgressCore.I.Data?.achievements;
        if (list == null) return false;
        // ¿Existe achievement con id y desbloqueado?
        return list.Exists(a => a != null && a.id == ProgressKey && a.unlocked);
    }

    void MarkSeen(string reason = "onboarding_seen")
    {
        if (!activarProgreso || string.IsNullOrEmpty(ecosistemaId) || !HasProgressCore) return;
        ProgressCore.I.UpsertAchievement(ProgressKey, true);
        // Guardado local ya ocurre en Touch(); si quieres empujar remoto inmediatamente, descomenta:
        // ProgressCore.I.SaveNow(reason);
    }

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
        if (_running || activation != ActivationMode.TriggerZone) return;
        if (triggerZoneCenter == null || playerTransform == null) return;

        float d2 = (playerTransform.position - triggerZoneCenter.position).sqrMagnitude;
        if (d2 <= triggerRadius * triggerRadius)
            TryStartSequence();
    }

    void TryStartSequence()
    {
        // Si el progreso dice que ya se vio, no lo muestres (a menos que activarProgreso = false)
        if (activarProgreso && WasSeen()) return;

        if (_running || steps == null || steps.Length == 0) return;

        _running = true;
        _index = -1;

        if (rootObject) rootObject.SetActive(true);

        SetFrozen(true);
        if (unlockCursorWhileShowing)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        if (marcarComoVistoAlEmpezar) MarkSeen("onboarding_seen_on_start");

        NextStep();
    }

    void NextStep()
    {
        _index++;
        if (_index >= steps.Length)
        {
            _index = steps.Length - 1;
            return;
        }

        var step = steps[_index];

        if (dialogueText) dialogueText.text = step.line ?? "";
        if (dusefSprite && step.pose != null) dusefSprite.sprite = step.pose;

        bool isLast = (_index == steps.Length - 1);
        if (nextButton) nextButton.gameObject.SetActive(!isLast);
        if (entendidoButton) entendidoButton.gameObject.SetActive(isLast);

        if (_dusefRT) StartCoroutine(Pop(_dusefRT, _startScaleSprite));
        if (_bubbleRT) StartCoroutine(Pop(_bubbleRT, _startScaleBubble));
        if (isLast && entendidoButton)
            StartCoroutine(Pop((RectTransform)entendidoButton.transform, Vector3.one));
        else if (!isLast && nextButton)
            StartCoroutine(Pop((RectTransform)nextButton.transform, Vector3.one));
    }

    void CloseSequence()
    {
        if (!_running) return;
        if (activarProgreso && !marcarComoVistoAlEmpezar) MarkSeen("onboarding_seen_on_close");
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
        Vector3 start = baseScale * 0.9f;
        Vector3 over = baseScale * popOvershoot;
        Vector3 end = baseScale;

        float half = popDuration * 0.55f;
        float t = 0f;

        while (t < half)
        {
            t += Time.deltaTime;
            rt.localScale = Vector3.Lerp(start, over, Mathf.SmoothStep(0f, 1f, t / half));
            yield return null;
        }

        t = 0f;
        while (t < popDuration - half)
        {
            t += Time.deltaTime;
            rt.localScale = Vector3.Lerp(over, end, Mathf.SmoothStep(0f, 1f, t / (popDuration - half)));
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

    // ===== Utilidades QA =====
    [ContextMenu("QA: Borrar progreso SOLO de este onboarding")]
    void QA_ClearThisOnboardingProgress()
    {
        if (!HasProgressCore) return;
        var list = ProgressCore.I.Data?.achievements;
        if (list == null) return;
        list.RemoveAll(a => a != null && a.id == ProgressKey);
        ProgressCore.I.SaveNow("onboarding_clear_flag");
        Debug.Log($"[Onboarding] Borrado flag de {ProgressKey}");
    }
}
