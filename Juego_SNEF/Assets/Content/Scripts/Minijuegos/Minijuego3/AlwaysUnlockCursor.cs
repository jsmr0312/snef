// AlwaysUnlockCursor.cs
using UnityEngine;

[DefaultExecutionOrder(10000)] // Se ejecuta al final para sobreescribir a otros scripts
public class AlwaysUnlockCursor : MonoBehaviour
{
    [Tooltip("Mantener el cursor visible además de desbloquearlo.")]
    public bool makeVisible = true;

    [Tooltip("Si es true, este objeto persistirá entre escenas.")]
    public bool persistAcrossScenes = true;

    void Awake()
    {
        if (persistAcrossScenes) DontDestroyOnLoad(gameObject);
        ForceUnlock();
    }

    void OnEnable()
    {
        ForceUnlock();
    }

    void Update()
    {
        // Reaplica cada frame por si otro script lo cambió
        ForceUnlock();
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus) ForceUnlock();
    }

    void OnApplicationPause(bool paused)
    {
        if (!paused) ForceUnlock();
    }

    private void ForceUnlock()
    {
        if (Cursor.lockState != CursorLockMode.None)
            Cursor.lockState = CursorLockMode.None;

        if (makeVisible && !Cursor.visible)
            Cursor.visible = true;
    }
}
