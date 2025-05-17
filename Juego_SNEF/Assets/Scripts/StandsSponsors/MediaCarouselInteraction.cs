using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(Collider))]
public class MediaCarouselInteraction : MonoBehaviour
{
    [Header("Prompt UI")]
    [SerializeField] private GameObject promptUI;

    [Header("Options")]
    [Tooltip("Si está en false, solo se mostrarán imágenes (no vídeo)")]
    [SerializeField] private bool useVideo = true;
    [Tooltip("Si está en false, no se mostrarán los botones Prev/Next")]
    [SerializeField] private bool usePrevNextButtons = true;
    [Tooltip("Tablet Animation opcional")]
    [SerializeField] private bool useTabletAnimation = false;

    [Header("Video Screen Object")]
    [SerializeField] private GameObject videoScreenObject;
    [SerializeField] private VideoPlayer videoPlayer;

    [Header("Carousel Images")]
    [SerializeField] private Texture2D[] imageTextures;
    [SerializeField] private Renderer screenRenderer;

    [Header("Navigation Buttons UI")]
    [SerializeField] private GameObject navButtonPanel;
    [SerializeField] private Button prevButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button closeButton;

    [Header("Tablet Animation")]
    [SerializeField] private Transform tabletTransform;
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
    private int currentIndex = 0;
    private int totalItems => (useVideo ? 1 : 0) + (imageTextures?.Length ?? 0);

    private Vector3 origCamPos;
    private Quaternion origCamRot;
    private Vector3 origTabletPos;
    private Quaternion origTabletRot;
    private Coroutine transitionCoroutine;
    private Coroutine tabletRoutine;

    void Start()
    {
        promptUI.SetActive(false);
        navButtonPanel.SetActive(false);

        // Guardar estado tablet si aplica
        if (useTabletAnimation && tabletTransform != null)
        {
            origTabletPos = tabletTransform.position;
            origTabletRot = tabletTransform.rotation;
        }

        // Configuración inicial de pantalla
        if (useVideo)
        {
            videoScreenObject.SetActive(true);
            screenRenderer.gameObject.SetActive(false);
            videoPlayer.playOnAwake = false;
        }
        else
        {
            videoScreenObject.SetActive(false);
            screenRenderer.gameObject.SetActive(true);
            if (imageTextures.Length > 0)
                screenRenderer.material.mainTexture = imageTextures[0];
        }

        // Listeners
        prevButton.onClick.AddListener(OnPrevClicked);
        nextButton.onClick.AddListener(OnNextClicked);
        closeButton.onClick.AddListener(OnCloseClicked);

        // Cursor oculto
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

        if (!inViewMode && Input.GetKeyDown(KeyCode.E))
        {
            EnterViewMode();
            return;
        }

        if (inViewMode)
        {
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

        // Ajustar visibilidad Prev/Next
        prevButton.gameObject.SetActive(usePrevNextButtons);
        nextButton.gameObject.SetActive(usePrevNextButtons);
        closeButton.gameObject.SetActive(true); // siempre visible

        origCamPos = playerCamera.transform.position;
        origCamRot = playerCamera.transform.rotation;

        SetCharacterRenderers(false);
        playerUI?.SetActive(false);

        var ctrl = playerCamera.GetComponentInParent<MonoBehaviour>();
        if (ctrl) ctrl.enabled = false;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // Tablet animation opcional
        if (useTabletAnimation && tabletTransform != null && tabletLiftedPoint != null)
        {
            if (tabletRoutine != null) StopCoroutine(tabletRoutine);
            tabletRoutine = StartCoroutine(AnimateTransform(
                tabletTransform,
                origTabletPos, tabletLiftedPoint.position,
                origTabletRot, tabletLiftedPoint.rotation,
                tabletTransitionDuration, null
            ));
        }

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

        // Restaurar pantalla
        if (useVideo)
        {
            videoScreenObject.SetActive(true);
            screenRenderer.gameObject.SetActive(false);
        }
        else
        {
            videoScreenObject.SetActive(false);
            screenRenderer.gameObject.SetActive(true);
        }

        SetCharacterRenderers(true);
        playerUI?.SetActive(true);

        var ctrl = playerCamera.GetComponentInParent<MonoBehaviour>();
        if (ctrl) ctrl.enabled = true;

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        // Tablet vuelve a mesa si aplica
        if (useTabletAnimation && tabletTransform != null)
        {
            if (tabletRoutine != null) StopCoroutine(tabletRoutine);
            tabletRoutine = StartCoroutine(AnimateTransform(
                tabletTransform,
                tabletTransform.position, origTabletPos,
                tabletTransform.rotation, origTabletRot,
                tabletTransitionDuration, null
            ));
        }

        StartTransition(playerCamera.transform.position, origCamPos, playerCamera.transform.rotation, origCamRot, () =>
        {
            playerInRange = false;
            promptUI.SetActive(false);
        });
    }

    private void ShowCurrentMedia()
    {
        if (useVideo && currentIndex == 0)
        {
            videoScreenObject.SetActive(true);
            screenRenderer.gameObject.SetActive(false);

            videoPlayer.enabled = true;
            videoPlayer.Stop();
            videoPlayer.Play();
        }
        else
        {
            int imgIndex = useVideo ? currentIndex - 1 : currentIndex;
            if (imgIndex >= 0 && imgIndex < imageTextures.Length)
            {
                videoPlayer.Stop();
                videoPlayer.enabled = false;

                videoScreenObject.SetActive(false);
                screenRenderer.gameObject.SetActive(true);

                screenRenderer.material.mainTexture = imageTextures[imgIndex];
            }
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
