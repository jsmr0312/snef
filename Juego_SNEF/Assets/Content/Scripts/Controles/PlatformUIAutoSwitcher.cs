// PlatformUIAutoSwitcher.cs
using UnityEngine;

public class PlatformUIAutoSwitcher : MonoBehaviour
{
    [Header("Roots de UI")]
    public GameObject mobileUIRoot;   // Canvas con joysticks/touch
    public GameObject desktopUIRoot;  // (opcional)

    [Header("Overrides manuales")]
    public bool forceMobile;
    public bool forceDesktop;

    void OnEnable() => Apply();
    void Start() => Apply();

    public void Apply()
    {
        bool isMobile = DetermineMobile();
        if (forceMobile) isMobile = true;
        if (forceDesktop) isMobile = false;

        if (mobileUIRoot) mobileUIRoot.SetActive(isMobile);
        if (desktopUIRoot) desktopUIRoot.SetActive(!isMobile);

        // WebGL: cursor bloqueado en desktop, libre en móvil
        if (isMobile)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
        else
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
    }

    bool DetermineMobile()
    {
        if (Input.touchSupported && SystemInfo.deviceType != DeviceType.Desktop) return true;

        // Heurística extra para WebGL
        if (Application.platform == RuntimePlatform.WebGLPlayer)
        {
            bool smallSide = Mathf.Min(Screen.width, Screen.height) <= 900;
            return Input.touchSupported || smallSide;
        }

        return Application.isMobilePlatform || SystemInfo.deviceType == DeviceType.Handheld;
    }
}
