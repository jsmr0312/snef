using UnityEngine;
using UnityEngine.UI;

public class OnboardingOpenWithButton : MonoBehaviour
{
    [Header("Asignaciones")]
    [Tooltip("Botón que abrirá el onboarding.")]
    public Button openButton;
    [Tooltip("Raíz del canvas/panel del onboarding (donde está tu OnboardingSlider).")]
    public GameObject onboardingRoot;

    [Header("Opcional")]
    [Tooltip("Ocultar el onboarding al iniciar la escena.")]
    public bool hideOnStart = true;

    void Reset()
    {
        onboardingRoot = gameObject; // por si lo pones en el mismo objeto del canvas
    }

    void Awake()
    {
        if (openButton != null)
            openButton.onClick.AddListener(Open);

        if (hideOnStart && onboardingRoot != null)
            onboardingRoot.SetActive(false);
    }

    public void Open()
    {
        if (onboardingRoot != null)
            onboardingRoot.SetActive(true);

        // Si quieres pausar y mostrar cursor al abrir, descomenta:
        // Time.timeScale = 0f;
        // Cursor.visible = true;
        // Cursor.lockState = CursorLockMode.None;
    }

    // Útil si quieres cerrar desde fuera (o conectar al botón Omitir/Terminar)
    public void Close()
    {
        if (onboardingRoot != null)
            onboardingRoot.SetActive(false);

        // Si pausaste arriba, aquí reanudas:
        // Time.timeScale = 1f;
        // Cursor.visible = false;
        // Cursor.lockState = CursorLockMode.Locked;
    }
}
