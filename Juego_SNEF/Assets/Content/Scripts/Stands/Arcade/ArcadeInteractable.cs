using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;            // Slider
using TMPro;
using System.Collections;

public class ArcadeInteractable : MonoBehaviour,
    Interactor.IInteractable,
    Interactor.IInteractableFeedback
{
    [Header("Stand Context")]
    public string standId;
    public string standNumber;
    public string ecosystemName;
    public string miniGameId; // ID base del minijuego (el mismo en todas las plantillas)

    [Header("Minigame Context (opcional)")]
    [Tooltip("Nombre legible del minijuego para métricas. Si lo dejas vacío, usará el nombre de la escena.")]
    public string minigameDisplayName;

    [Header("Prompt UI")]
    [Tooltip("GameObject que indica “Presiona E / Tap”")]
    public GameObject promptUI;
    [Tooltip("¿Debe el prompt mirar a la cámara?")]
    public bool lookAtCamera = true;

    [Tooltip("Botón del prompt que debe lanzar Interact()")]
    [SerializeField] private Button promptOpenButton;

    [Header("Lock UI")]
    [Tooltip("Canvas con el icono de candado (se muestra hasta desbloquear)")]
    public Canvas lockCanvas;
    [Tooltip("Texto que aparece al intentar interactuar bloqueado")]
    public TextMeshProUGUI lockMessageText;
    [Tooltip("Duración en segundos del mensaje de bloqueo")]
    public float lockMessageDuration = 2f;

    [Header("Arcade Settings")]
    [Tooltip("Nombre de la escena del mini-juego")]
    public string arcadeSceneName;

    [Header("Lock Settings")]
    [Tooltip("Si está activo, la arcade inicia DESBLOQUEADA")]
    public bool startUnlocked = false;

    [Header("Outline (QuickOutline)")]
    [Tooltip("Componente Outline (si no lo asignas, se auto-busca en este objeto o hijos)")]
    public Outline outline;
    [Tooltip("Encender outline al enfocar con la mirada")]
    public bool enableOutlineOnProximity = true;
    public Outline.Mode outlineModeNear = Outline.Mode.OutlineVisible;
    public Color outlineColorNear = Color.cyan;
    [Range(0, 10f)] public float outlineWidthNear = 4f;

    [Header("Pantalla de carga (básica)")]
    [SerializeField] private GameObject loadPanel; // Panel/canvas con el slider
    [SerializeField] private Slider loadbar;       // Slider de la barra

    // Estado interno
    private bool _isLocked = true;
    private Coroutine _hideMsgRoutine;

    void Start()
    {
        // Estado inicial
        SetLocked(!startUnlocked);

        if (ProgressCore.I != null && ProgressCore.I.Stand_IsArcadeUnlocked(standId))
            SetLocked(false);

        var phase = ProgressCore.I?.Stand_GetPhase(standId);
        if (phase == "Final")
        {
            SetLocked(false);
            if (ProgressCore.I != null && !ProgressCore.I.Stand_IsArcadeUnlocked(standId))
            {
                ProgressCore.I.Stand_UnlockArcade(standId);
                ProgressCore.I.SaveNow("fix_unlock_arcade_on_start_" + standId);
            }
        }

        if (promptUI) promptUI.SetActive(false);
        if (lockMessageText) lockMessageText.gameObject.SetActive(false);

        if (outline == null) outline = GetComponent<Outline>() ?? GetComponentInChildren<Outline>();
        if (outline != null) outline.enabled = false;

        // Botón dentro del prompt
        if (!promptOpenButton && promptUI)
            promptOpenButton = promptUI.GetComponentInChildren<Button>(true);
        if (promptOpenButton) promptOpenButton.onClick.RemoveAllListeners();

        // Loader UI inicial apagado
        if (loadPanel) loadPanel.SetActive(false);
        if (loadbar) loadbar.value = 0f;
    }

    void OnDisable()
    {
        BindPromptButton(false);
    }

    private void SetLocked(bool value)
    {
        _isLocked = value;
        if (lockCanvas) lockCanvas.gameObject.SetActive(_isLocked);
    }

    public void UnlockArcade()
    {
        SetLocked(false);
        ProgressCore.I?.Stand_UnlockArcade(standId);
        ProgressCore.I?.SaveNow("arcade_unlocked_" + standId);
    }

    public void LockArcade()
    {
        SetLocked(true);
        ProgressCore.I?.Stand_LockArcade(standId);
    }

    [ContextMenu("Forzar Desbloqueo (Editor)")]
    private void ContextUnlock() => SetLocked(false);

    // ============================
    //   Interactor Feedback
    // ============================
    public void OnGazeEnter()
    {
        if (promptUI) promptUI.SetActive(true);
        if (enableOutlineOnProximity && outline)
        {
            ApplyOutlineSettings();
            outline.enabled = true;
        }
        BindPromptButton(true);
    }

    public void OnGazeExit()
    {
        if (promptUI) promptUI.SetActive(false);
        if (lockMessageText) lockMessageText.gameObject.SetActive(false);
        if (_hideMsgRoutine != null) StopCoroutine(_hideMsgRoutine);
        if (outline) outline.enabled = false;
        BindPromptButton(false);
    }

    // ============================
    //       Interactor Core
    // ============================
    public void Interact()
    {
        if (_isLocked)
        {
            ShowLockMessage();
            return;
        }

        // Guardar retorno
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            var pos = player.transform.position;
            PlayerPrefs.SetFloat("SavedX", pos.x);
            PlayerPrefs.SetFloat("SavedY", pos.y);
            PlayerPrefs.SetFloat("SavedZ", pos.z);
            PlayerPrefs.SetString("ReturnTo", SceneManager.GetActiveScene().name);
            PlayerPrefs.Save();
        }

        // Scope de minijuego para métricas
        if (MinigameScope.I == null)
            new GameObject("MinigameScope").AddComponent<MinigameScope>();

        string friendlyName = string.IsNullOrEmpty(minigameDisplayName) ? arcadeSceneName : minigameDisplayName;

        MinigameScope.I.Begin(
            standId,
            standNumber,
            ecosystemName,
            miniGameId,
            friendlyName
        );

        // --- Cargar escena con loader básico ---
        if (!string.IsNullOrEmpty(arcadeSceneName))
        {
            if (promptUI) promptUI.SetActive(false);
            if (loadPanel) loadPanel.SetActive(true);
            if (loadbar) loadbar.value = 0f;

            // Si no hay referencias al loader, cae a carga directa
            if (loadPanel && loadbar) StartCoroutine(LoadAsync(arcadeSceneName));
            else SceneManager.LoadScene(arcadeSceneName);
        }
        else
        {
            Debug.LogWarning("ArcadeInteractable: arcadeSceneName no asignado.");
        }
    }

    // ============================
    //          Helpers
    // ============================
    private IEnumerator LoadAsync(string sceneName)
    {
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        while (!op.isDone)
        {
            // op.progress 0..0.9 → normalizamos 0..1
            float p = Mathf.Clamp01(op.progress / 0.9f);
            if (loadbar) loadbar.value = p;
            yield return null;
        }
    }

    private void ShowLockMessage()
    {
        if (lockMessageText == null) return;
        lockMessageText.text = "Termina de hablar con el personaje";
        lockMessageText.gameObject.SetActive(true);
        if (_hideMsgRoutine != null) StopCoroutine(_hideMsgRoutine);
        _hideMsgRoutine = StartCoroutine(HideLockMessageAfterDelay());
    }

    private IEnumerator HideLockMessageAfterDelay()
    {
        yield return new WaitForSeconds(lockMessageDuration);
        if (lockMessageText)
            lockMessageText.gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        if (!Camera.main) return;
        var cam = Camera.main.transform;

        if (lookAtCamera && promptUI != null && promptUI.activeSelf)
        {
            promptUI.transform.LookAt(cam);
            promptUI.transform.Rotate(0, 180, 0);
        }

        if (lookAtCamera && lockCanvas != null && lockCanvas.gameObject.activeSelf)
        {
            var t = lockCanvas.transform;
            t.LookAt(cam);
            t.Rotate(0, 180, 0);
        }
    }

    private void ApplyOutlineSettings()
    {
        if (!outline) return;
        outline.OutlineMode = outlineModeNear;
        outline.OutlineColor = outlineColorNear;
        outline.OutlineWidth = outlineWidthNear;
    }

    private void BindPromptButton(bool bind)
    {
        if (!promptOpenButton) return;
        promptOpenButton.onClick.RemoveAllListeners();
        if (bind) promptOpenButton.onClick.AddListener(Interact);
    }
}
