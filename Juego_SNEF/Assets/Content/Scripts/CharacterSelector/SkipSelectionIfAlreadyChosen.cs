using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-5900)] // corre después de ProgressBootstrapper (-8000) y StatsProgressSync (-7000)
public class SkipSelectionIfAlreadyChosen : MonoBehaviour
{
    [Tooltip("Escena a la que ir si ya hay avatar elegido (tu menú)")]
    public string nextSceneName = "MainMenu";

    [Tooltip("Esperar estos ms para que el bootstrap aplique Data antes de decidir")]
    public int waitForBootstrapMs = 150;

    public bool log = true;

    void Start() => StartCoroutine(Route());

    IEnumerator Route()
    {
        // 1) Asegurar que ProgressCore existe (él se auto-spawnea BeforeSceneLoad, pero por si acaso)
        if (ProgressCore.I == null)
            new GameObject("ProgressCore").AddComponent<ProgressCore>();

        // 2) Espera corta para que ProgressBootstrapper/StatsSync terminen su Start()
        float t = 0f, max = waitForBootstrapMs / 1000f;
        while (t < max)
        {
            // si ya hay avatar, no esperes más
            if (HasAvatarSelected()) break;
            yield return null;
            t += Time.unscaledDeltaTime;
        }

        // 3) Si hay avatar seleccionado, saltar al menú
        if (HasAvatarSelected())
        {
            if (log) Debug.Log("[Router] Avatar encontrado. Saltando al menú…");
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            if (log) Debug.Log("[Router] No hay avatar aún. Mostrar selector.");
        }
    }

    bool HasAvatarSelected()
    {
        // a) si el progreso ya trae avatar_id (inyectado o guardado)
        if (ProgressCore.I != null && !string.IsNullOrEmpty(ProgressCore.I.GetAvatarId()))
            return true;

        // b) compat legacy por PlayerPrefs
        if (PlayerPrefs.GetInt("avatar_selected", 0) == 1)
            return true;

        // c) si ya hay índice guardado (1..12), también consideramos válido
        int idx = AvatarSelectionBridge.GetSelectedIndexFromPrefs();
        if (idx >= 1) return true;

        return false;
    }
}
