using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using System.Collections;

[RequireComponent(typeof(Collider), typeof(Renderer))]
public class UnifiedScreenDisplay : MonoBehaviour
{
    public enum ContentType { Image, Video, Presentation }

    [Header("Prompt UI")]
    public GameObject promptUI;
    public bool lookAtCamera = true;

    [Header("Contenido")]
    public ContentType contentType;
    public Texture2D singleImage;
    public string videoURL;
    public VideoClip videoClip;
    public VideoPlayer videoPlayer;
    public Texture2D[] slides;

    [Header("Idle Image")]
    [Tooltip("Imagen estática que se muestra cuando no se está interactuando")]
    public Texture2D idleImage;

    [Header("Quads")]
    [Tooltip("GameObject que contiene el quad de vídeo")]
    public GameObject videoQuad;
    [Tooltip("Renderer del quad de imágenes/presentación")]
    public Renderer imageRenderer;

    [Header("Botones de navegación")]
    [Tooltip("Panel que agrupa Prev y Next")]
    public GameObject navButtonPanel;
    public Button prevButton;
    public Button nextButton;
    [Tooltip("Botón Cerrar (siempre visible en modo vista)")]
    public Button closeButton;

    [Header("Cámara y jugador")]
    public GameObject playerRoot;
    public GameObject playerUI;
    public Camera mainCamera;
    public Transform screenViewpoint;
    public float transitionDuration = 0.8f;

    [Header("Cursor")]
    public bool unlockCursorDuringView = true;

    [Header("Configuración del brillo")]
    public Color brilloColor = Color.cyan;
    public float pulsoVelocidad = 2f;
    public float pulsoIntensidadMax = 2f;

    // Estado interno
    private bool _isViewing;
    private Vector3 _camOrigPos;
    private Quaternion _camOrigRot;
    private int _currentIndex;
    private int _totalSlides;
    private Coroutine _transitionRoutine;
    private MaterialPropertyBlock _imageProp;
    private RenderTexture _videoRT;

    // Para el brillo
    private MaterialPropertyBlock _brilloProp;
    private Renderer _brilloRend;
    private bool _brilloActivo = true;

    void Awake()
    {
        // UI inicial
        promptUI?.SetActive(false);
        navButtonPanel?.SetActive(false);
        closeButton?.gameObject.SetActive(false);

        // Prepara brillo
        _brilloRend = GetComponent<Renderer>();
        _brilloProp = new MaterialPropertyBlock();
        _brilloRend.GetPropertyBlock(_brilloProp);
        _brilloProp.SetColor("_EmissionColor", Color.black);
        _brilloRend.material.EnableKeyword("_EMISSION");
        _brilloRend.SetPropertyBlock(_brilloProp);

        // Prepara imagen
        _imageProp = new MaterialPropertyBlock();
        imageRenderer.GetPropertyBlock(_imageProp);

        // Prepara vídeo
        if (videoPlayer != null)
        {
            _videoRT = new RenderTexture(1280, 720, 0);
            videoPlayer.targetTexture = _videoRT;
        }

        _totalSlides = slides?.Length ?? 0;

        // Listeners de navegación
        prevButton?.onClick.AddListener(() => ShowPresentationItem(_currentIndex - 1));
        nextButton?.onClick.AddListener(() => ShowPresentationItem(_currentIndex + 1));
        closeButton?.onClick.AddListener(ExitViewMode);

        // Muestra la imagen idle al empezar
        videoQuad?.SetActive(false);
        imageRenderer.gameObject.SetActive(true);
        ShowIdleImage();
    }

    void Update()
    {
        // Pulso de brillo
        if (_brilloActivo)
        {
            float intensity = Mathf.PingPong(Time.time * pulsoVelocidad, pulsoIntensidadMax);
            Color pulseColor = brilloColor * intensity;
            _brilloRend.GetPropertyBlock(_brilloProp);
            _brilloProp.SetColor("_EmissionColor", pulseColor);
            _brilloRend.SetPropertyBlock(_brilloProp);
            DynamicGI.SetEmissive(_brilloRend, pulseColor);
        }

        // Entrada de usuario
        if (!_isViewing && promptUI.activeSelf && Input.GetKeyDown(KeyCode.E))
            EnterViewMode();
        else if (_isViewing && Input.GetKeyDown(KeyCode.Escape))
            ExitViewMode();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!_isViewing && other.CompareTag("Player"))
            promptUI?.SetActive(true);
    }

    void OnTriggerExit(Collider other)
    {
        if (!_isViewing && other.CompareTag("Player"))
            promptUI?.SetActive(false);
    }

    void EnterViewMode()
    {
        _isViewing = true;
        promptUI?.SetActive(false);

        // Desactiva brillo
        _brilloActivo = false;
        _brilloProp.SetColor("_EmissionColor", Color.black);
        _brilloRend.SetPropertyBlock(_brilloProp);

        // Guarda cámara y oculta jugador/UI
        _camOrigPos = mainCamera.transform.position;
        _camOrigRot = mainCamera.transform.rotation;
        playerRoot?.SetActive(false);
        playerUI?.SetActive(false);

        // Desactiva control de jugador
        var ctrl = mainCamera.GetComponentInParent<MonoBehaviour>();
        if (ctrl) ctrl.enabled = false;

        // Muestra cursor
        if (unlockCursorDuringView)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        // Transición a pantalla
        StartOrRestartTransition(
            _camOrigPos, screenViewpoint.position,
            _camOrigRot, screenViewpoint.rotation,
            OnEnteredViewMode
        );
    }

    void OnEnteredViewMode()
    {
        navButtonPanel?.SetActive(true);
        prevButton?.gameObject.SetActive(contentType == ContentType.Presentation);
        nextButton?.gameObject.SetActive(contentType == ContentType.Presentation);
        closeButton?.gameObject.SetActive(true);

        switch (contentType)
        {
            case ContentType.Image:
                ShowImage(singleImage);
                break;
            case ContentType.Video:
                ShowVideo();
                break;
            case ContentType.Presentation:
                _currentIndex = 0;
                ShowPresentationItem(0);
                break;
        }
    }

    void ExitViewMode()
    {
        _isViewing = false;
        promptUI?.SetActive(false);

        if (_transitionRoutine != null)
            StopCoroutine(_transitionRoutine);

        videoPlayer?.Stop();

        navButtonPanel?.SetActive(false);
        closeButton?.gameObject.SetActive(false);

        StartOrRestartTransition(
            mainCamera.transform.position, _camOrigPos,
            mainCamera.transform.rotation, _camOrigRot,
            OnExitedViewMode
        );
    }

    void OnExitedViewMode()
    {
        playerRoot?.SetActive(true);
        playerUI?.SetActive(true);

        var ctrl = mainCamera.GetComponentInParent<MonoBehaviour>();
        if (ctrl) ctrl.enabled = true;

        if (unlockCursorDuringView)
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        // Restaurar imagen idle
        videoQuad?.SetActive(false);
        imageRenderer.gameObject.SetActive(true);
        ShowIdleImage();
    }

    private void ShowIdleImage()
    {
        Texture2D tex = idleImage != null
            ? idleImage
            : contentType == ContentType.Presentation && _totalSlides > 0
                ? slides[0]
                : singleImage;
        ShowImage(tex);
    }

    void ShowImage(Texture2D tex)
    {
        videoQuad?.SetActive(false);
        imageRenderer.gameObject.SetActive(true);
        _imageProp.Clear();
        _imageProp.SetTexture("_MainTex", tex);
        imageRenderer.SetPropertyBlock(_imageProp);
    }

    void ShowVideo()
    {
        imageRenderer.gameObject.SetActive(false);
        videoQuad?.SetActive(true);

        var vr = videoQuad.GetComponent<Renderer>();
        if (vr != null)
        {
            var mb = new MaterialPropertyBlock();
            vr.GetPropertyBlock(mb);
            mb.SetTexture("_MainTex", _videoRT);
            vr.SetPropertyBlock(mb);
        }

        videoPlayer.source = !string.IsNullOrEmpty(videoURL)
            ? VideoSource.Url
            : VideoSource.VideoClip;
        videoPlayer.url = videoURL;
        videoPlayer.clip = videoClip;
        videoPlayer.Play();
    }

    void ShowPresentationItem(int idx)
    {
        if (!_isViewing || contentType != ContentType.Presentation) return;

        idx = Mathf.Clamp(idx, 0, _totalSlides - 1);
        _currentIndex = idx;
        ShowImage(slides[idx]);

        prevButton.interactable = idx > 0;
        nextButton.interactable = idx < _totalSlides - 1;
    }

    void StartOrRestartTransition(
        Vector3 fromPos, Vector3 toPos,
        Quaternion fromRot, Quaternion toRot,
        System.Action onComplete)
    {
        if (_transitionRoutine != null)
            StopCoroutine(_transitionRoutine);
        _transitionRoutine = StartCoroutine(
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
            mainCamera.transform.position = Vector3.Lerp(aPos, bPos, t);
            mainCamera.transform.rotation = Quaternion.Slerp(aRot, bRot, t);
            elapsed += Time.deltaTime;
            yield return null;
        }
        mainCamera.transform.SetPositionAndRotation(bPos, bRot);
        onDone?.Invoke();
    }

    void LateUpdate()
    {
        if (lookAtCamera && promptUI != null && promptUI.activeSelf)
        {
            var camT = mainCamera.transform;
            promptUI.transform.LookAt(camT);
            promptUI.transform.Rotate(0, 180, 0);
        }
    }
}
