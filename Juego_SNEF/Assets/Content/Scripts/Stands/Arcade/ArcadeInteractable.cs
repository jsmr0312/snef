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

    [Header("Lock Settings")]
    [Tooltip("Si está activo, la arcade inicia DESBLOQUEADA")]
    public bool startUnlocked = false;

    // Estado interno
    private bool _isLocked = true;
    private Coroutine _hideMsgRoutine;

    void Start()
    {
        // Aplicar estado inicial según el toggle del inspector
        SetLocked(!startUnlocked);

        // Ocultar prompt y mensaje
        if (promptUI) promptUI.SetActive(false);
        if (lockMessageText) lockMessageText.gameObject.SetActive(false);
    }

    /// <summary>
    /// Cambia el estado de bloqueo y actualiza el UI del candado.
    /// </summary>
    private void SetLocked(bool value)
    {
        _isLocked = value;
        if (lockCanvas) lockCanvas.gameObject.SetActive(_isLocked);
    }

    /// <summary>
    /// Llamar desde QuizManager.EndQuiz() para desbloquear la arcade
    /// </summary>
    public void UnlockArcade()
    {
        SetLocked(false);
    }

    /// <summary>
    /// (Opcional) Por si quieres volver a bloquearla en runtime.
    /// </summary>
    public void LockArcade()
    {
        SetLocked(true);
    }

    // Atajo útil para probar desde el editor (clic derecho en el componente)
    [ContextMenu("Forzar Desbloqueo (Editor)")]
    private void ContextUnlock() => SetLocked(false);

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

        // Guardar la posición del jugador antes de cambiar de escena
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            Vector3 pos = player.transform.position;
            PlayerPrefs.SetFloat("SavedX", pos.x);
            PlayerPrefs.SetFloat("SavedY", pos.y);
            PlayerPrefs.SetFloat("SavedZ", pos.z);
            PlayerPrefs.SetString("ReturnTo", SceneManager.GetActiveScene().name);
            PlayerPrefs.Save();
        }

        // Arcade desbloqueada: cargamos la escena del minijuego
        if (!string.IsNullOrEmpty(arcadeSceneName))
            SceneManager.LoadScene(arcadeSceneName);
        else
            Debug.LogWarning("ArcadeInteractable: arcadeSceneName no asignado.");
    }

    private void ShowLockMessage()
    {
        if (lockMessageText == null) return;

        lockMessageText.text = "Termina de hablar con el personaje";
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
