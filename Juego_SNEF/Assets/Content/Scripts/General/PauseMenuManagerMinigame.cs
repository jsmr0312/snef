using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using StarterAssets; // Para ThirdPersonController

public class PauseMenuManagerMinigame : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject pauseMenuUI;
    public Button continueButton;
    public Button returnToEcosistemaButton;
    public Button exitButton;

    [Header("Escenas")]
    public string mainMenuSceneName = "MainMenu";

    [Header("Pausa")]
    public KeyCode pauseKey = KeyCode.Escape;

    [Header("Controladores a congelar")]
    public ThirdPersonController[] controllersToFreeze;

    private bool isPaused = false;

    void Awake()
    {
        if (pauseMenuUI != null)
            pauseMenuUI.SetActive(false);

        if (continueButton != null) continueButton.onClick.AddListener(Resume);
        if (returnToEcosistemaButton != null) returnToEcosistemaButton.onClick.AddListener(ReturnToPreviousScene);
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

        foreach (var ctrl in controllersToFreeze)
        {
            ctrl.FreezeMovement = true;
            ctrl.LockCameraPosition = true;
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void Resume()
    {
        if (pauseMenuUI != null)
            pauseMenuUI.SetActive(false);

        Time.timeScale = 1f;
        isPaused = false;

        foreach (var ctrl in controllersToFreeze)
        {
            ctrl.FreezeMovement = false;
            ctrl.LockCameraPosition = false;
        }

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void ReturnToPreviousScene()
    {
        Time.timeScale = 1f;

        if (PlayerPrefs.HasKey("ReturnTo"))
        {
            string previousScene = PlayerPrefs.GetString("ReturnTo");
            SceneManager.LoadScene(previousScene);
        }
        else
        {
            Debug.LogWarning("No se encontró una escena previa guardada.");
        }
    }

    public void QuitToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    void OnDestroy()
    {
        if (continueButton != null) continueButton.onClick.RemoveListener(Resume);
        if (returnToEcosistemaButton != null) returnToEcosistemaButton.onClick.RemoveListener(ReturnToPreviousScene);
        if (exitButton != null) exitButton.onClick.RemoveListener(QuitToMainMenu);
    }
}
