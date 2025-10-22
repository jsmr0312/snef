using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using StarterAssets; // Para ThirdPersonController
using System.Collections;

public class PauseMenuController : MonoBehaviour
{
    [Header("UI Pause")]
    public GameObject pauseMenuUI;
    public Button continueButton;
    public Button lobbyButton;
    public Button exitButton;

    [Header("Escenas")]
    public string lobbySceneName = "Lobby";
    public string mainMenuSceneName = "MainMenu";

    [Header("Opciones de pausa")]
    public KeyCode pauseKey = KeyCode.Escape;

    [Header("Freeze Controllers (movimiento + cámara)")]
    public ThirdPersonController[] controllersToFreeze;

    [Header("Pantalla de carga (básica)")]
    [SerializeField] private GameObject loadPanel; // Panel/canvas con el slider
    [SerializeField] private Slider loadbar;       // Slider de la barra

    [Header("Pausa por botón (opcional)")]
    public Button pauseToggleButton;

    bool isPaused = false;
    bool isLoading = false;

    void Awake()
    {
        if (pauseMenuUI) pauseMenuUI.SetActive(false);
        if (loadPanel) loadPanel.SetActive(false);
        if (loadbar) loadbar.value = 0f;

        if (continueButton) continueButton.onClick.AddListener(Resume);
        if (lobbyButton) lobbyButton.onClick.AddListener(ReturnToLobby);
        if (exitButton) exitButton.onClick.AddListener(QuitToMainMenu);

        if (pauseToggleButton) pauseToggleButton.onClick.AddListener(TogglePause);
    }

    void Update()
    {
        if (!isLoading && Input.GetKeyDown(pauseKey))
            TogglePause();
    }

    public void TogglePause()
    {
        if (isLoading) return;
        if (isPaused) Resume();
        else Pause();
    }

    public void Pause()
    {
        if (pauseMenuUI) pauseMenuUI.SetActive(true);
        Time.timeScale = 0f;
        isPaused = true;

        if (controllersToFreeze != null)
        {
            foreach (var ctrl in controllersToFreeze)
            {
                if (!ctrl) continue;
                ctrl.FreezeMovement = true;
                ctrl.LockCameraPosition = true;
            }
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void Resume()
    {
        if (pauseMenuUI) pauseMenuUI.SetActive(false);
        Time.timeScale = 1f;
        isPaused = false;

        if (controllersToFreeze != null)
        {
            foreach (var ctrl in controllersToFreeze)
            {
                if (!ctrl) continue;
                ctrl.FreezeMovement = false;
                ctrl.LockCameraPosition = false;
            }
        }

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void ReturnToLobby()
    {
        if (isLoading) return;
        StartCoroutine(LoadSceneWithUI(lobbySceneName));
    }

    public void QuitToMainMenu()
    {
        if (isLoading) return;
        StartCoroutine(LoadSceneWithUI(mainMenuSceneName));
    }

    private IEnumerator LoadSceneWithUI(string sceneName)
    {
        isLoading = true;

        // Salimos del pause y mostramos loader
        Time.timeScale = 1f;
        isPaused = false;
        if (pauseMenuUI) pauseMenuUI.SetActive(false);

        if (loadPanel) loadPanel.SetActive(true);
        if (loadbar) loadbar.value = 0f;

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        // No pausamos la activación; que cambie al terminar.
        while (!op.isDone)
        {
            float p = Mathf.Clamp01(op.progress / 0.9f); // 0..0.9 → 0..1
            if (loadbar) loadbar.value = p;
            yield return null;
        }
    }

    void OnDestroy()
    {
        if (continueButton) continueButton.onClick.RemoveListener(Resume);
        if (lobbyButton) lobbyButton.onClick.RemoveListener(ReturnToLobby);
        if (exitButton) exitButton.onClick.RemoveListener(QuitToMainMenu);
        if (pauseToggleButton) pauseToggleButton.onClick.RemoveListener(TogglePause);
    }
}
