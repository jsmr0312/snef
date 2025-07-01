using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using StarterAssets; // Para ThirdPersonController

public class PauseMenuController : MonoBehaviour
{
    [Header("UI Elements")]
    [Tooltip("El panel que contiene el menú de pausa")]
    public GameObject pauseMenuUI;
    [Tooltip("Botón para continuar el juego")]
    public Button continueButton;
    [Tooltip("Botón para regresar al lobby")]
    public Button lobbyButton;
    [Tooltip("Botón para abandonar al menú principal")]
    public Button exitButton;

    [Header("Escenas")]
    [Tooltip("Nombre de la escena de Lobby")]
    public string lobbySceneName = "Lobby";
    [Tooltip("Nombre de la escena de Menú Principal")]
    public string mainMenuSceneName = "MainMenu";

    [Header("Opciones de pausa")]
    [Tooltip("Tecla para alternar pausa")]
    public KeyCode pauseKey = KeyCode.Escape;

    [Header("Freeze Controllers (movimiento + cámara)")]
    [Tooltip("Los ThirdPersonController a congelar durante la pausa")]
    public ThirdPersonController[] controllersToFreeze;

    bool isPaused = false;

    void Awake()
    {
        // Asegura que el menú inicie oculto
        if (pauseMenuUI != null)
            pauseMenuUI.SetActive(false);

        // Conectar los botones
        if (continueButton != null) continueButton.onClick.AddListener(Resume);
        if (lobbyButton != null) lobbyButton.onClick.AddListener(ReturnToLobby);
        if (exitButton != null) exitButton.onClick.AddListener(QuitToMainMenu);
    }

    void Update()
    {
        if (Input.GetKeyDown(pauseKey))
        {
            if (isPaused) Resume();
            else Pause();
        }
    }

    public void Pause()
    {
        if (pauseMenuUI != null)
            pauseMenuUI.SetActive(true);

        Time.timeScale = 0f;
        isPaused = true;

        // Bloquear movimiento y cámara
        foreach (var ctrl in controllersToFreeze)
        {
            ctrl.FreezeMovement = true;
            ctrl.LockCameraPosition = true;
        }

        // Mostrar cursor
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void Resume()
    {
        if (pauseMenuUI != null)
            pauseMenuUI.SetActive(false);

        Time.timeScale = 1f;
        isPaused = false;

        // Desbloquear movimiento y cámara
        foreach (var ctrl in controllersToFreeze)
        {
            ctrl.FreezeMovement = false;
            ctrl.LockCameraPosition = false;
        }

        // Ocultar cursor
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void ReturnToLobby()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(lobbySceneName);
    }

    public void QuitToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    void OnDestroy()
    {
        // Limpiar listeners
        if (continueButton != null) continueButton.onClick.RemoveListener(Resume);
        if (lobbyButton != null) lobbyButton.onClick.RemoveListener(ReturnToLobby);
        if (exitButton != null) exitButton.onClick.RemoveListener(QuitToMainMenu);
    }
}
