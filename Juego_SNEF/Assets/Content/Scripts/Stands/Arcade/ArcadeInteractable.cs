using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

public class ArcadeInteractable : MonoBehaviour,
    Interactor.IInteractable,
    Interactor.IInteractableFeedback

{

    [Header("Stand (Progress)")]
    public string standId;
    public string standType = "master";

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

    [Header("Outline (QuickOutline)")]
    [Tooltip("Componente Outline (si no lo asignas, se auto-busca en este objeto o hijos)")]
    public Outline outline;
    [Tooltip("Encender outline al enfocar con la mirada")]
    public bool enableOutlineOnProximity = true;
    public Outline.Mode outlineModeNear = Outline.Mode.OutlineVisible;
    public Color outlineColorNear = Color.cyan;
    [Range(0, 10f)] public float outlineWidthNear = 4f;

    // Estado interno
    private bool _isLocked = true;
    private Coroutine _hideMsgRoutine;

    void Start()
    {
        // Aplicar estado inicial según el toggle del inspector
        SetLocked(!startUnlocked);
        // Si ya estaba desbloqueada en progreso, reflejarlo
        if (ProgressCore.I != null && ProgressCore.I.Stand_IsArcadeUnlocked(standId))
            SetLocked(false);


        // Ocultar prompt y mensaje
        if (promptUI) promptUI.SetActive(false);
        if (lockMessageText) lockMessageText.gameObject.SetActive(false);

        // Outline: auto-descubrir si no está asignado y apagarlo
        if (outline == null) outline = GetComponent<Outline>() ?? GetComponentInChildren<Outline>();
        if (outline != null) outline.enabled = false;
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
        ProgressCore.I?.Stand_UnlockArcade(standId);
        ProgressRemote.I.UpdateStand(standId, standType, phase: "PostScreens", screensViewed: null, quizUnlocked: true);

    }

    // Atajo útil para probar desde el editor (clic derecho en el componente)
    [ContextMenu("Forzar Desbloqueo (Editor)")]
    private void ContextUnlock() => SetLocked(false);

    // ============================
    //   Interactor Feedback
    // ============================
    public void OnGazeEnter()
    {
        // Prompt on
        if (promptUI) promptUI.SetActive(true);

        // Outline on
        if (enableOutlineOnProximity && outline)
        {
            ApplyOutlineSettings();
            outline.enabled = true;
        }
    }

    public void OnGazeExit()
    {
        // Prompt off
        if (promptUI) promptUI.SetActive(false);

        // Ocultar mensaje de bloqueo si estaba mostrándose
        if (lockMessageText) lockMessageText.gameObject.SetActive(false);
        if (_hideMsgRoutine != null) StopCoroutine(_hideMsgRoutine);

        // Outline off
        if (outline) outline.enabled = false;
    }

    // ============================
    //       Interactor Core
    // ============================
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
        if (!Camera.main) return;

        var cam = Camera.main.transform;

        if (lookAtCamera && promptUI != null && promptUI.activeSelf)
        {
            promptUI.transform.LookAt(cam);
            promptUI.transform.Rotate(0, 180, 0);
        }

        // El candado (si es world-space) también mira a cámara
        if (lookAtCamera && lockCanvas != null && lockCanvas.gameObject.activeSelf)
        {
            var t = lockCanvas.transform;
            t.LookAt(cam);
            t.Rotate(0, 180, 0);
        }
    }

    // ============================
    //          Helpers
    // ============================
    private void ApplyOutlineSettings()
    {
        if (!outline) return;
        outline.OutlineMode = outlineModeNear;
        outline.OutlineColor = outlineColorNear;
        outline.OutlineWidth = outlineWidthNear;
    }
}
