using System;
using System.Runtime.InteropServices;
using UnityEngine;

public static class BrowserStorage
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern IntPtr LS_GetItem(string key);
#endif

    public static string GetItem(string key)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        var ptr = LS_GetItem(key);
        return ptr == IntPtr.Zero ? null : Marshal.PtrToStringAnsi(ptr);
#else
        // En Editor simulamos con PlayerPrefs para que no truene
        return PlayerPrefs.GetString(key, null);
#endif
    }
}
