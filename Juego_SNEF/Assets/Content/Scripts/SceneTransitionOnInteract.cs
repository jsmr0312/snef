using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;   // ← NUEVO

[RequireComponent(typeof(Collider))]
public class SceneTransitionOnInteract : MonoBehaviour
{
    [Header("UI Prompt")]
    [SerializeField] private GameObject promptUI;
    [SerializeField] private Button promptButton; // ← NUEVO (asigna el botón del panel)

    [Header("Escena destino")]
    [Tooltip("Pon el nombre EXACTO como está en Build Settings")]
    [SerializeField] private string sceneName;

    private bool playerInRange = false;

    void Start()
    {
        if (promptUI) promptUI.SetActive(false);
        // No dejamos onClicks colgados por si el prefab apuntaba a otro portal
        if (promptButton) promptButton.onClick.RemoveAllListeners();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            if (promptUI) promptUI.SetActive(true);

            if (promptButton)
            {
                promptButton.onClick.RemoveAllListeners();
                promptButton.onClick.AddListener(DoSceneTransition); // ← se ata a ESTE portal
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            if (promptUI) promptUI.SetActive(false);
            if (promptButton) promptButton.onClick.RemoveAllListeners();
        }
    }

    void Update()
    {
        if (!playerInRange) return;
        if (Input.GetKeyDown(KeyCode.E)) // PC/teclado
            DoSceneTransition();
    }

    public void DoSceneTransition()
    {
        if (!string.IsNullOrEmpty(sceneName))
        {
            SceneManager.LoadScene(sceneName);
            if (promptUI) promptUI.SetActive(false);
        }
    }
}
