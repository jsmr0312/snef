using UnityEngine;

public class AvatarSelectionMetricsBinder : MonoBehaviour
{
    [Header("Mapper (índice → id)")]
    public AvatarIdMapper mapper;

    int currentIndex = 1; // ajusta a tu UI real (1..N)

    void Start()
    {
        MetricsClient.I?.TrackAvatarScreenEntered();
        // Si tu UI de inicio ya muestra una tarjeta, repórtala:
        var id = mapper ? mapper.GetByIndex(currentIndex) : null;
        if (!string.IsNullOrEmpty(id))
            MetricsClient.I?.TrackAvatarCardViewed(id);
    }

    // Llama esto desde tus botones de navegación izquierda/derecha
    public void OnNavigateToIndex(int idx1Based)
    {
        currentIndex = Mathf.Max(1, idx1Based);
        var id = mapper ? mapper.GetByIndex(currentIndex) : null;
        if (!string.IsNullOrEmpty(id))
            MetricsClient.I?.TrackAvatarCardViewed(id);
    }

    // Llama esto al confirmar selección (además de tu flujo normal de guardar avatar)
    public void OnConfirmSelection()
    {
        var id = mapper ? mapper.GetByIndex(currentIndex) : null;
        if (!string.IsNullOrEmpty(id))
        {
            // 1) Métrica
            MetricsClient.I?.TrackAvatarSeleccionado(id);
            // 2) Progreso (persistencia real)
            AvatarSelectionBridge.SetAvatarId(id);
        }
    }
}
