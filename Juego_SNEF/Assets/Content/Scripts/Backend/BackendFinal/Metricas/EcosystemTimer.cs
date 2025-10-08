using UnityEngine;
using UnityEngine.SceneManagement;

/// Cronometra por-escena y emite tiempo_en_ecosistema + salida_ecosistema.
[DefaultExecutionOrder(-9999)]
public class EcosystemTimer : MonoBehaviour
{
    public static EcosystemTimer I { get; private set; }

    string _currentEco;
    float _enterRealtime;
    float _pausedAccum;
    float _pauseStart;
    bool _inPause;

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.activeSceneChanged += OnSceneChanged;
    }
    void OnDestroy() => SceneManager.activeSceneChanged -= OnSceneChanged;

    public void NotifyEnter(string ecosystemName)
    {
        FinalizeIfAny("exit");                   // cierra anterior si lo hubiera
        _currentEco = ecosystemName;
        _enterRealtime = Time.realtimeSinceStartup;
        _pausedAccum = 0f;
        _inPause = false;
    }

    public void NotifyExit(string reason = "exit")
    {
        FinalizeIfAny(reason);
        _currentEco = null;
    }

    void OnApplicationPause(bool pause)
    {
        if (string.IsNullOrEmpty(_currentEco)) return;
        if (pause && !_inPause) { _inPause = true; _pauseStart = Time.realtimeSinceStartup; }
        else if (!pause && _inPause) { _pausedAccum += Time.realtimeSinceStartup - _pauseStart; _inPause = false; }
    }

    void OnApplicationQuit()
    {
        FinalizeIfAny("exit");
    }

    void OnSceneChanged(Scene prev, Scene next)
    {
        if (!string.IsNullOrEmpty(_currentEco))
            FinalizeIfAny("exit");
    }

    void FinalizeIfAny(string reason)
    {
        if (string.IsNullOrEmpty(_currentEco)) return;
        float end = Time.realtimeSinceStartup;
        if (_inPause) { _pausedAccum += end - _pauseStart; _inPause = false; }
        int dur = Mathf.Max(0, Mathf.RoundToInt(end - _enterRealtime - _pausedAccum));

        MetricsClient.I?.TrackTiempoEnEcosistema(_currentEco, dur, true);
        MetricsClient.I?.TrackSalidaEcosistema(_currentEco, reason);

        _currentEco = null;
        _pausedAccum = 0f;
    }
}
