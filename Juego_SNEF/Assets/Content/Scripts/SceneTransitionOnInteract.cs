using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(Collider))]
public class SceneTransitionOnInteract : MonoBehaviour
{
    [Header("UI Prompt (opcional)")]
    [SerializeField] private GameObject promptUI;
    [SerializeField] private Button promptButton;

    [Header("Pantalla de carga")]
    [SerializeField] private GameObject loadPanel; // Canvas/Panel que contiene el slider
    [SerializeField] private Slider loadbar;       // Tu Slider

    [Header("Escena destino")]
    [Tooltip("Nombre EXACTO como en Build Settings")]
    [SerializeField] private string sceneName;

    [Header("Seguridad anti-dobles")]
    [Tooltip("Si está activo, borra los OnClick del Inspector y deja solo este script.")]
    [SerializeField] private bool forceOverrideButtonOnClick = true;

    private bool playerInRange = false;
    private bool isLoading = false;
    private bool listenerBound = false;

    void Start()
    {
        if (promptUI) promptUI.SetActive(false);
        if (loadPanel) loadPanel.SetActive(false);
        if (loadbar) loadbar.value = 0f;

        if (promptButton)
        {
            // IMPORTANTE: limpia OnClick del Inspector para evitar el salto a 'Bosque'
            if (forceOverrideButtonOnClick)
                promptButton.onClick = new Button.ButtonClickedEvent(); // limpia incluso los persistentes

            // Nos aseguramos de no dejar listeners duplicados
            promptButton.onClick.RemoveListener(DoSceneTransition);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        playerInRange = true;
        if (promptUI) promptUI.SetActive(true);
        BindPromptButton(true);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        playerInRange = false;
        if (promptUI) promptUI.SetActive(false);
        BindPromptButton(false);
    }

    void Update()
    {
        if (!playerInRange || isLoading) return;

        // Tecla E (o lo que uses) para confirmar
        if (Input.GetKeyDown(KeyCode.E))
            DoSceneTransition();
    }

    public void DoSceneTransition()
    {
        if (isLoading) return;
        if (string.IsNullOrEmpty(sceneName)) return;

        isLoading = true;
        if (promptButton) promptButton.interactable = false; // evita doble click táctil
        if (promptUI) promptUI.SetActive(false);

        if (loadPanel) loadPanel.SetActive(true);
        if (loadbar) loadbar.value = 0f;

        StartCoroutine(LoadAsync(sceneName));
    }

    private IEnumerator LoadAsync(string targetScene)
    {
        var op = SceneManager.LoadSceneAsync(targetScene, LoadSceneMode.Single);
        // No pausamos la activación: al terminar cambia de escena
        while (!op.isDone)
        {
            // Normaliza 0..0.9 a 0..1 para el slider
            float p = Mathf.Clamp01(op.progress / 0.9f);
            if (loadbar) loadbar.value = p;
            yield return null;
        }
    }

    private void BindPromptButton(bool bind)
    {
        if (!promptButton) return;

        if (bind && !listenerBound)
        {
            promptButton.onClick.AddListener(DoSceneTransition);
            listenerBound = true;
        }
        else if (!bind && listenerBound)
        {
            promptButton.onClick.RemoveListener(DoSceneTransition);
            listenerBound = false;
        }
    }
}
