using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SceneChanger : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("Button that, when clicked, will trigger the scene change")]
    public Button changeSceneButton;

    [Header("Scene")]
    [Tooltip("Name of the scene to load (must be added to Build Settings)")]
    public string sceneName;

    void Start()
    {
        if (changeSceneButton == null)
        {
            Debug.LogError($"[{nameof(SceneChanger)}] No Button assigned on {gameObject.name}");
            return;
        }
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError($"[{nameof(SceneChanger)}] sceneName is empty on {gameObject.name}");
            return;
        }

        changeSceneButton.onClick.AddListener(OnChangeScene);
    }

    void OnDestroy()
    {
        if (changeSceneButton != null)
            changeSceneButton.onClick.RemoveListener(OnChangeScene);
    }

    private void OnChangeScene()
    {
        SceneManager.LoadScene(sceneName);
    }
}
