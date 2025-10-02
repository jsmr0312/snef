using UnityEngine;
using UnityEngine.SceneManagement;

public class SkipSelectionIfAlreadyChosen : MonoBehaviour
{
    [Tooltip("Escena a la que ir si ya hay avatar elegido")]
    public string nextSceneName = "BosquePrincipal";

    // ...
    void Awake()
    {
        if (ProgressCore.I == null) new GameObject("ProgressCore").AddComponent<ProgressCore>();

        var id = ProgressCore.I.GetAvatarId();  // <— en lugar de Data?.profile?.avatar_id
        if (!string.IsNullOrEmpty(id))
        {
            SceneManager.LoadScene(nextSceneName);
            return;
        }

        if (PlayerPrefs.GetInt("avatar_selected", 0) == 1)
            SceneManager.LoadScene(nextSceneName);
    }

}
