using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class SimplePromptCanvas : MonoBehaviour
{
    [Header("Detección (Trigger)")]
    public string playerTag = "Player";

    [Header("UI")]
    public GameObject promptUI;
    public Button promptButton;
    public GameObject windowCanvas;

    [Header("Botones dentro del Canvas")]
    public Button closeWindowButton;
    public Button quitToMenuButton;

    [Header("Input")]
    public bool enableKeyboardInteract = true;

#if ENABLE_INPUT_SYSTEM
    [Tooltip("Opcional: arrastra aquí tu acción de Interact (ej. la misma que usa tu jugador). Si lo dejas vacío, usará la tecla E directa.")]
    public InputActionReference interactAction;
#endif

    [Tooltip("Tecla fallback (por si no asignas una InputAction).")]
    public KeyCode interactKey = KeyCode.E;

    [Header("Prioridad (para que NO se dispare el NPC)")]
    [Tooltip("Componentes que quieres desactivar mientras estás dentro del trigger (ej. tu Interactor del jugador).")]
    public Behaviour[] disableWhileInZone;

    [Header("Escena de menú")]
    public string menuSceneName = "MainMenu";

    bool _playerInside;
    bool _windowOpen;

    void Awake()
    {
        if (promptUI) promptUI.SetActive(false);
        if (windowCanvas) windowCanvas.SetActive(false);

        if (promptButton)
        {
            promptButton.onClick.RemoveAllListeners();
            promptButton.onClick.AddListener(OpenWindow);
        }

        if (closeWindowButton)
        {
            closeWindowButton.onClick.RemoveAllListeners();
            closeWindowButton.onClick.AddListener(CloseWindow);
        }

        if (quitToMenuButton)
        {
            quitToMenuButton.onClick.RemoveAllListeners();
            quitToMenuButton.onClick.AddListener(GoToMenu);
        }
    }

    void OnEnable()
    {
#if ENABLE_INPUT_SYSTEM
        if (interactAction != null && interactAction.action != null)
        {
            interactAction.action.Enable();
            interactAction.action.performed += OnInteractPerformed;
        }
#endif
    }

    void OnDisable()
    {
#if ENABLE_INPUT_SYSTEM
        if (interactAction != null && interactAction.action != null)
        {
            interactAction.action.performed -= OnInteractPerformed;
            interactAction.action.Disable();
        }
#endif
    }

    void Update()
    {
        if (!_playerInside || _windowOpen || !enableKeyboardInteract) return;

#if ENABLE_INPUT_SYSTEM
        // Si NO asignaste InputAction, usamos tecla E del Input System directo:
        if (interactAction == null || interactAction.action == null)
        {
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                OpenWindow();
        }
        // Si SÍ asignaste InputAction, el callback OnInteractPerformed lo maneja.
#else
        // Old Input fallback
        if (Input.GetKeyDown(interactKey))
            OpenWindow();
#endif
    }

#if ENABLE_INPUT_SYSTEM
    void OnInteractPerformed(InputAction.CallbackContext ctx)
    {
        if (!_playerInside || _windowOpen || !enableKeyboardInteract) return;
        OpenWindow();
    }
#endif

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        _playerInside = true;

        // Bloquea otros interactores (para que la E no se vaya al NPC)
        SetOtherInteractionsEnabled(false);

        if (!_windowOpen && promptUI)
            promptUI.SetActive(true);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        _playerInside = false;

        if (promptUI) promptUI.SetActive(false);

        // Reactiva otros interactores al salir
        if (!_windowOpen)
            SetOtherInteractionsEnabled(true);
    }

    void SetOtherInteractionsEnabled(bool enabled)
    {
        if (disableWhileInZone == null) return;
        for (int i = 0; i < disableWhileInZone.Length; i++)
        {
            if (disableWhileInZone[i] != null)
                disableWhileInZone[i].enabled = enabled;
        }
    }

    public void OpenWindow()
    {
        _windowOpen = true;

        if (promptUI) promptUI.SetActive(false);
        if (windowCanvas) windowCanvas.SetActive(true);

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void CloseWindow()
    {
        _windowOpen = false;

        if (windowCanvas) windowCanvas.SetActive(false);

        if (_playerInside && promptUI)
            promptUI.SetActive(true);

        // Si sigues dentro de la zona, mantenemos bloqueado el Interactor
        if (_playerInside) SetOtherInteractionsEnabled(false);
        else SetOtherInteractionsEnabled(true);

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void GoToMenu()
    {
        if (!string.IsNullOrWhiteSpace(menuSceneName))
            SceneManager.LoadScene(menuSceneName);
    }
}
