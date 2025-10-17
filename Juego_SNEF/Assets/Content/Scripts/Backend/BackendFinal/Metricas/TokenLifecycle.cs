// TokenLifecycle.cs � gestor de ciclo de vida del token (equivalente a axios interceptor)
using System;
using System.Collections;
using UnityEngine;

public class TokenLifecycle : MonoBehaviour
{
    public static TokenLifecycle I { get; private set; }

    [Header("Refresh")]
    [Tooltip("Segundos antes de expirar para intentar refrescar autom�ticamente")]
    public int proactiveRefreshSeconds = 60;
    [Tooltip("Tiempo m�ximo para esperar un refresh (segundos)")]
    public float refreshTimeout = 8f;

    [Header("Logs")]
    public bool verbose = true;

    public bool RefreshInProgress { get; private set; }
    string _lastToken = "";

#if UNITY_WEBGL && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")] static extern void __RequestTokenRefresh();
#endif

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
        StartCoroutine(RefreshSchedulerLoop());
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

    IEnumerator RefreshSchedulerLoop()
    {
        var wait = new WaitForSecondsRealtime(1f);
        while (true)
        {
            yield return wait;

            var tok = WebGLBridge.Token;
            if (string.IsNullOrEmpty(tok)) continue;

            // Si expira pronto -> dispara refresh proactivo
            if (JwtLite.WillExpireSoon(tok, proactiveRefreshSeconds) && !RefreshInProgress)
            {
                if (verbose) Debug.Log("[TokenLifecycle] Token por expirar: solicitando refresh proactivo�");
                yield return RequestRefreshAndWait();
            }
        }
    }

    public IEnumerator WaitUntilTokenValid()
    {
        // Si no hay token, esperamos a que llegue (WebGLBridge ya lo emite)
        float waited = 0f;
        while (string.IsNullOrEmpty(WebGLBridge.Token))
        {
            if (waited >= refreshTimeout) yield break;
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        // Si est� expirado o por expirar, refrescamos
        if (JwtLite.IsExpired(WebGLBridge.Token) || JwtLite.WillExpireSoon(WebGLBridge.Token, 10))
        {
            yield return RequestRefreshAndWait();
        }
    }

    public IEnumerator RequestRefreshAndWait()
    {
        if (RefreshInProgress) yield break;

        RefreshInProgress = true;
        if (verbose) Debug.Log("[TokenLifecycle] RequestRefreshAndWait()");

#if UNITY_WEBGL && !UNITY_EDITOR
        try { __RequestTokenRefresh(); } catch { }
#else
        // Editor: simular que el host actualizar� el token
#endif

        // Esperar hasta que cambie el token o timeout
        string before = WebGLBridge.Token ?? "";
        float t = 0f;
        while (t < refreshTimeout)
        {
            yield return null;
            t += Time.unscaledDeltaTime;
            // �lleg� uno nuevo?
            if (!string.IsNullOrEmpty(WebGLBridge.Token) && WebGLBridge.Token != before)
                break;
        }

        RefreshInProgress = false;
        if (verbose) Debug.Log("[TokenLifecycle] Refresh terminado (ok=" + (WebGLBridge.Token != before) + ")");
    }
}
