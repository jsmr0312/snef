using UnityEngine;

public class AvatarSelectionMetricsBinder : MonoBehaviour
{
    [Header("Mapper (índice → id/nombre)")]
    public AvatarIdMapper mapper;

    int currentIndex = 1; // ajusta a tu UI real (1..N)

    void Start()
    {
        // Ya no mandamos "entered/viewed" aquí.
        // Si quieres debug visual, puedes imprimir el avatar inicial:
        // var id = mapper ? mapper.GetByIndex(currentIndex) : null;
        // Debug.Log($"[Binder] start on {id}");
    }

    // Llama esto desde tus botones de navegación izquierda/derecha
    public void OnNavigateToIndex(int idx1Based)
    {
        currentIndex = Mathf.Max(1, idx1Based);
        // Ya no mandamos métricas aquí.
    }

    // Si usas este método, solo fija selección local.
    // La métrica "avatar_seleccionado" la envía CambiaEscena al confirmar.
    public void OnConfirmSelection()
    {
        var id = mapper ? mapper.GetByIndex(currentIndex) : null;
        if (!string.IsNullOrEmpty(id))
        {
            AvatarSelectionBridge.SetAvatarId(id); // persistencia local / legacy
        }
    }
}
