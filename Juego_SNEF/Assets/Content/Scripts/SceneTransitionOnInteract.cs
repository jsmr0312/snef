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

    // Dentro de SceneTransitionOnInteract
    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = true;
        if (promptUI) promptUI.SetActive(true);
        // Si NO hay botón asignado, no nos suscribimos a nada (solo E)
        if (promptButton)
        {
            promptButton.onClick.RemoveListener(DoSceneTransition);
            promptButton.onClick.AddListener(DoSceneTransition);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = false;
        if (promptUI) promptUI.SetActive(false);
        if (promptButton) promptButton.onClick.RemoveListener(DoSceneTransition);
    }

    void Update()
    {
        if (!playerInRange) return;
        if (Input.GetKeyDown(KeyCode.E)) DoSceneTransition(); // ← solo E
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
