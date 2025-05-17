using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(Collider))]
public class TabletInteraction : MonoBehaviour
{
    [Header("Prompt UI")]
    [SerializeField] private GameObject promptUI;

    [Header("Video Screen Object")]
    [Tooltip("GameObject que contiene el quad de vídeo y el VideoPlayer")]
    [SerializeField] private GameObject videoScreenObject;

    [Header("Video Player")]
    [SerializeField] private VideoPlayer videoPlayer;

    [Header("Carousel Images")]
    [Tooltip("Imágenes a mostrar tras el vídeo")]
    [SerializeField] private Texture2D[] imageTextures;

    [Header("Screen Renderer")]
    [Tooltip("Renderer del objeto pantalla que usa material con mainTexture")]
    [SerializeField] private Renderer screenRenderer;

    [Header("Navigation Buttons UI")]
    [Tooltip("Panel que agrupa los botones")]
    [SerializeField] private GameObject navButtonPanel;
    [SerializeField] private Button prevButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button closeButton;

    [Header("Tablet Animation")]
    [Tooltip("El Transform de la tablet sobre la mesa")]
    [SerializeField] private Transform tabletTransform;
    [Tooltip("Dónde debe quedar la tablet cuando se levanta")]
    [SerializeField] private Transform tabletLiftedPoint;
    [SerializeField] private float tabletTransitionDuration = 0.5f;

    [Header("Camera Settings")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform viewPoint;
    [SerializeField] private float transitionDuration = 1f;

    [Header("Hide On View")]
    [SerializeField] private GameObject characterRoot;
    [SerializeField] private GameObject playerUI;

    private bool playerInRange;
    private bool inViewMode;
    private Vector3 origCamPos;
    private Quaternion origCamRot;
    private Vector3 origTabletPos;
    private Quaternion origTabletRot;
    private Coroutine transitionCoroutine;
    private Coroutine tabletRoutine;

    // índice 0 = vídeo; 1..N = imageTextures[0..N-1]
    private int currentIndex = 0;
    private int totalItems => 1 + (imageTextures?.Length ?? 0);

    void Start()
    {
        promptUI.SetActive(false);
        navButtonPanel.SetActive(false);

        // Inicialmente solo el quad de vídeo
        screenRenderer.gameObject.SetActive(false);
        videoScreenObject.SetActive(true);

        videoPlayer.playOnAwake = false;

        // Guardamos posición/orientación original de la tablet
        origTabletPos = tabletTransform.position;
        origTabletRot = tabletTransform.rotation;

        // Asignar listeners
        prevButton.onClick.AddListener(OnPrevClicked);
        nextButton.onClick.AddListener(OnNextClicked);
        closeButton.onClick.AddListener(OnCloseClicked);

        // Cursor oculto al inicio
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!inViewMode && other.CompareTag("Player"))
        {
            playerInRange = true;
            promptUI.SetActive(true);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (!inViewMode && other.CompareTag("Player"))
        {
            playerInRange = false;
            promptUI.SetActive(false);
        }
    }

    void Update()
    {
        if (!playerInRange && !inViewMode) return;

        // Entrar en modo vista
        if (!inViewMode && Input.GetKeyDown(KeyCode.E))
        {
            EnterViewMode();
            return;
        }

        if (inViewMode)
        {
            // Navegación por teclado
            if (Input.GetKeyDown(KeyCode.E)) OnNextClicked();
            if (Input.GetKeyDown(KeyCode.F)) OnPrevClicked();
            if (Input.GetKeyDown(KeyCode.Escape)) OnCloseClicked();
        }
    }

    private void EnterViewMode()
    {
        inViewMode = true;
        promptUI.SetActive(false);
        navButtonPanel.SetActive(true);

        // Guarda cámara original
        origCamPos = playerCamera.transform.position;
        origCamRot = playerCamera.transform.rotation;

        // Oculta personaje y HUD
        SetCharacterRenderers(false);
        playerUI?.SetActive(false);

        // Desactiva control de jugador
        var ctrl = playerCamera.GetComponentInParent<MonoBehaviour>();
        if (ctrl) ctrl.enabled = false;

        // Muestra cursor
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // Animamos tablet al levantarse
        if (tabletRoutine != null) StopCoroutine(tabletRoutine);
        tabletRoutine = StartCoroutine(AnimateTransform(
            tabletTransform,
            origTabletPos, tabletLiftedPoint.position,
            origTabletRot, tabletLiftedPoint.rotation,
            tabletTransitionDuration,
            null
        ));

        // Mueve cámara y al completar, resetea índice y muestra el medio
        StartTransition(origCamPos, viewPoint.position, origCamRot, viewPoint.rotation, () =>
        {
            currentIndex = 0;
            ShowCurrentMedia();
        });
    }

    private void ExitViewMode()
    {
        videoPlayer.Stop();
        inViewMode = false;
        navButtonPanel.SetActive(false);

        // Restaurar quad vídeo/imágenes
        videoScreenObject.SetActive(true);
        screenRenderer.gameObject.SetActive(false);

        // Restaurar personaje y HUD
        SetCharacterRenderers(true);
        playerUI?.SetActive(true);

        // Reactiva control de jugador
        var ctrl = playerCamera.GetComponentInParent<MonoBehaviour>();
        if (ctrl) ctrl.enabled = true;

        // Oculta cursor
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        // Animamos tablet de vuelta a la mesa
        if (tabletRoutine != null) StopCoroutine(tabletRoutine);
        tabletRoutine = StartCoroutine(AnimateTransform(
            tabletTransform,
            tabletTransform.position, origTabletPos,
            tabletTransform.rotation, origTabletRot,
            tabletTransitionDuration,
            null
        ));

        // Mueve cámara de vuelta
        StartTransition(playerCamera.transform.position, origCamPos, playerCamera.transform.rotation, origCamRot, () =>
        {
            playerInRange = false;
            promptUI.SetActive(false);
        });
    }

    private void ShowCurrentMedia()
    {
        if (currentIndex == 0)
        {
            // === VÍDEO ===
            videoScreenObject.SetActive(true);
            screenRenderer.gameObject.SetActive(false);

            videoPlayer.enabled = true;
            videoPlayer.Stop();
            videoPlayer.Play();
        }
        else
        {
            // === IMÁGENES ===
            videoPlayer.Stop();
            videoPlayer.enabled = false;

            videoScreenObject.SetActive(false);
            screenRenderer.gameObject.SetActive(true);

            screenRenderer.material.mainTexture = imageTextures[currentIndex - 1];
        }
    }

    private void OnNextClicked()
    {
        if (currentIndex < totalItems - 1)
        {
            currentIndex++;
            ShowCurrentMedia();
        }
    }

    private void OnPrevClicked()
    {
        if (currentIndex > 0)
        {
            currentIndex--;
            ShowCurrentMedia();
        }
    }

    private void OnCloseClicked()
    {
        ExitViewMode();
    }

    private void StartTransition(Vector3 fromPos, Vector3 toPos, Quaternion fromRot, Quaternion toRot, System.Action onComplete)
    {
        if (transitionCoroutine != null) StopCoroutine(transitionCoroutine);
        transitionCoroutine = StartCoroutine(Transition(fromPos, toPos, fromRot, toRot, onComplete));
    }

    private IEnumerator Transition(Vector3 aPos, Vector3 bPos, Quaternion aRot, Quaternion bRot, System.Action onDone)
    {
        float elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            float t = elapsed / transitionDuration;
            playerCamera.transform.position = Vector3.Lerp(aPos, bPos, t);
            playerCamera.transform.rotation = Quaternion.Slerp(aRot, bRot, t);
            elapsed += Time.deltaTime;
            yield return null;
        }
        playerCamera.transform.position = bPos;
        playerCamera.transform.rotation = bRot;
        onDone?.Invoke();
    }

    // Corrutina genérica para animar posición+rotación
    private IEnumerator AnimateTransform(
        Transform t,
        Vector3 startPos, Vector3 endPos,
        Quaternion startRot, Quaternion endRot,
        float duration,
        System.Action onComplete)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float f = elapsed / duration;
            t.position = Vector3.Lerp(startPos, endPos, f);
            t.rotation = Quaternion.Slerp(startRot, endRot, f);
            elapsed += Time.deltaTime;
            yield return null;
        }
        t.position = endPos;
        t.rotation = endRot;
        onComplete?.Invoke();
    }

    private void SetCharacterRenderers(bool on)
    {
        if (characterRoot == null) return;
        foreach (var rend in characterRoot.GetComponentsInChildren<Renderer>())
            rend.enabled = on;
    }
}
