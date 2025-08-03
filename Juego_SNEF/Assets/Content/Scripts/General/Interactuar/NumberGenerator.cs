using UnityEngine;
using TMPro;

public class NumberGenerator : MonoBehaviour, Interactor.IInteractable, Interactor.IInteractableFeedback
{
    [Header("Canvas 'Presiona E' (opcional)")]
    public Canvas promptCanvas;
    public bool lookAtCamera = true;

    private int ejecuciones = 0;
    private APIClient apiClient;

    void Start()
    {
        apiClient = FindObjectOfType<APIClient>();
        if (apiClient == null)
            Debug.LogWarning("No se encontró APIClient en la escena.");
    }

    public void Interact()
    {
        int number = Random.Range(0, 100);
        ejecuciones++;

        Debug.Log("Número aleatorio: " + number);

        if (apiClient != null)
        {
            var evento = new MetricaEvento
            {
                name = "numero_generado",
                contenido = new MetricaEvento.Contenido
                {
                    nombre = number.ToString(),       // nombre = número generado
                    tiempo = ejecuciones.ToString()   // lo usamos como contador en string
                }
            };

            apiClient.EnviarEvento(evento);
        }
    }

    public void OnGazeEnter()
    {
        if (promptCanvas != null)
            promptCanvas.gameObject.SetActive(true);
    }

    public void OnGazeExit()
    {
        if (promptCanvas != null)
            promptCanvas.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (lookAtCamera && promptCanvas != null && promptCanvas.gameObject.activeSelf)
        {
            Transform cam = Camera.main.transform;
            promptCanvas.transform.LookAt(cam);
            promptCanvas.transform.Rotate(0, 180, 0);
        }
    }
}
