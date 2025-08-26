using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class MenuCursorController : MonoBehaviour
{
    [Tooltip("Desactiva PlayerInput para que el jugador/cámara no capturen el mouse")]
    public bool disablePlayerInput = true;

    void OnEnable()
    {
        FreeCursor();

#if ENABLE_INPUT_SYSTEM
        if (disablePlayerInput)
        {
            var pi = FindObjectOfType<PlayerInput>();
            if (pi) pi.enabled = false;          // Alternativa: pi.SwitchCurrentActionMap("UI");
        }
#endif
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus) FreeCursor();              // Si otro script lo cambió, lo recuperamos
    }

    void FreeCursor()
    {
        Cursor.lockState = CursorLockMode.None;  // Cursor libre
        Cursor.visible = true;                 // Cursor visible
    }
}
