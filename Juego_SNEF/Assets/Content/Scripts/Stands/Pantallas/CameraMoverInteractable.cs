using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider))]
public class CameraMoverInteractable : MonoBehaviour
{
    [Header("Prompt UI")]
    [Tooltip("UI que aparece al acercarte")]
    public GameObject promptUI;
    [Tooltip("¿Debe mirar al jugador?")]
    public bool lookAtCamera = true;

    [Header("Camera Settings")]
    [Tooltip("La cámara principal del jugador")]
    public Camera playerCamera;
    [Tooltip("Empty que marca la posición/rotación de vista frontal")]
    public Transform viewPoint;
    [Tooltip("Duración de la transición de cámara (s)")]
    public float transitionDuration = 1f;

    [Header("Hide On View")]
    [Tooltip("Root del personaje para ocultar durante la vista")]
    public GameObject characterRoot;
    [Tooltip("HUD del jugador para ocultar")]
    public GameObject playerUI;

    bool playerInRange = false;
    bool inViewMode = false;
    Vector3 origCamPos;
    Quaternion origCamRot;
    Coroutine transitionCoroutine;

    void Start()
    {
        // Asegúrate de que el Collider no sea trigger
        if (promptUI) promptUI.SetActive(false);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!inViewMode && other.CompareTag("Player"))
        {
            playerInRange = true;
            promptUI?.SetActive(true);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (!inViewMode && other.CompareTag("Player"))
        {
            playerInRange = false;
            promptUI?.SetActive(false);
        }
    }

    void Update()
    {
        // Si estamos en rango y no en modo vista, E entra
        if (playerInRange && !inViewMode && Input.GetKeyDown(KeyCode.E))
        {
            EnterViewMode();
        }
        // Si ya estamos en vista, Escape sale
        else if (inViewMode && Input.GetKeyDown(KeyCode.Escape))
        {
            ExitViewMode();
        }
    }

    void EnterViewMode()
    {
        inViewMode = true;
        promptUI?.SetActive(false);

        // Guarda cámara y ocultar jugador/UI
        origCamPos = playerCamera.transform.position;
        origCamRot = playerCamera.transform.rotation;
        characterRoot?.SetActive(false);
        playerUI?.SetActive(false);

        // Desactiva control de jugador si existe
        var ctrl = playerCamera.GetComponentInParent<MonoBehaviour>();
        if (ctrl) ctrl.enabled = false;

        // Muestra cursor
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // Inicia transición
        StartTransition(
            origCamPos, viewPoint.position,
            origCamRot, viewPoint.rotation,
            null
        );
    }

    void ExitViewMode()
    {
        inViewMode = false;
        playerInRange = false;
        promptUI?.SetActive(false);

        // Si hay transición en curso, la detenemos
        if (transitionCoroutine != null)
            StopCoroutine(transitionCoroutine);

        StartTransition(
            playerCamera.transform.position, origCamPos,
            playerCamera.transform.rotation, origCamRot,
            () =>
            {
                // Restaura jugador/UI
                characterRoot?.SetActive(true);
                playerUI?.SetActive(true);

                // Reactiva control de jugador
                var ctrl = playerCamera.GetComponentInParent<MonoBehaviour>();
                if (ctrl) ctrl.enabled = true;

                // Oculta cursor
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
            }
        );
    }

    void StartTransition(
        Vector3 fromPos, Vector3 toPos,
        Quaternion fromRot, Quaternion toRot,
        System.Action onComplete)
    {
        if (transitionCoroutine != null)
            StopCoroutine(transitionCoroutine);

        transitionCoroutine = StartCoroutine(
            Transition(fromPos, toPos, fromRot, toRot, onComplete)
        );
    }

    IEnumerator Transition(
        Vector3 aPos, Vector3 bPos,
        Quaternion aRot, Quaternion bRot,
        System.Action onDone)
    {
        float elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / transitionDuration);
            playerCamera.transform.position = Vector3.Lerp(aPos, bPos, t);
            playerCamera.transform.rotation = Quaternion.Slerp(aRot, bRot, t);
            elapsed += Time.deltaTime;
            yield return null;
        }
        playerCamera.transform.SetPositionAndRotation(bPos, bRot);
        onDone?.Invoke();
    }

    void LateUpdate()
    {
        if (lookAtCamera && promptUI != null && promptUI.activeSelf)
        {
            var cam = playerCamera.transform;
            promptUI.transform.LookAt(cam);
            promptUI.transform.Rotate(0, 180, 0);
        }
    }
}
