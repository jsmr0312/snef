// Assets/Content/Scripts/Backend/ApiClient.cs
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using System.Text;

public static class ApiClient
{
    public static string BaseUrl = "https://api.estudiohera.mx";
    public static float TimeoutSeconds = 15f;

    static string GetToken() => WebGLBridge.Token;

    static void AddAuth(UnityWebRequest req)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        var t = GetToken();
        if (!string.IsNullOrEmpty(t)) req.SetRequestHeader("Authorization", "Bearer " + t);
#endif
    }

    public static IEnumerator Get(string path, System.Action<string> onOk, System.Action<long, string> onErr = null)
    {
        using (var req = UnityWebRequest.Get(BaseUrl.TrimEnd('/') + path))
        {
            req.downloadHandler = new DownloadHandlerBuffer();
            req.timeout = Mathf.CeilToInt(TimeoutSeconds);
            AddAuth(req);
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success) onOk?.Invoke(req.downloadHandler.text);
            else onErr?.Invoke(req.responseCode, req.error);
        }
    }

    public static IEnumerator PutJson(string path, string json, System.Action<string> onOk, System.Action<long, string> onErr = null)
    {
        using (var req = new UnityWebRequest(BaseUrl.TrimEnd('/') + path, UnityWebRequest.kHttpVerbPUT))
        {
            var body = Encoding.UTF8.GetBytes(string.IsNullOrEmpty(json) ? "{}" : json);
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = Mathf.CeilToInt(TimeoutSeconds);
            AddAuth(req);
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success) onOk?.Invoke(req.downloadHandler.text);
            else onErr?.Invoke(req.responseCode, req.error);
        }
    }

    public static IEnumerator PatchJson(string path, string json, System.Action<string> onOk, System.Action<long, string> onErr = null)
    {
        using (var req = new UnityWebRequest(BaseUrl.TrimEnd('/') + path, "PATCH"))
        {
            var body = Encoding.UTF8.GetBytes(string.IsNullOrEmpty(json) ? "{}" : json);
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = Mathf.CeilToInt(TimeoutSeconds);
            AddAuth(req);
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success) onOk?.Invoke(req.downloadHandler.text);
            else onErr?.Invoke(req.responseCode, req.error);
        }
    }

    public static IEnumerator PostJson(string path, string json, System.Action<string> onOk, System.Action<long, string> onErr = null)
    {
        using (var req = new UnityWebRequest(BaseUrl.TrimEnd('/') + path, UnityWebRequest.kHttpVerbPOST))
        {
            var body = Encoding.UTF8.GetBytes(string.IsNullOrEmpty(json) ? "{}" : json);
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = Mathf.CeilToInt(TimeoutSeconds);
            AddAuth(req);
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success) onOk?.Invoke(req.downloadHandler.text);
            else onErr?.Invoke(req.responseCode, req.error);
        }
    }
}
