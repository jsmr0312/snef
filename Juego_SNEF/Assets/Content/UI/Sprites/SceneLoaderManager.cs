using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneLoaderManager : MonoBehaviour
{
    [SerializeField] private Slider loadbar;
    [SerializeField] private GameObject loadPanel;

    private void Awake()
    {
        if (loadPanel) loadPanel.SetActive(false);
        if (loadbar) loadbar.value = 0f;
    }

    // Cargar por índice (Build Settings)
    public void SceneLoad(int sceneIndex)
    {
        if (loadPanel) loadPanel.SetActive(true);
        StartCoroutine(LoadAsync(sceneIndex));
    }

    // Cargar por nombre (opcional)
    public void SceneLoadByName(string sceneName)
    {
        if (loadPanel) loadPanel.SetActive(true);
        StartCoroutine(LoadAsync(sceneName));
    }

    private IEnumerator LoadAsync(int sceneIndex)
    {
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneIndex);
        yield return UpdateProgress(op);
    }

    private IEnumerator LoadAsync(string sceneName)
    {
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        yield return UpdateProgress(op);
    }

    private IEnumerator UpdateProgress(AsyncOperation op)
    {
        // No pausamos la activación; dejamos que cambie de escena al terminar.
        while (!op.isDone)
        {
            // op.progress va de 0..0.9. Normalizamos a 0..1 para el slider.
            float p = Mathf.Clamp01(op.progress / 0.9f);
            if (loadbar) loadbar.value = p;
            yield return null;
        }
    }
}
