using UnityEngine;
using UnityEngine.UI;

public class OnboardingSlider : MonoBehaviour
{
    [Header("Slides")]
    [Tooltip("Imágenes que se mostrarán en el slider, en orden.")]
    public Sprite[] slides;

    [Header("UI Elements")]
    [Tooltip("Imagen donde se mostrará cada slide.")]
    public Image slideImage;
    [Tooltip("Botón para pasar a la siguiente slide.")]
    public Button nextButton;
    [Tooltip("Botón para retroceder a la anterior slide.")]
    public Button prevButton;
    [Tooltip("Botón para omitir (cerrar el onboarding).")]
    public Button skipButton;
    [Tooltip("Botón para finalizar (solo visible en la última slide).")]
    public Button finishButton;

    [Header("Animación (desactivada)")]
    [Tooltip("CanvasGroup usado antes para el fade; ahora solo se asegura alpha=1.")]
    public CanvasGroup slideGroup;
    [Tooltip("Duración de transición (sin uso).")]
    public float transitionDuration = 0.3f;

    private int _currentIndex = 0;

    void Start()
    {
        if (slides == null || slides.Length == 0)
        {
            Debug.LogWarning("No hay slides asignadas al OnboardingSlider.");
            return;
        }

        // Listeners (idénticos al script anterior)
        if (nextButton) nextButton.onClick.AddListener(NextSlide);
        if (prevButton) prevButton.onClick.AddListener(PrevSlide);
        if (skipButton) skipButton.onClick.AddListener(CloseOnboarding);
        if (finishButton) finishButton.onClick.AddListener(CloseOnboarding);

        // Asegura que el grupo está visible (sin fade)
        if (slideGroup) slideGroup.alpha = 1f;

        ShowSlide(_currentIndex);
        UpdateButtons();
    }

    void ShowSlide(int index)
    {
        if (slideImage == null) return;

        _currentIndex = Mathf.Clamp(index, 0, slides.Length - 1);

        // Cambio inmediato de imagen (sin animación)
        slideImage.sprite = slides[_currentIndex];

        // Si usabas AspectRatioFitter/Layout, no tocamos SetNativeSize.
        // (Se mantiene el layout tal cual lo tenías.)
        UpdateButtons();
    }

    void NextSlide()
    {
        if (_currentIndex < slides.Length - 1)
            ShowSlide(_currentIndex + 1);
    }

    void PrevSlide()
    {
        if (_currentIndex > 0)
            ShowSlide(_currentIndex - 1);
    }

    void UpdateButtons()
    {
        bool isFirst = _currentIndex == 0;
        bool isLast = _currentIndex == slides.Length - 1;

        if (prevButton) prevButton.gameObject.SetActive(!isFirst);
        if (nextButton) nextButton.gameObject.SetActive(!isLast);
        if (finishButton) finishButton.gameObject.SetActive(isLast);
        // Omitir siempre visible: no se toca.
    }

    void CloseOnboarding()
    {
        // Cierra el panel/canvas del onboarding (idéntico a antes)
        gameObject.SetActive(false);

        // Si en tu flujo pausas el juego/cursor fuera de aquí, no lo modificamos.
        // (Descomenta si lo necesitas en este panel)
        // Time.timeScale = 1f;
        // Cursor.visible = false;
        // Cursor.lockState = CursorLockMode.Locked;
    }
}
