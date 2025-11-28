using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PopupCanvasInteractable : MonoBehaviour,
    Interactor.IInteractable,
    Interactor.IInteractableFeedback
{
    [Header("Prompt UI")]
    [Tooltip("GameObject que indica “Presiona E / Tap”")]
    public GameObject promptUI;

    [Tooltip("¿Debe el prompt mirar a la cámara?")]
    public bool lookAtCamera = true;

    [Tooltip("Botón dentro del prompt que debe lanzar Interact() (opcional)")]
    [SerializeField] private Button promptOpenButton;

    [Header("Popup UI")]
    [Tooltip("Canvas/Panel que se mostrará al interactuar")]
    public GameObject popupCanvas;

    public Button closeButton;
    public Button goMenuButton;

    [Header("Escena de menú")]
    public string menuSceneName = "MainMenu";

    private bool _isOpen = false;
    private CursorLockMode _prevLock;
    private bool _prevVisible;

    void Start()
    {
        if (promptUI) promptUI.SetActive(false);
        if (popupCanvas) popupCanvas.SetActive(false);

        // prompt button (si existe)
        if (!promptOpenButton && promptUI)
            promptOpenButton = promptUI.GetComponentInChildren<Button>(true);
        if (promptOpenButton) promptOpenButton.onClick.RemoveAllListeners();

        // botones del popup
        if (closeButton)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(ClosePopup);
        }

        if (goMenuButton)
        {
            goMenuButton.onClick.RemoveAllListeners();
            goMenuButton.onClick.AddListener(GoToMenu);
        }
    }

    void OnDisable()
    {
        BindPromptButton(false);
    }

    // ============================
    //   Interactor Feedback
    // ============================
    public void OnGazeEnter()
    {
        if (_isOpen) return;
        if (promptUI) promptUI.SetActive(true);
        BindPromptButton(true);
    }

    public void OnGazeExit()
    {
        if (_isOpen) return;
        if (promptUI) promptUI.SetActive(false);
        BindPromptButton(false);
    }

    // ============================
    //       Interactor Core
    // ============================
    public void Interact()
    {
        if (_isOpen) return;

        _isOpen = true;

        if (promptUI) promptUI.SetActive(false);
        if (popupCanvas) popupCanvas.SetActive(true);

        // IMPORTANTE: tu Interactor se pausa si el cursor está desbloqueado,
        // así que mientras el popup esté abierto, NO se irá al NPC. :contentReference[oaicite:3]{index=3}
        _prevLock = Cursor.lockState;
        _prevVisible = Cursor.visible;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void ClosePopup()
    {
        _isOpen = false;

        if (popupCanvas) popupCanvas.SetActive(false);

        Cursor.visible = _prevVisible;
        Cursor.lockState = _prevLock;

        // no forces prompt aquí; el Interactor te lo volverá a mostrar al reenfocar
        BindPromptButton(false);
    }

    public void GoToMenu()
    {
        if (!string.IsNullOrWhiteSpace(menuSceneName))
            SceneManager.LoadScene(menuSceneName);
    }

    // ============================
    //          Helpers
    // ============================
    private void BindPromptButton(bool bind)
    {
        if (!promptOpenButton) return;
        promptOpenButton.onClick.RemoveAllListeners();
        if (bind) promptOpenButton.onClick.AddListener(Interact);
    }

    void LateUpdate()
    {
        if (!lookAtCamera || !Camera.main) return;
        var cam = Camera.main.transform;

        if (promptUI && promptUI.activeSelf)
        {
            promptUI.transform.LookAt(cam);
            promptUI.transform.Rotate(0, 180, 0);
        }
        if (popupCanvas && popupCanvas.activeSelf)
        {
            popupCanvas.transform.LookAt(cam);
            popupCanvas.transform.Rotate(0, 180, 0);
        }
    }
}
