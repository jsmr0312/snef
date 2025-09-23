using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CambiaEscena : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Script que persiste la selección local (legacy). Opcional si ya usas AvatarSelectionBridge.SetAvatarId(...)")]
    public GuardarPersonaje guardarPersonaje;   // arrástralo si lo usas
    public AvatarIdMapper avatarMapper;         // arrástralo (o se busca solo)
    public Button confirmButton;                // opcional
    public GameObject loadingBlocker;           // opcional

    [Header("Integraciones (deben existir en escena)")]
    public MetricsClient metrics;               // arrastra el GO 'Metrics' configurado
    public ProgressRemote progressRemote;       // arrastra el GO 'Progress' con ProgressRemote
    public ProgressCore progressCore;           // arrastra el GO 'ProgressCore' (o déjalo null si ya existe singleton)

    [Header("Escena destino")]
    public string nombreEscena;

    [Header("Opciones")]
    [Tooltip("Si ya reportas en el Binder, desactívalo para no duplicar la métrica.")]
    public bool sendMetricHere = true;
    public bool syncRemoteProfile = true;

    public void cambiar() => StartCoroutine(Flujo());

    IEnumerator Flujo()
    {
        if (confirmButton) confirmButton.interactable = false;
        if (loadingBlocker) loadingBlocker.SetActive(true);

        // 1) Persistir selección local (legacy, si lo usas)
        if (guardarPersonaje != null) guardarPersonaje.Guardar();
        yield return null; // da un frame para que se actualicen PlayerPrefs/estado

        // 2) Asegurar mapper
        if (avatarMapper == null) avatarMapper = FindObjectOfType<AvatarIdMapper>();

        // 3) Resolver índice → id de avatar
        int idx = AvatarSelectionBridge.GetSelectedIndexFromPrefs();
        if (idx == 0) idx = AvatarSelectionBridge.GetSelectedIndexFromLegacyPrefs();
        string avatarId = (avatarMapper != null) ? avatarMapper.GetByIndex(idx) : null;

        if (!string.IsNullOrEmpty(avatarId))
        {
            // 4) Cache local (para HUDs/reanudación)
            if (ProgressCore.I != null) ProgressCore.I.SetAvatar(avatarId);
            else if (progressCore != null) progressCore.SetAvatar(avatarId);
            else Debug.LogWarning("[CambiaEscena] No hay ProgressCore singleton visible (solo afecta cache local).");

            // 5) Métrica (avatar seleccionado)
            if (sendMetricHere)
            {
                if (metrics == null) metrics = FindObjectOfType<MetricsClient>();
                if (metrics != null) metrics.TrackAvatarSeleccionado(avatarId);
                else Debug.LogError("[CambiaEscena] No hay MetricsClient en escena. Agrega un GO 'Metrics' con MetricsClient configurado (baseUrl/metricsPath).");
            }

            // 6) Sincronización remota de perfil (avatar_id)
            if (syncRemoteProfile)
            {
                if (progressRemote == null) progressRemote = FindObjectOfType<ProgressRemote>();
                if (progressRemote != null)
                {
                    progressRemote.UpdateProfileAvatar(avatarId);
                    // opcional: cede un frame para que arranque el request (no bloquea)
                    yield return null;
                }
                else
                {
                    Debug.LogError("[CambiaEscena] No hay ProgressRemote en escena. Agrega un GO 'Progress' con ProgressRemote configurado.");
                }
            }
        }
        else
        {
            Debug.LogWarning("[CambiaEscena] No hay avatar seleccionado. Revisa AvatarIdMapper y el índice guardado.");
        }

        if (loadingBlocker) loadingBlocker.SetActive(false);
        SceneManager.LoadScene(nombreEscena);
    }
}
