using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CambiaEscena : MonoBehaviour
{
    [Header("Refs")]
    public GuardarPersonaje guardarPersonaje; // opcional (legacy local)
    public AvatarIdMapper avatarMapper;       // mapea índice→nombre
    public Button confirmButton;              // opcional (para desactivar al click)
    public GameObject loadingBlocker;         // opcional

    [Header("Scene")]
    public string nombreEscena;               // a dónde vas después de confirmar

    public void cambiar() => StartCoroutine(Flujo());

    IEnumerator Flujo()
    {
        if (confirmButton) confirmButton.interactable = false;
        if (loadingBlocker) loadingBlocker.SetActive(true);

        // 1) Guardado local legacy (si lo usas)
        if (guardarPersonaje) guardarPersonaje.Guardar();
        yield return null;

        if (avatarMapper == null) avatarMapper = FindObjectOfType<AvatarIdMapper>();

        // 2) Resolver el avatar seleccionado (índice → nombre)
        int idx = AvatarSelectionBridge.GetSelectedIndexFromPrefs();
        if (idx == 0) idx = AvatarSelectionBridge.GetSelectedIndexFromLegacyPrefs();
        string avatarName = avatarMapper ? avatarMapper.GetByIndex(idx) : null;

        // 3) MÉTRICA canónica (solo AQUÍ, al confirmar)
        if (!string.IsNullOrEmpty(avatarName))
            MetricsClient.I?.TrackAvatarSeleccionado(avatarName);
        else
            Debug.LogWarning("[CambiaEscena] avatarName vacío; revisa AvatarIdMapper y el índice guardado.");

        if (loadingBlocker) loadingBlocker.SetActive(false);
        SceneManager.LoadScene(nombreEscena);
    }
}
