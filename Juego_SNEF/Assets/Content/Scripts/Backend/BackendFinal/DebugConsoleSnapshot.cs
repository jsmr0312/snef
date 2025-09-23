using UnityEngine;
using UnityEngine.SceneManagement;

public class DebugConsoleSnapshot : MonoBehaviour
{
    [Header("Tecla para imprimir snapshot")]
    public KeyCode snapshotKey = KeyCode.F2;

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        WebGLBridge.OnTokenChanged += OnTokenArrived;
    }

    void OnDisable()
    {
        WebGLBridge.OnTokenChanged -= OnTokenArrived;
    }

    void Start()
    {
        LogSnapshot("Start");
    }

    void Update()
    {
        if (Input.GetKeyDown(snapshotKey))
            LogSnapshot("Manual");
    }

    void OnTokenArrived(string tok)
    {
        Debug.Log($"[DBG] Token arrived: len={SafeLen(tok)} preview={Preview(tok)}");
        LogSnapshot("TokenChanged");
    }

    static int SafeLen(string s) => string.IsNullOrEmpty(s) ? 0 : s.Length;

    static string Preview(string s)
    {
        if (string.IsNullOrEmpty(s)) return "<empty>";
        int n = Mathf.Min(16, s.Length);
        return s.Substring(0, n) + "…";
    }

    void LogSnapshot(string reason)
    {
        var token = WebGLBridge.Token;

        var mc = MetricsClient.I;
        var pr = ProgressRemote.I ?? FindObjectOfType<ProgressRemote>(true);
        var pc = ProgressCore.I ?? FindObjectOfType<ProgressCore>(true);
        var mapper = FindObjectOfType<AvatarIdMapper>(true);

        int buffered = -1;
        if (mc != null)
        {
            try
            {
                // requiere el helper GetBufferedCountForDebug() (ver abajo)
                buffered = mc.GetBufferedCountForDebug();
            }
            catch
            {
                // si aún no agregas el helper, no pasa nada
            }
        }

        // índice e id del avatar (si existen)
        int idx = AvatarSelectionBridge.GetSelectedIndexFromPrefs();
        if (idx == 0) idx = AvatarSelectionBridge.GetSelectedIndexFromLegacyPrefs();
        string avatarId = (mapper != null && idx > 0) ? mapper.GetByIndex(idx) : null;

        string sceneName = SceneManager.GetActiveScene().name;

        Debug.Log(
$@"[DBG SNAPSHOT] reason={reason}
  token.len={SafeLen(token)} preview={Preview(token)}
  metrics={(mc != null ? "yes" : "no")} buffered={(buffered >= 0 ? buffered.ToString() : "n/a")}
  progressRemote={(pr != null ? "yes" : "no")} active={(pr != null ? pr.gameObject.activeInHierarchy.ToString() : "-")} profilePath={(pr != null ? pr.profilePath : "-")}
  progressCore={(pc != null ? "yes" : "no")}
  avatarIndex={idx} avatarId={(string.IsNullOrEmpty(avatarId) ? "<null>" : avatarId)}
  scene={sceneName}"
        );
    }
}
