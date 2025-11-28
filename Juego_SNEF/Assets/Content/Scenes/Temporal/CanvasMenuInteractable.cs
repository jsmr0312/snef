using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CanvasMenuInteractable : MonoBehaviour,
    Interactor.IInteractable,
    Interactor.IInteractableFeedback
{
    [Header("Prompt UI")]
    [Tooltip("GameObject que indica “Presiona E / Tap”")]
    public GameObject promptUI;

    [Tooltip("¿Debe el prompt mirar a la cámara?")]
    public bool lookAtCamera = true;

    [Tooltip("Botón del prompt (si existe) que también dispara Interact()")]
    [SerializeField] private Button promptOpenButton;

    [Header("Canvas a abrir")]
    public Canvas interactionCanvas;

    [Header("Botones del Canvas")]
    [Tooltip("Botón: volver al juego (cierra el canvas)")]
    [SerializeField] private Button backToGameButton;

    [Tooltip("Botón: ir al menú (carga escena)")]
    [SerializeField] private Button goToMenuButton;

    [Tooltip("Nombre EXACTO de la escena del menú (debe estar en Build Settings)")]
    public string menuSceneName = "MainMenu";

    [Header("Pausa")]
    [Tooltip("Si está activo, al abrir el canvas pone Time.timeScale = 0 y al cerrar lo restaura.")]
    public bool pauseGameWhenOpen = true;

    [Tooltip("Opcional: también pausa audio con AudioListener.pause.")]
    public bool pauseAudioWhenOpen = false;

    [Header("Outline (QuickOutline)")]
    public Outline outline;
    public bool enableOutlineOnProximity = true;
    public Outline.Mode outlineModeNear = Outline.Mode.OutlineVisible;
    public Color outlineColorNear = Color.cyan;
    [Range(0, 10f)] public float outlineWidthNear = 4f;

    [Header("Anti-conflicto con NPC (recomendado)")]
    [Tooltip("Arrastra aquí el script del Player que maneja la interacción (tu Interactor). Se desactiva al abrir el canvas.")]
    public Behaviour playerInteractorToDisable;

    public bool manageCursor = true;

    private bool _uiOpen;
    private float _prevTimeScale = 1f;
    private bool _pausedByMe;

    void Start()
    {
        if (promptUI) promptUI.SetActive(false);
        if (interactionCanvas) interactionCanvas.gameObject.SetActive(false);

        if (outline == null) outline = GetComponent<Outline>() ?? GetComponentInChildren<Outline>(true);
        if (outline != null) outline.enabled = false;

        // Auto-busca botón dentro del prompt si no lo asignas
        if (!promptOpenButton && promptUI)
            promptOpenButton = promptUI.GetComponentInChildren<Button>(true);

        BindPromptButton(false);
        BindCanvasButtons();
    }

    void OnDisable()
    {
        // Evita quedarte con el juego pausado si desactivas/destruyes el objeto
        if (_uiOpen) ForceCleanup();

        BindPromptButton(false);
        UnbindCanvasButtons();
    }

    // ============ Interactor Feedback ============
    public void OnGazeEnter()
    {
        if (_uiOpen) return;

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
        if (outline) outline.enabled = false;
        BindPromptButton(false);
    }

    // ============ Interactor Core ============
    public void Interact()
    {
        if (_uiOpen) return;
        OpenUI();
    }

    private void OpenUI()
    {
        _uiOpen = true;

        if (promptUI) promptUI.SetActive(false);
        if (outline) outline.enabled = false;

        if (interactionCanvas) interactionCanvas.gameObject.SetActive(true);

        // Para que al abrir el canvas NO se vaya al NPC u otro interactuable
        if (playerInteractorToDisable) playerInteractorToDisable.enabled = false;

        // Pausa
        PauseGame();

        if (manageCursor)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }

    // ========= Métodos para los botones del Canvas =========
    public void UIButton_BackToGame()
    {
        CloseUI();
    }

    public void UIButton_GoToMenu()
    {
        // Importantísimo: si cargas escena con timeScale=0, se queda en 0 en el menú.
        ForceCleanup();
        SceneManager.LoadScene(menuSceneName);
    }

    private void CloseUI()
    {
        _uiOpen = false;

        if (interactionCanvas) interactionCanvas.gameObject.SetActive(false);

        if (playerInteractorToDisable) playerInteractorToDisable.enabled = true;

        ResumeGame();

        if (manageCursor)
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
    }

    private void ForceCleanup()
    {
        // Cierra y despausa “sí o sí”
        if (interactionCanvas) interactionCanvas.gameObject.SetActive(false);
        if (playerInteractorToDisable) playerInteractorToDisable.enabled = true;
        ResumeGame();

        _uiOpen = false;

        if (manageCursor)
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
    }

    // ============ Pausa ============
    private void PauseGame()
    {
        if (!pauseGameWhenOpen) return;
        if (_pausedByMe) return;

        _prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        if (pauseAudioWhenOpen) AudioListener.pause = true;

        _pausedByMe = true;
    }

    private void ResumeGame()
    {
        if (!_pausedByMe) return;

        Time.timeScale = _prevTimeScale;
        if (pauseAudioWhenOpen) AudioListener.pause = false;

        _pausedByMe = false;
    }

    // ============ Helpers ============
    void LateUpdate()
    {
        if (!lookAtCamera) return;
        if (!Camera.main) return;

        if (promptUI != null && promptUI.activeSelf)
        {
            var cam = Camera.main.transform;
            promptUI.transform.LookAt(cam);
            promptUI.transform.Rotate(0, 180, 0);
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

    private void BindCanvasButtons()
    {
        if (backToGameButton)
        {
            backToGameButton.onClick.RemoveAllListeners();
            backToGameButton.onClick.AddListener(UIButton_BackToGame);
        }

        if (goToMenuButton)
        {
            goToMenuButton.onClick.RemoveAllListeners();
            goToMenuButton.onClick.AddListener(UIButton_GoToMenu);
        }
    }

    private void UnbindCanvasButtons()
    {
        if (backToGameButton) backToGameButton.onClick.RemoveListener(UIButton_BackToGame);
        if (goToMenuButton) goToMenuButton.onClick.RemoveListener(UIButton_GoToMenu);
    }
}

