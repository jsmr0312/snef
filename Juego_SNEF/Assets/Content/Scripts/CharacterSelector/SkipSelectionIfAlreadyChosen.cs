using UnityEngine;
using UnityEngine.SceneManagement;

public class SkipSelectionIfAlreadyChosen : MonoBehaviour
{
    [Tooltip("Escena a la que ir si ya hay avatar elegido")]
    public string nextSceneName = "BosquePrincipal";

    void Awake()
    {
        // Asegura ProgressCore (por si la escena de selección es la primera)
        if (ProgressCore.I == null) new GameObject("ProgressCore").AddComponent<ProgressCore>();

        // Si ya hay avatar en el JSON único → saltamos
        var id = ProgressCore.I.Data?.profile?.avatar_id;
        if (!string.IsNullOrEmpty(id))
        {
            SceneManager.LoadScene(nextSceneName);
            return;
        }

        // Fallback legacy: si ya marcaste antes en PlayerPrefs
        if (PlayerPrefs.GetInt("avatar_selected", 0) == 1)
        {
            SceneManager.LoadScene(nextSceneName);
        }
    }
}
