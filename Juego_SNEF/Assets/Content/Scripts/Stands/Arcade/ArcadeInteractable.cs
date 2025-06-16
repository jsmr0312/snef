using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

public class ArcadeInteractable : MonoBehaviour,
    Interactor.IInteractable,
    Interactor.IInteractableFeedback
{
    [Header("Prompt UI")]
    [Tooltip("GameObject que indica “Presiona E”")]
    public GameObject promptUI;
    [Tooltip("¿Debe el prompt mirar a la cámara?")]
    public bool lookAtCamera = true;

    [Header("Lock UI")]
    [Tooltip("Canvas con el icono de candado (se muestra hasta desbloquear)")]
    public Canvas lockCanvas;
    [Tooltip("Texto que aparece al intentar interactuar bloqueado")]
    public TextMeshProUGUI lockMessageText;
    [Tooltip("Duración en segundos del mensaje de bloqueo")]
    public float lockMessageDuration = 2f;

    [Header("Arcade Settings")]
    [Tooltip("Nombre de la escena o ruta del mini-juego")]
    public string arcadeSceneName;

    // Estado interno
    private bool _isLocked = true;
    private Coroutine _hideMsgRoutine;

    void Start()
    {
        // Mostrar candado al inicio
        if (lockCanvas) lockCanvas.gameObject.SetActive(true);
        // Ocultar prompt y mensaje
        if (promptUI) promptUI.SetActive(false);
        if (lockMessageText) lockMessageText.gameObject.SetActive(false);
    }

    /// <summary>
    /// Llamar desde QuizManager.EndQuiz() para desbloquear la arcade
    /// </summary>
    public void UnlockArcade()
    {
        _isLocked = false;
        if (lockCanvas) lockCanvas.gameObject.SetActive(false);
    }

    // IInteractableFeedback
    public void OnGazeEnter()
    {
        // Siempre mostramos el prompt al mirar la arcade
        if (promptUI)
            promptUI.SetActive(true);
    }

    public void OnGazeExit()
    {
        // Ocultamos todo al alejarnos
        if (promptUI)
            promptUI.SetActive(false);
        if (lockMessageText)
            lockMessageText.gameObject.SetActive(false);
        if (_hideMsgRoutine != null)
            StopCoroutine(_hideMsgRoutine);
    }

    // IInteractable
    public void Interact()
    {
        if (_isLocked)
        {
            ShowLockMessage();
            return;
        }

        // Arcade desbloqueada: cargamos la escena
        if (!string.IsNullOrEmpty(arcadeSceneName))
            SceneManager.LoadScene(arcadeSceneName);
        else
            Debug.LogWarning("ArcadeInteractable: arcadeSceneName no asignado.");
    }

    private void ShowLockMessage()
    {
        if (lockMessageText == null) return;

        lockMessageText.text = "Primero termina de hablar con el NPC";
        lockMessageText.gameObject.SetActive(true);

        if (_hideMsgRoutine != null) StopCoroutine(_hideMsgRoutine);
        _hideMsgRoutine = StartCoroutine(HideLockMessageAfterDelay());
    }

    private IEnumerator HideLockMessageAfterDelay()
    {
        yield return new WaitForSeconds(lockMessageDuration);
        if (lockMessageText)
            lockMessageText.gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        if (lookAtCamera && promptUI != null && promptUI.activeSelf && Camera.main != null)
        {
            var cam = Camera.main.transform;
            promptUI.transform.LookAt(cam);
            promptUI.transform.Rotate(0, 180, 0);
        }
    }
}
