using UnityEngine;
using UnityEngine.SceneManagement;

public class OnboardingShowOnce : MonoBehaviour
{
    [Header("Qué mostrar")]
    [Tooltip("Raíz del canvas/panel del onboarding (donde está tu OnboardingSlider).")]
    public GameObject onboardingRoot;

    [Header("Identificador")]
    [Tooltip("Si está vacío, usa el nombre de la escena.")]
    public string customId = "";
    public bool useSceneNameAsId = true;

    [Header("Opciones")]
    [Tooltip("Forzar que SIEMPRE aparezca (para pruebas).")]
    public bool alwaysShowForTesting = false;
    [Tooltip("Marcar como visto al abrir (si no, se marca al cerrar).")]
    public bool markSeenOnOpen = true;

    string Key => $"onboard_seen::{GetId()}";

    void Reset()
    {
        if (onboardingRoot == null) onboardingRoot = gameObject;
    }

    void Start()
    {
        if (onboardingRoot == null) onboardingRoot = gameObject;

        bool shouldShow = alwaysShowForTesting || !WasSeen();
        onboardingRoot.SetActive(shouldShow);

        if (shouldShow && markSeenOnOpen && !alwaysShowForTesting)
            SetSeen();
    }

    public void CloseFromUIButton()
    {
        if (!markSeenOnOpen && !alwaysShowForTesting)
            SetSeen();

        if (onboardingRoot != null)
            onboardingRoot.SetActive(false);
    }

    public bool WasSeen() => PlayerPrefs.GetInt(Key, 0) == 1;

    public void ClearSeenFlag()
    {
        PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save();
    }

    void SetSeen()
    {
        PlayerPrefs.SetInt(Key, 1);
        PlayerPrefs.Save();
    }

    string GetId()
    {
        if (!string.IsNullOrWhiteSpace(customId)) return customId.Trim();
        if (useSceneNameAsId) return SceneManager.GetActiveScene().name;
        return "default_onboarding";
    }
}
