using UnityEngine;
using UnityEngine.SceneManagement;  // para LoadScene

[RequireComponent(typeof(Collider))]
public class SceneTransitionOnInteract : MonoBehaviour
{
    [Header("UI Prompt")]
    [SerializeField] private GameObject promptUI;

    [Header("Escena destino")]
    [Tooltip("Pon aquí el nombre EXACTO de la escena tal como está en Build Settings")]
    [SerializeField] private string sceneName;

    private bool playerInRange = false;

    void Start()
    {
        if (promptUI) promptUI.SetActive(false);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            if (promptUI) promptUI.SetActive(true);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            if (promptUI) promptUI.SetActive(false);
        }
    }

    void Update()
    {
        if (!playerInRange) return;

        // Método para PC (tecla E)
        if (Input.GetKeyDown(KeyCode.E))
        {
            DoSceneTransition();
        }
    }

    // Método público para llamar desde un botón UI en móvil
    public void DoSceneTransition()
    {
        if (!string.IsNullOrEmpty(sceneName))
        {
            // Opcional: aquí podrías reproducir un sonido o animación antes del cambio
            SceneManager.LoadScene(sceneName);

            // Ocultar el prompt por seguridad
            if (promptUI) promptUI.SetActive(false);
        }
    }
}
