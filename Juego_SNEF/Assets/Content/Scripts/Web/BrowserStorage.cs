using System.Runtime.InteropServices;
using UnityEngine;

public static class BrowserStorage
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern string __GetLocalStorageItem(string key);
#endif

    public static string GetItem(string key)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try { return __GetLocalStorageItem(key); } catch { return null; }
#else
        return PlayerPrefs.GetString(key, null);
#endif
    }
}
