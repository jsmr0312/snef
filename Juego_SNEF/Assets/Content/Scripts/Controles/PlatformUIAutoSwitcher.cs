// PlatformUIAutoSwitcher.cs
using UnityEngine;
using System.Runtime.InteropServices;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PlatformUIAutoSwitcher : MonoBehaviour
{
    [Header("Roots de UI")]
    [Tooltip("Canvas/objetos que SOLO deben verse en móvil/tablet")]
    public GameObject mobileUIRoot;
    [Tooltip("Canvas/objetos que SOLO deben verse en desktop (opcional)")]
    public GameObject desktopUIRoot;

    [Header("Overrides manuales (solo para probar)")]
    public bool forceMobile;
    public bool forceDesktop;

    [Header("Ajuste dinámico por 'pistas' de entrada")]
    public bool autoSwitchOnRuntimeHints = true;
    public float debounceSeconds = 0.4f;

    [Header("WebGL (opcional, para máxima precisión)")]
    public bool useWebGLJsHeuristics = true;

    // Llamada JS opcional (solo WebGL build, no en Editor)
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern bool IsMobileBrowser();
#endif

    bool _isMobile;
    float _lastSwitchTime = -999f;

    void Awake()
    {
        Apply(DetectMobile());
    }

    void OnEnable()
    {
        // Por si el orden de activación lo requiere
        Apply(_isMobile);
    }

    // --- API pública: llamar esto desde un botón "Click para jugar" en WebGL ---
    // Necesario para solicitar pointer lock legalmente con un gesto del usuario.
    public void RequestPointerLock()
    {
#if UNITY_WEBGL
        if (!_isMobile)
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked; // ahora sí funciona tras un click
        }
#endif
    }

    void Apply(bool useMobile)
    {
        _isMobile = useMobile;

        if (mobileUIRoot) mobileUIRoot.SetActive(_isMobile);
        if (desktopUIRoot) desktopUIRoot.SetActive(!_isMobile);

        // No intentes bloquear/soltar cursor automáticamente aquí en WebGL:
        // hazlo con RequestPointerLock() tras un gesto del usuario.
#if !UNITY_WEBGL
        if (_isMobile)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
        else
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
#else
        // En WebGL, deja el cursor libre por defecto;
        // podrás bloquearlo cuando el usuario haga click en tu overlay/botón.
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
#endif
    }

    bool DetectMobile()
    {
        if (forceMobile) return true;
        if (forceDesktop) return false;

        // 1) Señales generales de Unity
        bool mobile = Application.isMobilePlatform;

#if ENABLE_INPUT_SYSTEM
        // 2) Si hay pantalla táctil real, favorecer móvil
        if (Touchscreen.current != null) mobile = true;
#else
        if (Input.touchSupported) mobile = true;
#endif

        // 3) Señales específicas de WebGL (opcionales, más precisas)
#if UNITY_WEBGL && !UNITY_EDITOR
        if (useWebGLJsHeuristics)
        {
            // Usa el valor del navegador (maxTouchPoints / pointer: coarse)
            bool jsSaysMobile = false;
            try { jsSaysMobile = IsMobileBrowser(); } catch { /* silencioso */ }
            if (jsSaysMobile) mobile = true;
        }
#endif

        return mobile;
    }

    void Update()
    {
        if (!autoSwitchOnRuntimeHints) return;
        if (Time.unscaledTime - _lastSwitchTime < debounceSeconds) return;

        // Si actualmente "desktop" y recibimos toque -> cambiar a móvil
      
#if ENABLE_INPUT_SYSTEM
        bool sawTouch = Touchscreen.current != null && Touchscreen.current.wasUpdatedThisFrame;
#else
    bool sawTouch = Input.touchCount > 0;
#endif
        if (!_isMobile && sawTouch)
        {
            _lastSwitchTime = Time.unscaledTime;
            Apply(true);
            return;
        }

        // Si estamos en móvil y hay mouse/teclado/gamepad activo -> cambiar a desktop
        bool sawDesktopSignal = false;
#if ENABLE_INPUT_SYSTEM
        sawDesktopSignal =
            (Mouse.current != null && Mouse.current.wasUpdatedThisFrame) ||
            (Keyboard.current != null && Keyboard.current.wasUpdatedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.wasUpdatedThisFrame);
#else
        sawDesktopSignal = Input.GetMouseButtonDown(0) || Input.anyKeyDown;
#endif
        if (_isMobile && sawDesktopSignal)
        {
            _lastSwitchTime = Time.unscaledTime;
            Apply(false);
        }
    }
}
