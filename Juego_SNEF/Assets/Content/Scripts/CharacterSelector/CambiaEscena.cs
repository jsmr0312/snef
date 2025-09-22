using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CambiaEscena : MonoBehaviour
{
    [Header("Referencias")]
    public GameObject funciones;        // tiene GuardarPersonaje
    public AvatarIdMapper avatarMapper; // asigna en Inspector (o lo encuentra solo)
    public Button confirmButton;        // opcional
    public GameObject loadingBlocker;   // opcional

    [Header("Escena destino")]
    public string nombreEscena;

    public void cambiar() => StartCoroutine(Flujo());

    IEnumerator Flujo()
    {
        if (confirmButton) confirmButton.interactable = false;
        if (loadingBlocker) loadingBlocker.SetActive(true);

        // 1) Persistir selección local
        var gp = funciones ? funciones.GetComponent<GuardarPersonaje>() : null;
        if (gp != null) gp.Guardar();
        yield return null;

        // 2) Asegurar ProgressCore
        if (ProgressCore.I == null) new GameObject("ProgressCore").AddComponent<ProgressCore>();

        // 3) Asegurar mapper
        if (avatarMapper == null) avatarMapper = FindObjectOfType<AvatarIdMapper>();

        // 4) Resolver índice → id/nombre para métrica
        int idx = AvatarSelectionBridge.GetSelectedIndexFromPrefs();
        if (idx == 0) idx = AvatarSelectionBridge.GetSelectedIndexFromLegacyPrefs();
        string avatarNameOrId = avatarMapper ? avatarMapper.GetByIndex(idx) : null;
        Debug.Log($"[CambiaEscena] idx={idx} → avatar={(avatarNameOrId ?? "<null>")}");

        if (!string.IsNullOrEmpty(avatarNameOrId))
            ProgressCore.I.SetAvatar(avatarNameOrId);
        else
            Debug.LogWarning("[CambiaEscena] avatar vacío; revisa AvatarIdMapper e índice.");

        // 4.1) MÉTRICA: SOLO aquí (al confirmar)
        if (!string.IsNullOrEmpty(avatarNameOrId))
        {
            if (MetricsClient.I == null) new GameObject("MetricsClient").AddComponent<MetricsClient>();
            MetricsClient.I.TrackAvatarSeleccionado(avatarNameOrId); // { name:"avatar_seleccionado", contenido:{avatar_name:...} }
        }

        // 5) Guardar progreso remoto (si aplica) y cambiar de escena
        yield return ProgressCore.I.SaveNowRoutine("avatar_selected");

        if (loadingBlocker) loadingBlocker.SetActive(false);
        SceneManager.LoadScene(nombreEscena);
    }
}
