// PlatformUIAutoSwitcher.cs
using UnityEngine;
using System.Runtime.InteropServices;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PlatformUIAutoSwitcher : MonoBehaviour
{
    [Header("Roots de UI")]
    public GameObject mobileUIRoot;   // ← tu Canvas de joysticks
    public GameObject desktopUIRoot;  // ← HUD/teclado (opcional)

    [Header("Overrides (pruebas)")]
    public bool forceMobile;
    public bool forceDesktop;

    [Header("Ajuste por pistas de entrada")]
    public bool autoSwitchOnRuntimeHints = true;
    public float debounceSeconds = 0.4f;

    [Header("WebGL (heurística JS opcional)")]
    public bool useWebGLJsHeuristics = true;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern bool IsMobileBrowser();
#endif

    bool _isMobile;
    float _lastSwitchTime = -999f;

    void Awake() => Apply(DetectMobile());
    void OnEnable() => Apply(_isMobile);

    // Llama esto desde tu botón "Tocar para jugar" (WebGL) para bloquear el cursor legalmente
    public void RequestPointerLock()
    {
#if UNITY_WEBGL
        if (!_isMobile) { Cursor.visible = false; Cursor.lockState = CursorLockMode.Locked; }
#endif
    }

    void Apply(bool useMobile)
    {
        _isMobile = useMobile;
        if (mobileUIRoot) mobileUIRoot.SetActive(_isMobile);
        if (desktopUIRoot) desktopUIRoot.SetActive(!_isMobile);

#if !UNITY_WEBGL
        if (_isMobile) { Cursor.visible = true; Cursor.lockState = CursorLockMode.None; }
        else           { Cursor.visible = false; Cursor.lockState = CursorLockMode.Locked; }
#else
        // En WebGL, deja el cursor libre por defecto; bloquea tras un gesto con RequestPointerLock()
        Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
#endif
    }

    bool DetectMobile()
    {
        if (forceMobile) return true;
        if (forceDesktop) return false;

        // 1) Señales de Unity
        bool mobile = Application.isMobilePlatform;

        // 2) Señales de InputSystem u Old Input
#if ENABLE_INPUT_SYSTEM
        if (Touchscreen.current != null) mobile = true;
#else
        if (Input.touchSupported)        mobile = true;
#endif

        // 3) Heurística JS solo en WebGL real (no en Editor)
#if UNITY_WEBGL && !UNITY_EDITOR
        if (useWebGLJsHeuristics)
        {
            bool jsSaysMobile = false;
            try { jsSaysMobile = IsMobileBrowser(); } catch {}
            if (jsSaysMobile) mobile = true;
        }
#endif
        return mobile;
    }

    void Update()
    {
        if (!autoSwitchOnRuntimeHints) return;
        if (Time.unscaledTime - _lastSwitchTime < debounceSeconds) return;

        // Si vemos toque → cambia a móvil
#if ENABLE_INPUT_SYSTEM
        bool sawTouch = Touchscreen.current != null && Touchscreen.current.wasUpdatedThisFrame;
#else
        bool sawTouch = Input.touchCount > 0;
#endif
        if (!_isMobile && sawTouch) { _lastSwitchTime = Time.unscaledTime; Apply(true); return; }

        // Si estamos en móvil y vemos mouse/teclado/gamepad → cambia a desktop
        bool sawDesktopSignal = false;
#if ENABLE_INPUT_SYSTEM
        sawDesktopSignal =
          (Mouse.current != null && Mouse.current.wasUpdatedThisFrame) ||
          (Keyboard.current != null && Keyboard.current.wasUpdatedThisFrame) ||
          (Gamepad.current != null && Gamepad.current.wasUpdatedThisFrame);
#else
        sawDesktopSignal = Input.GetMouseButtonDown(0) || Input.anyKeyDown;
#endif
        if (_isMobile && sawDesktopSignal) { _lastSwitchTime = Time.unscaledTime; Apply(false); }
    }
}
