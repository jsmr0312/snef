// Assets/Scripts/ApiClient.cs
using System;
using System.Text;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class APIClient : MonoBehaviour
{
    [Header("Config")]
    public string apiBase = "https://api.estudiohera.mx"; // cámbialo si fuese otro
    public float timeoutSeconds = 15f;

    public IEnumerator PostJson<TReq, TRes>(string path, TReq body,
        Action<TRes> onOk, Action<string> onErr)
    {
        var url = apiBase.TrimEnd('/') + path;
        var json = JsonUtility.ToJson(body);

        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.timeout = Mathf.CeilToInt(timeoutSeconds);

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onErr?.Invoke($"{(int)req.responseCode} {req.error} :: {req.downloadHandler.text}");
            yield break;
        }

        // La API puede devolver texto o JSON; aquí asumimos JSON { "token": "..." }
        try
        {
            var res = JsonUtility.FromJson<TRes>(req.downloadHandler.text);
            onOk?.Invoke(res);
        }
        catch
        {
            onErr?.Invoke("No pude parsear la respuesta: " + req.downloadHandler.text);
        }
    }

    // Utilidad para llamadas autenticadas con Bearer <token>
    public IEnumerator GetJsonAuth(string path, string token,
        Action<string> onOk, Action<string> onErr)
    {
        var url = apiBase.TrimEnd('/') + path;
        using var req = UnityWebRequest.Get(url);
        req.downloadHandler = new DownloadHandlerBuffer();
        if (!string.IsNullOrEmpty(token))
            req.SetRequestHeader("Authorization", "Bearer " + token);
        req.timeout = Mathf.CeilToInt(timeoutSeconds);

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success) onOk?.Invoke(req.downloadHandler.text);
        else onErr?.Invoke($"{(int)req.responseCode} {req.error} :: {req.downloadHandler.text}");
    }
}
