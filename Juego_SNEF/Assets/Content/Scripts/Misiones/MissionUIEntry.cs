using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MissionUIEntry : MonoBehaviour
{
    [Header("Referencias UI")]
    public MissionManager.MissionType missionType;
    public TextMeshProUGUI descriptionText;
    public TextMeshProUGUI progressText;
    public Image backgroundImage;    // Asigna aquí el Image de fondo del entry
    public Image checkmarkImage;     // Asigna aquí el Image del ✓

    [Header("Colores")]
    public Color normalColor = Color.white;
    public Color completedColor = Color.green;

    [Header("Animación del ✓")]
    [Tooltip("Duración de la animación")]
    public float animDuration = 0.5f;
    [Tooltip("Escala máxima durante la animación")]
    public float maxScale = 1.3f;
    [Tooltip("Grados de rotación totales")]
    public float rotationAngle = 360f;

    bool _wasCompleted;

    void Awake()
    {
        // Al arrancar, ocultamos la palomita y establecemos el color normal
        if (checkmarkImage != null)
        {
            checkmarkImage.gameObject.SetActive(false);
        }
        if (backgroundImage != null)
        {
            backgroundImage.color = normalColor;
        }
    }

    /// <summary>
    /// Actualiza descripción, progreso y estado de completado.
    /// </summary>
    public void Refresh((int actual, int objetivo) prog, bool completed)
    {
        // 1) Descripción según tipo
        switch (missionType)
        {
            case MissionManager.MissionType.VisitStand:
                descriptionText.text = "Visita stands";
                break;
            case MissionManager.MissionType.CompleteQuiz:
                descriptionText.text = "Completa el quiz";
                break;
            default:
                descriptionText.text = missionType.ToString();
                break;
        }

        // 2) Progreso
        progressText.text = $"{prog.actual}/{prog.objetivo}";

        // 3) Color de fondo
        if (backgroundImage != null)
            backgroundImage.color = completed ? completedColor : normalColor;

        // 4) Palomita y animación
        if (completed)
        {
            if (checkmarkImage != null && !_wasCompleted)
            {
                checkmarkImage.gameObject.SetActive(true);
                _wasCompleted = true;
                StartCoroutine(AnimateCheck());
            }
        }
        else
        {
            if (checkmarkImage != null)
                checkmarkImage.gameObject.SetActive(false);
            _wasCompleted = false;
        }
    }

    IEnumerator AnimateCheck()
    {
        var rt = checkmarkImage.rectTransform;
        rt.localScale = Vector3.zero;
        rt.localRotation = Quaternion.identity;

        float elapsed = 0f;
        while (elapsed < animDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / animDuration);
            // escala tipo “pop” con seno
            float s = Mathf.LerpUnclamped(0, maxScale, Mathf.Sin(t * Mathf.PI));
            rt.localScale = Vector3.one * s;
            // rotación suave
            rt.localRotation = Quaternion.Euler(0, 0, t * rotationAngle);
            yield return null;
        }

        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
    }
}
