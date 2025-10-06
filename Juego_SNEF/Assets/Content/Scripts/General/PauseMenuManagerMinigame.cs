using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using StarterAssets; // Para ThirdPersonController

public class PauseMenuManagerMinigame : MonoBehaviour
{
    [Header("UI")]
    public GameObject pauseMenuUI;
    public Button continueButton;
    public Button returnToEcosistemaButton;
    public Button exitButton;

    [Header("Escenas")]
    [Tooltip("Nombre de la escena fija a la que se regresará (debe estar en Build Settings).")]
    public string fixedReturnSceneName = "Ecosistema";
    [Tooltip("Nombre de la escena del menú principal para el botón Salir.")]
    public string mainMenuSceneName = "MainMenu";

    [Header("Pausa")]
    [Tooltip("Tecla para alternar pausa (ponla en Z si así pausas en teclado).")]
    public KeyCode pauseKey = KeyCode.Escape;

    [Header("Controladores a congelar")]
    public ThirdPersonController[] controllersToFreeze;

    // --- NUEVO: botón opcional para pausar desde UI ---
    [Header("Pausa por botón (opcional)")]
    [Tooltip("Arrastra aquí un botón de la UI si quieres que haga Toggle de la pausa.")]
    public Button pauseToggleButton; // opcional

    private bool isPaused = false;

    void Awake()
    {
        if (pauseMenuUI != null)
            pauseMenuUI.SetActive(false);

        if (continueButton != null) continueButton.onClick.AddListener(Resume);
        if (returnToEcosistemaButton != null) returnToEcosistemaButton.onClick.AddListener(ReturnToFixedScene);
        if (exitButton != null) exitButton.onClick.AddListener(QuitToMainMenu);

        // Enlaza el botón opcional para alternar pausa
        if (pauseToggleButton != null)
            pauseToggleButton.onClick.AddListener(TogglePause);

        // Estado inicial del cursor (ajústalo si tu juego lo requiere distinto)
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        if (Input.GetKeyDown(pauseKey))
        {
            TogglePause();
        }
    }

    // --- NUEVO: lo mismo que presionar la tecla de pausa ---
    public void TogglePause()
    {
        if (isPaused) Resume();
        else Pause();
    }

    public void Pause()
    {
        if (pauseMenuUI != null)
            pauseMenuUI.SetActive(true);

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
        if (pauseMenuUI != null)
            pauseMenuUI.SetActive(false);

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

    // <<< Ahora regresa a una escena fija definida en el Inspector >>>
    public void ReturnToFixedScene()
    {
        Time.timeScale = 1f;

        if (!string.IsNullOrEmpty(fixedReturnSceneName))
        {
            // Cargamos por nombre. Asegúrate de que la escena esté en File > Build Settings.
            SceneManager.LoadScene(fixedReturnSceneName);
        }
        else
        {
            Debug.LogWarning("No se asignó una escena fija en el Inspector (fixedReturnSceneName).");
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
        if (returnToEcosistemaButton != null) returnToEcosistemaButton.onClick.RemoveListener(ReturnToFixedScene);
        if (exitButton != null) exitButton.onClick.RemoveListener(QuitToMainMenu);
        if (pauseToggleButton != null) pauseToggleButton.onClick.RemoveListener(TogglePause);
    }
}
