using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Colócalo en cada escena de ecosistema.
/// Fija StandContext.I.ecosystemName al entrar a la escena
/// para que MissionManager cuente todo en la llave correcta.
[DefaultExecutionOrder(-1000)]
public class EcosystemBootstrap : MonoBehaviour
{
    [Tooltip("Nombre del ecosistema para esta escena. Ej: \"Ecosistema 3\"")]
    public string ecosystemName = "Ecosistema 3";

    [Header("Opciones")]
    [Tooltip("Si ya hay un nombre en StandContext, ¿lo sobrescribimos?")]
    public bool overrideIfAlreadySet = true;

    [Tooltip("Intentar derivar el número desde el nombre de la escena (\"Ecosistema_3\" → \"Ecosistema 3\"). Si no encuentra, usa 'ecosystemName'.")]
    public bool tryDeriveFromSceneName = true;

    void Awake()
    {
        // Asegura el singleton
        if (StandContext.I == null)
            new GameObject("StandContext").AddComponent<StandContext>();

        if (!overrideIfAlreadySet && !string.IsNullOrEmpty(StandContext.I.ecosystemName))
            return;

        string finalEco = ecosystemName;

        if (tryDeriveFromSceneName)
        {
            var sceneName = SceneManager.GetActiveScene().name ?? "";
            var m = Regex.Match(sceneName, @"ecosistema\s*[-_ ]?\s*(\d+)", RegexOptions.IgnoreCase);
            if (m.Success) finalEco = $"Ecosistema {m.Groups[1].Value}";
        }

        // Solo seteamos el ecosistema; standId/number vacíos para no tocar contexto de stand
        StandContext.I.SetCurrentStand("", "", finalEco);

        Debug.Log($"[Bootstrap] Ecosystem set => '{finalEco}' (scene='{SceneManager.GetActiveScene().name}')");
    }
}
