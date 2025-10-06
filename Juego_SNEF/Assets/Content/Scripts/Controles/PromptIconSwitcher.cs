using UnityEngine;
using UnityEngine.UI;
using System.Runtime.InteropServices;

public class PromptIconSwitcher : MonoBehaviour
{
    [Header("Icono del prompt")]
    public Image icon;                 // Image del ícono
    public Sprite desktopSprite;       // Tecla E
    public Sprite mobileSprite;        // Tap / dedo
    public bool setNativeSize = false; // Opcional

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern bool IsMobile(); // tu .jslib
#endif

    void OnEnable() => Apply();
    void Start() => Apply();

    void Apply()
    {
        if (!icon) return;

        bool isMobile =
#if UNITY_WEBGL && !UNITY_EDITOR
            (IsMobile());
#else
            Application.isMobilePlatform;
#endif
        icon.sprite = isMobile && mobileSprite ? mobileSprite : desktopSprite;
        if (setNativeSize) icon.SetNativeSize();
    }
}
