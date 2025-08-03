using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

public class APIClient : MonoBehaviour
{
    public string endpoint = "https://webhook.site/tu-url";
    public string authToken = ""; // lo puedes asignar desde GameManager

    public void EnviarEvento(MetricaEvento evento)
    {
        StartCoroutine(Enviar(evento));
    }

    IEnumerator Enviar(MetricaEvento evento)
    {
        string json = JsonUtility.ToJson(evento);

        UnityWebRequest req = new UnityWebRequest(endpoint, "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        string token = authToken;
        if (!string.IsNullOrEmpty(token))
            req.SetRequestHeader("Authorization", "Bearer " + token);

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
            Debug.Log("[Métrica] Enviada: " + json);
        else
            Debug.LogError("[Métrica] Error al enviar: " + req.error);
    }
}
