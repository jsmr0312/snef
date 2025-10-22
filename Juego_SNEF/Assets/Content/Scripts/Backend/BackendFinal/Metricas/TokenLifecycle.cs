// TokenLifecycle.cs
using UnityEngine;
using System.Collections;
public class TokenLifecycle : MonoBehaviour
{
    public static TokenLifecycle I { get; private set; }

    [Header("Refresh (DESACTIVADO)")]
    [Tooltip("Ya no se usa. Dejar en 0.")]
    public int proactiveRefreshSeconds = 0;
    public float refreshTimeout = 8f;
    public bool verbose = true;

    public bool RefreshInProgress { get; private set; }
    string _lastToken = "";

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        WebGLBridge.OnTokenChanged += OnTokenChanged;
        _lastToken = WebGLBridge.Token ?? "";
        // ❌ NO iniciar ningún scheduler
        if (verbose) Debug.Log("[TokenLifecycle] Refresh desactivado (token permanente).");
    }

    void OnDisable()
    {
        WebGLBridge.OnTokenChanged -= OnTokenChanged;
    }

    void OnTokenChanged(string tok)
    {
        _lastToken = tok ?? "";
        if (verbose) Debug.Log($"[TokenLifecycle] Token actualizado (len={_lastToken.Length})");
    }

    // ✅ Solo espera a que exista un token; NO refresca
    public IEnumerator WaitUntilTokenValid()
    {
        float waited = 0f;
        while (string.IsNullOrEmpty(WebGLBridge.Token))
        {
            if (waited >= refreshTimeout) yield break;
            waited += Time.unscaledDeltaTime;
            yield return null;
        }
        // Nada más que hacer: el token ya es válido y no expira
        yield break;
    }

    // ✅ NO-OP
    public IEnumerator RequestRefreshAndWait()
    {
        if (verbose) Debug.Log("[TokenLifecycle] RequestRefreshAndWait() ignorado (token permanente).");
        yield break;
    }
}
