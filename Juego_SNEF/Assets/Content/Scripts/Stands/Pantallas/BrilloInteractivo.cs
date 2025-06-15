using UnityEngine;

public class BrilloInteractivo : MonoBehaviour, Interactor.IInteractable, Interactor.IInteractableFeedback
{
    [Header("Canvas 'Presiona E'")]
    public Canvas promptCanvas;
    public bool lookAtCamera = true;

    [Header("Configuración del brillo")]
    public Color brilloColor = Color.cyan;
    public float pulsoVelocidad = 2f;        // velocidad de pulso
    public float pulsoIntensidadMax = 2f;    // intensidad máxima del brillo

    private MaterialPropertyBlock propBlock;
    private Renderer rend;

    private bool brilloActivo = false;

    private void Start()
    {
        if (promptCanvas != null)
            promptCanvas.gameObject.SetActive(false);

        rend = GetComponent<Renderer>();
        propBlock = new MaterialPropertyBlock();

        // Inicia sin brillo
        rend.GetPropertyBlock(propBlock);
        propBlock.SetColor("_EmissionColor", Color.black);
        rend.SetPropertyBlock(propBlock);
    }

    public void Interact()
    {
        brilloActivo = true;
        Debug.Log("Brillo activado con pulso en " + gameObject.name);
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

    private void Update()
    {
        if (brilloActivo && rend != null)
        {
            float intensidad = Mathf.PingPong(Time.time * pulsoVelocidad, pulsoIntensidadMax);
            Color pulsoColor = brilloColor * intensidad;

            rend.GetPropertyBlock(propBlock);
            propBlock.SetColor("_EmissionColor", pulsoColor);
            rend.SetPropertyBlock(propBlock);
        }
    }

    private void LateUpdate()
    {
        if (lookAtCamera && promptCanvas != null && promptCanvas.gameObject.activeSelf)
        {
            promptCanvas.transform.LookAt(Camera.main.transform);
            promptCanvas.transform.Rotate(0, 180, 0);
        }
    }
}
