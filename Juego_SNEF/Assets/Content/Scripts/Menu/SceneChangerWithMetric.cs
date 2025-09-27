// Assets/Content/Scripts/Backend/SceneChangerWithMetric.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SceneChangerWithMetric : MonoBehaviour
{
    [Header("UI")]
    public Button changeSceneButton;

    [Header("Scene")]
    public string sceneName;             // ← nombre de la escena del Lobby
    public string ecosystemName = "lobby"; // ← el string que quiere backend

    void Start()
    {
        if (!changeSceneButton || string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError($"[{nameof(SceneChangerWithMetric)}] Falta asignar Button o sceneName en {gameObject.name}");
            return;
        }
        changeSceneButton.onClick.AddListener(OnChangeScene);
    }

    void OnDestroy()
    {
        if (changeSceneButton) changeSceneButton.onClick.RemoveListener(OnChangeScene);
    }

    void OnChangeScene()
    {
        // 1) Evento canónico
        MetricsClient.I?.TrackEntradaEcosistema(ecosystemName);
        // 2) Cambiar de escena
        SceneManager.LoadScene(sceneName);
    }
}
