// ProgressHydrator.cs
using UnityEngine;

public class ProgressHydrator : MonoBehaviour
{
    [Tooltip("Clave localStorage que entrega el wrapper de React")]
    public string localStorageKey = "progress-storage";

    void Start()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // lee directamente del localStorage del contenedor (mismo origen)
        string json = WebGLBridge.GetLocalStorageItem(localStorageKey);
        if (!string.IsNullOrEmpty(json))
            ProgressCore.I?.LoadFromBootstrapJson(json);
#else
        // En Editor tomamos el guardado local (PlayerPrefs) si existiera
        // (ProgressCore ya intenta cargarlo en Awake)
#endif
    }
}
