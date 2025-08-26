using UnityEngine;
using System.Collections;

public class EnsurePointerLock : MonoBehaviour
{
    [Tooltip("Vuelve a solicitar pointer lock si se pierde (ESC o alt-tab).")]
    public bool relockOnFocus = true;

#if UNITY_WEBGL && !UNITY_EDITOR
    // Opcional: captura teclado en el canvas de WebGL
    void Awake()
    {
        WebGLInput.captureAllKeyboardInput = true;
    }
#endif

    void OnEnable()
    {
        StartCoroutine(TryLockNextFrame());
    }

    IEnumerator TryLockNextFrame()
    {
        // Espera 1 frame para que la escena termine de mostrarse
        yield return null;

        TryLock(); // En algunos navegadores no hará lock hasta el primer click, pero sí ocultará si ya estaba en lock.
    }

    void Update()
    {
        // Primer click del usuario: ahora sí el navegador permite pointer lock
        if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked)
            TryLock();
    }

    void OnApplicationFocus(bool focus)
    {
        if (!relockOnFocus) return;
        if (focus && Cursor.lockState != CursorLockMode.Locked)
            TryLock();
    }

    private void TryLock()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
