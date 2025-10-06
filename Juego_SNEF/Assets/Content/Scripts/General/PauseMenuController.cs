using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using StarterAssets; // Para ThirdPersonController

public class PauseMenuController : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject pauseMenuUI;
    public Button continueButton;
    public Button lobbyButton;
    public Button exitButton;

    [Header("Escenas")]
    public string lobbySceneName = "Lobby";
    public string mainMenuSceneName = "MainMenu";

    [Header("Opciones de pausa")]
    public KeyCode pauseKey = KeyCode.Escape; // ← en el Inspector ponlo en Z si quieres

    [Header("Freeze Controllers (movimiento + cámara)")]
    public ThirdPersonController[] controllersToFreeze;

    // --- NUEVO: botón opcional para pausar desde UI ---
    [Header("Pausa por botón (opcional)")]
    public Button pauseToggleButton; // arrástralo en el Inspector

    bool isPaused = false;

    void Awake()
    {
        if (pauseMenuUI != null) pauseMenuUI.SetActive(false);

        if (continueButton != null) continueButton.onClick.AddListener(Resume);
        if (lobbyButton != null) lobbyButton.onClick.AddListener(ReturnToLobby);
        if (exitButton != null) exitButton.onClick.AddListener(QuitToMainMenu);

        // Enlaza el botón opcional para pausar/reanudar
        if (pauseToggleButton != null)
            pauseToggleButton.onClick.AddListener(TogglePause);
    }

    void Update()
    {
        if (Input.GetKeyDown(pauseKey))
        {
            TogglePause();
        }
    }

    // --- NUEVO: lo mismo que presionar la tecla de pausa (Z si así lo pones) ---
    public void TogglePause()
    {
        if (isPaused) Resume();
        else Pause();
    }

    public void Pause()
    {
        if (pauseMenuUI != null) pauseMenuUI.SetActive(true);
        Time.timeScale = 0f;
        isPaused = true;

        if (controllersToFreeze != null)
        {
            foreach (var ctrl in controllersToFreeze)
            {
                if (ctrl == null) continue;
                ctrl.FreezeMovement = true;
                ctrl.LockCameraPosition = true;
            }
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void Resume()
    {
        if (pauseMenuUI != null) pauseMenuUI.SetActive(false);
        Time.timeScale = 1f;
        isPaused = false;

        if (controllersToFreeze != null)
        {
            foreach (var ctrl in controllersToFreeze)
            {
                if (ctrl == null) continue;
                ctrl.FreezeMovement = false;
                ctrl.LockCameraPosition = false;
            }
        }

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
        if (continueButton != null) continueButton.onClick.RemoveListener(Resume);
        if (lobbyButton != null) lobbyButton.onClick.RemoveListener(ReturnToLobby);
        if (exitButton != null) exitButton.onClick.RemoveListener(QuitToMainMenu);
        if (pauseToggleButton != null) pauseToggleButton.onClick.RemoveListener(TogglePause);
    }
}
