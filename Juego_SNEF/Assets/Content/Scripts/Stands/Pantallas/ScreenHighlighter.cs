using UnityEngine;

public class ScreenHighlighter : MonoBehaviour
{
    private Material material;
    private Color originalEmissionColor;
    public Color highlightEmissionColor = Color.cyan;

    private void Start()
    {
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            material = renderer.material;
            if (material.HasProperty("_EmissionColor"))
            {
                originalEmissionColor = material.GetColor("_EmissionColor");
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black); // iniciar apagado
            }
        }
    }

    public void ActivateHighlight()
    {
        if (material != null && material.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", highlightEmissionColor);
        }
    }

    public void DeactivateHighlight()
    {
        if (material != null && material.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", originalEmissionColor);
        }
    }
}
