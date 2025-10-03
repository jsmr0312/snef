using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

public class PointerLockManager : MonoBehaviour
{
    [Tooltip("Reintenta bloquear al recuperar foco (solo si NO hay UI abierta).")]
    public bool relockOnFocus = true;

    // Marca si hay un menú/UI que necesita el cursor
    public bool uiOpen = false;

    // Si estamos esperando el primer click para poder bloquear (WebGL exige interacción)
    private bool pendingLockOnClick = false;

#if UNITY_WEBGL && !UNITY_EDITOR
    void Awake()
    {
        // Opcional, útil para capturar teclado en el canvas
        WebGLInput.captureAllKeyboardInput = true;
    }
#endif

    void OnEnable()
    {
        // Al entrar en escena: si no hay UI, dejamos listo para bloquear con el primer click
        if (!uiOpen)
            StartCoroutine(RequestLockNextFrame());
        else
            UnlockAndShow();
    }

    IEnumerator RequestLockNextFrame()
    {
        yield return null; // espera 1 frame a que todo esté listo
        RequestLockOnNextClick();
    }

    void Update()
    {
        // Si no tenemos foco, no hacemos nada.
        if (!Application.isFocused) return;

        // Si la UI está abierta, mantenemos el cursor visible y no intentamos lock.
        if (uiOpen)
        {
            EnsureUnlockedVisible();
            return;
        }

        // Si estamos esperando el primer click para bloquear...
        if (pendingLockOnClick)
        {
            // No bloquees si el click es sobre UI (botones, sliders, etc.)
            if (Input.GetMouseButtonDown(0) && !IsPointerOverUI())
            {
                TryLock();
            }
        }
        else
        {
            // Ya estamos en gameplay: si por alguna razón se perdió el lock, vuelve a pedirlo en el próximo click
            if (Cursor.lockState != CursorLockMode.Locked)
                RequestLockOnNextClick();
        }
    }

    void OnApplicationFocus(bool focus)
    {
        if (!relockOnFocus) return;

        // Si recuperamos foco y NO hay UI, prepara para bloquear en el próximo click
        if (focus && !uiOpen)
            RequestLockOnNextClick();
    }

    // ===== API pública para tus menús =====

    /// Llama esto cuando abras el menú de pausa / inventario, etc.
    public void SetUIOpen(bool open)
    {
        uiOpen = open;
        if (uiOpen)
        {
            UnlockAndShow();
        }
        else
        {
            // Cerraste la UI: en WebGL hay que esperar un click para relock
            RequestLockOnNextClick();
        }
    }

    /// Si quieres forzar el desbloqueo (p.ej. al salir a menú principal)
    public void UnlockAndShow()
    {
        pendingLockOnClick = false;
        EnsureUnlockedVisible();
    }

    /// Prepara para bloquear en el próximo click válido (no sobre UI)
    public void RequestLockOnNextClick()
    {
        pendingLockOnClick = true;
        EnsureUnlockedVisible(); // muestra el cursor hasta que el usuario haga click para volver al juego
    }

    // ===== Internos =====
    private void TryLock()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        pendingLockOnClick = false;
    }

    private void EnsureUnlockedVisible()
    {
        if (Cursor.lockState != CursorLockMode.None)
            Cursor.lockState = CursorLockMode.None;
        if (!Cursor.visible)
            Cursor.visible = true;
    }

    private bool IsPointerOverUI()
    {
        if (EventSystem.current == null) return false;
        // Esto cubre mouse en Standalone/WebGL. Si usas touch, considera EventSystem.current.IsPointerOverGameObject(touch.fingerId)
        return EventSystem.current.IsPointerOverGameObject();
    }
}
