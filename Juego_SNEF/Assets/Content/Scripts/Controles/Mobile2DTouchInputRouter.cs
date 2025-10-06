using UnityEngine;
using StarterAssets; // para UICanvasControllerInput

public class Mobile2DTouchInputRouter : MonoBehaviour
{
    [Header("Referencia al UI Input Router")]
    public UICanvasControllerInput ui;   // arrastra aquí el componente del Canvas

    // estados internos
    private bool _leftHeld;
    private bool _rightHeld;

    // --- enlaza estos métodos desde los UIVirtualButton (buttonStateOutputEvent) ---
    public void SetLeft(bool pressed)
    {
        _leftHeld = pressed;
        SendMove();
    }

    public void SetRight(bool pressed)
    {
        _rightHeld = pressed;
        SendMove();
    }

    public void SetJump(bool pressed)
    {
        if (ui != null)
            ui.VirtualJumpInput(pressed);
    }

    private void SendMove()
    {
        if (ui == null) return;
        float x = 0f;
        if (_leftHeld) x -= 1f;
        if (_rightHeld) x += 1f;
        ui.VirtualMoveInput(new Vector2(x, 0f));
    }

    void OnDisable()
    {
        // seguridad: si el objeto se desactiva, detén movimiento y salto
        _leftHeld = _rightHeld = false;
        if (ui != null)
        {
            ui.VirtualMoveInput(Vector2.zero);
            ui.VirtualJumpInput(false);
        }
    }
}
