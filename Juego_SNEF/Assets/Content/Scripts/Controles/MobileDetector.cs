using UnityEngine;
using System.Runtime.InteropServices;

public class MobileDetector : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern bool IsMobile();
#endif

    [Header("Asigna tu Canvas de joysticks")]
    public GameObject joysticks;

    [Header("Opcional: UI de escritorio para alternar")]
    public GameObject desktopUIRoot;

    void Start()
    {
        bool isMobile = IsRunningOnMobile();

        if (joysticks) joysticks.SetActive(isMobile);
        if (desktopUIRoot) desktopUIRoot.SetActive(!isMobile);

        // En WebGL dejamos el cursor libre por defecto.
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    bool IsRunningOnMobile()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // Sólo confía en el plugin JS en builds WebGL
        try { return IsMobile(); } catch { return false; }
#else
        // En editor / otros targets, usa la plataforma de Unity
        return Application.isMobilePlatform;
#endif
    }
}
