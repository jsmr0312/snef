using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using System;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Collider), typeof(Renderer))]
public class UnifiedScreenDisplay : MonoBehaviour,
    Interactor.IInteractable,
    Interactor.IInteractableFeedback
{
    public enum ContentType { Image, Video, Presentation, TallImageScrollable }

    #region Inspector

    [Header("Prompt UI")]
    public GameObject promptUI;
    public bool lookAtCamera = true;
    public Button promptOpenButton;

    [Header("Stand (Identidad)")]
    public string standId;
    public string screenId = "screen1";
    public string standType = "master";

    [Header("Stand (Métricas)")]
    public string ecosystemName;
    [Tooltip("Si está vacío, usa screenId")]
    public string assetId;

    [Header("Descarga")]
    public Button downloadButton;
    public string downloadURL;
    public bool hideDownloadIfEmpty = true;

    [Header("Contenido")]
    public ContentType contentType;
    public Texture2D singleImage;
    public string videoURL;
    public VideoClip videoClip;
    public VideoPlayer videoPlayer;
    public Texture2D[] slides;

    [Header("Idle Image")]
    public Texture2D idleImage;

    [Header("Canvas Targets")]
    public RawImage imageRawImage;
    public RawImage videoRawImage;

    [Header("Tall Image Scroll (nuevo)")]
    public CanvasGroup tallScrollRoot;
    public ScrollRect tallScrollRect;
    public RectTransform tallViewport;
    public RawImage tallImage;
    public Slider tallSlider;
    public bool invertTallSlider = true;

    [Header("Botones de navegación")]
    public GameObject navButtonPanel;
    public Button prevButton;
    public Button nextButton;
    public Button closeButton;

    [Header("Cámara y jugador")]
    public GameObject playerRoot;
    public GameObject playerUI;
    public Camera mainCamera;
    public Transform screenViewpoint;
    public float transitionDuration = 0.8f;

    [Header("Avatares del jugador (multi)")]
    public Transform avatarsParent;
    public GameObject[] avatarList;
    public bool includeInactiveAvatars = false;

    [Header("Cursor")]
    public bool unlockCursorDuringView = true;

    [Header("Outline (opcional)")]
    [Tooltip("Arrastra aquí tu componente Outline (QuickOutline, etc.).")]
    public Behaviour outline;
    public bool enableOutlineOnProximity = true;
    [Tooltip("Nombre del modo de Outline (p.ej. 'OutlineVisible') si tu Outline lo soporta.")]
    public string outlineModeNear = "OutlineVisible";
    public Color outlineColorNear = Color.cyan;
    [Range(0, 10f)] public float outlineWidthNear = 4f;
    public bool disableOutlineWhileViewing = true;

    [Header("Configuración del brillo")]
    public Color brilloColor = Color.cyan;
    public float pulsoVelocidad = 2f;
    public float pulsoIntensidadMax = 2f;

    [Header("Tablet Lift (Opcional)")]
    public bool useLiftAnimation = false;
    public Transform liftTransform;
    public Transform liftTarget;
    public float liftDuration = 0.5f;

    [Header("WorldSpace Canvas (opcional)")]
    public Canvas videoCanvas;
    public float videoSurfaceOffset = 0.01f; // 1 cm

    void NudgeVideoTowardCamera()
    {
        if (!videoRawImage || !mainCamera) return;
        var t = videoRawImage.rectTransform;
        var dir = (mainCamera.transform.position - t.position).normalized;
        t.position += dir * videoSurfaceOffset;
    }
  



    [Header("Audio (WebGL-friendly)")]
    public bool startMuted = true;
    public bool useAudioSourceForVideo = true;
    public AudioSource videoAudioSource;

    [Header("RenderTexture (opcional)")]
    public RenderTexture overrideRenderTexture;
    public int rtWidth = 1280, rtHeight = 720;

    [Header("Auto Viewpoint (opcional)")]
    public bool autoCreateViewpointIfNull = true;
    public Vector3 autoViewLocalOffset = new Vector3(0f, 1.6f, 2.0f);

    [Header("Integración Interactor")]
    public bool useProximityTriggers = false;
    public bool legacyKeyToInteract = false;

    #endregion

    #region Estado interno

    private readonly List<GameObject> _avatars = new List<GameObject>();
    private readonly Dictionary<GameObject, bool> _avatarPrevActive = new Dictionary<GameObject, bool>();
    private bool _disabledPlayerRoot = false;

    private bool _isViewing;
    private Vector3 _camOrigPos;
    private Quaternion _camOrigRot;
    private int _currentIndex;
    private int _totalSlides;
    private Coroutine _transitionRoutine;
    private RenderTexture _videoRT;
    private bool _videoPlayPending = false;
    public bool Viewed { get; private set; }
    private bool _playerInside = false;

    private bool _tallActive = false;

    private static UnifiedScreenDisplay _active;

    private MaterialPropertyBlock _brilloProp;
    private Renderer _brilloRend;
    private bool _brilloActivo = false;

    private Vector3 _liftOrigPos;
    private Quaternion _liftOrigRot;

    // Métricas de contenido
    private float _viewStart;
    private int _progressPct;
    private bool _completed;

    #endregion

    #region Setup

    public void EnableHighlight() { _brilloActivo = true; }

    void ApplyOutlineSettings()
    {
        if (outline == null) return;
        var t = outline.GetType();

        var propMode = t.GetProperty("OutlineMode");
        if (propMode != null && propMode.PropertyType.IsEnum)
        {
            try
            {
                var enumVal = Enum.Parse(propMode.PropertyType, outlineModeNear, true);
                propMode.SetValue(outline, enumVal, null);
            }
            catch { }
        }

        var propColor = t.GetProperty("OutlineColor");
        if (propColor != null && propColor.PropertyType == typeof(Color))
            propColor.SetValue(outline, outlineColorNear, null);

        var propWidth = t.GetProperty("OutlineWidth");
        if (propWidth != null && (propWidth.PropertyType == typeof(float) || propWidth.PropertyType == typeof(int)))
            propWidth.SetValue(outline, outlineWidthNear, null);
    }

    private void BuildAvatarsList()
    {
        _avatars.Clear();

        if (avatarList != null && avatarList.Length > 0)
        {
            foreach (var go in avatarList)
                if (go != null) _avatars.Add(go);
            return;
        }

        if (avatarsParent != null)
        {
            int childCount = avatarsParent.childCount;
            for (int i = 0; i < childCount; i++)
            {
                var child = avatarsParent.GetChild(i);
                if (child != null)
                {
                    if (includeInactiveAvatars || child.gameObject.activeSelf)
                        _avatars.Add(child.gameObject);
                }
            }
        }
    }

    public void OnDownloadClick()
    {
        if (string.IsNullOrWhiteSpace(downloadURL)) return;
        MetricsClient.I?.TrackClickEnlaceExterno(standId, downloadURL, "download");
        Application.OpenURL(downloadURL);
    }

    void SetupDownloadUI()
    {
        if (downloadButton == null) return;
        downloadButton.onClick.RemoveAllListeners();

        bool hasUrl = !string.IsNullOrWhiteSpace(downloadURL);
        if (hideDownloadIfEmpty)
            downloadButton.gameObject.SetActive(hasUrl);
        else
            downloadButton.interactable = hasUrl;

        if (hasUrl)
            downloadButton.onClick.AddListener(OnDownloadClick);
    }

    private void ToggleAvatars(bool turnOn)
    {
        if (_avatars.Count == 0) return;

        if (!turnOn)
        {
            _avatarPrevActive.Clear();
            foreach (var go in _avatars)
            {
                if (!go) continue;
                _avatarPrevActive[go] = go.activeSelf;
                go.SetActive(false);
            }
        }
        else
        {
            foreach (var kv in _avatarPrevActive)
            {
                var go = kv.Key;
                if (!go) continue;
                go.SetActive(kv.Value);
            }
            _avatarPrevActive.Clear();
        }
    }

    void Awake()
    {
        promptUI?.SetActive(false);
        navButtonPanel?.SetActive(false);
        closeButton?.gameObject.SetActive(false);

        if (promptOpenButton == null && promptUI != null)
            promptOpenButton = promptUI.GetComponentInChildren<Button>(true);
        if (promptOpenButton != null)
            promptOpenButton.onClick.AddListener(() => Interact());

        SetupDownloadUI();

        _brilloRend = GetComponent<Renderer>();
        _brilloProp = new MaterialPropertyBlock();
        _brilloRend.GetPropertyBlock(_brilloProp);
        _brilloProp.SetColor("_EmissionColor", Color.black);
        _brilloRend.material.EnableKeyword("_EMISSION");
        _brilloRend.SetPropertyBlock(_brilloProp);

        if (outline == null)
        {
            foreach (var b in GetComponentsInChildren<Behaviour>(true))
            {
                if (b != null && b.GetType().Name == "Outline") { outline = b; break; }
            }
        }
        if (outline != null) outline.enabled = false;

        if (videoPlayer != null)
        {
            if (overrideRenderTexture != null)
            {
                _videoRT = overrideRenderTexture;
            }
            else
            {
                _videoRT = new RenderTexture(rtWidth, rtHeight, 0, RenderTextureFormat.ARGB32);
                _videoRT.name = "UnifiedScreen_RT";
                _videoRT.Create();
            }

            videoPlayer.targetTexture = _videoRT;

#if UNITY_WEBGL
            videoPlayer.waitForFirstFrame = false;
            videoPlayer.skipOnDrop = true;
#else
            videoPlayer.waitForFirstFrame = true;
#endif
            videoPlayer.errorReceived += OnVideoError;
            videoPlayer.prepareCompleted += OnVideoPrepared;
            videoPlayer.loopPointReached += OnVideoFinished;
        }

        if (tallScrollRoot) tallScrollRoot.gameObject.SetActive(false);
        if (tallSlider)
        {
            tallSlider.minValue = 0f;
            tallSlider.maxValue = 1f;
            tallSlider.onValueChanged.RemoveAllListeners();
            tallSlider.onValueChanged.AddListener(v => {
                if (!tallScrollRect) return;
                float p = invertTallSlider ? 1f - v : v;
                tallScrollRect.verticalNormalizedPosition = Mathf.Clamp01(p);
            });
        }

        _totalSlides = slides?.Length ?? 0;

        if (liftTransform != null)
        {
            _liftOrigPos = liftTransform.position;
            _liftOrigRot = liftTransform.rotation;
        }

        prevButton?.onClick.AddListener(() => ShowPresentationItem(_currentIndex - 1));
        nextButton?.onClick.AddListener(() => ShowPresentationItem(_currentIndex + 1));
        closeButton?.onClick.AddListener(ExitViewMode);

        if (screenViewpoint == null && autoCreateViewpointIfNull)
        {
            var vp = new GameObject("AutoViewpoint").transform;
            vp.SetParent(transform, false);
            vp.position = transform.position
                        + transform.right * autoViewLocalOffset.x
                        + Vector3.up * autoViewLocalOffset.y
                        + transform.forward * autoViewLocalOffset.z;
            vp.LookAt(transform.position, Vector3.up);
            screenViewpoint = vp;
        }

        SetVideoVisualActive(false);
        SetImageVisualActive(true);
        ShowIdleImage();

        BuildAvatarsList();
    }

    void OnDestroy()
    {
        if (videoPlayer != null)
        {
            videoPlayer.errorReceived -= OnVideoError;
            videoPlayer.prepareCompleted -= OnVideoPrepared;
            videoPlayer.loopPointReached -= OnVideoFinished;
        }
        if (promptOpenButton != null)
            promptOpenButton.onClick.RemoveAllListeners();
        if (_active == this) _active = null;
    }

    void OnDisable()
    {
        if (_active == this) _active = null;
    }

    #endregion

    #region Update & Triggers

    void Update()
    {
        if (_brilloActivo)
        {
            float intensity = Mathf.PingPong(Time.time * pulsoVelocidad, pulsoIntensidadMax);
            Color pulseColor = brilloColor * intensity;
            _brilloRend.GetPropertyBlock(_brilloProp);
            _brilloProp.SetColor("_EmissionColor", pulseColor);
            _brilloRend.SetPropertyBlock(_brilloProp);
            DynamicGI.SetEmissive(_brilloRend, pulseColor);
        }

        if (useProximityTriggers && legacyKeyToInteract)
        {
            if (!_isViewing && _playerInside && promptUI != null && promptUI.activeSelf && Input.GetKeyDown(KeyCode.E))
                EnterViewMode();
        }

        if (lookAtCamera && promptUI != null && promptUI.activeSelf && mainCamera != null)
        {
            var camT = mainCamera.transform;
            promptUI.transform.LookAt(camT);
            promptUI.transform.Rotate(0, 180, 0);
        }
        if (_isViewing && _tallActive && tallScrollRect && tallSlider && tallSlider.gameObject.activeSelf)
        {
            float v = tallScrollRect.verticalNormalizedPosition;
            tallSlider.SetValueWithoutNotify(invertTallSlider ? 1f - v : v);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!useProximityTriggers) return;
        if (other.CompareTag("Player"))
        {
            _playerInside = true;

            if (!_isViewing)
            {
                promptUI?.SetActive(true);

                if (enableOutlineOnProximity && outline != null)
                {
                    ApplyOutlineSettings();
                    outline.enabled = true;
                }
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (!useProximityTriggers) return;
        if (other.CompareTag("Player"))
        {
            _playerInside = false;

            if (!_isViewing)
            {
                promptUI?.SetActive(false);
                if (outline != null) outline.enabled = false;
            }
        }
    }

    #endregion

    #region Interactor integration

    public void Interact()
    {
        if (!_isViewing) EnterViewMode();
    }

    public void OnGazeEnter()
    {
        if (_isViewing) return;

        if (enableOutlineOnProximity && outline != null)
        {
            ApplyOutlineSettings();
            outline.enabled = true;
        }
        if (promptUI) promptUI.SetActive(true);
    }

    public void OnGazeExit()
    {
        if (_isViewing) return;

        if (promptUI) promptUI.SetActive(false);
        if (outline != null) outline.enabled = false;
    }

    #endregion

    #region View/Open/Close

    void EnterViewMode()
    {
        if (_active != null && _active != this)
            _active.ExitViewMode();

        _active = this;

        _isViewing = true;
        promptUI?.SetActive(false);

        _brilloActivo = false;
        _brilloProp.SetColor("_EmissionColor", Color.black);
        _brilloRend.SetPropertyBlock(_brilloProp);
        Viewed = true;

        _viewStart = Time.realtimeSinceStartup;
        _progressPct = 0;
        _completed = false;

        if (mainCamera != null)
        {
            _camOrigPos = mainCamera.transform.position;
            _camOrigRot = mainCamera.transform.rotation;
        }

        if (_avatars.Count > 0)
        {
            ToggleAvatars(false);
            _disabledPlayerRoot = false;
        }
        else
        {
            if (playerRoot != null)
            {
                _disabledPlayerRoot = playerRoot.activeSelf;
                playerRoot.SetActive(false);
            }
        }

        playerUI?.SetActive(false);

        if (disableOutlineWhileViewing && outline != null)
            outline.enabled = false;

        var ctrl = mainCamera ? mainCamera.GetComponentInParent<MonoBehaviour>() : null;
        if (ctrl) ctrl.enabled = false;

        if (unlockCursorDuringView)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        if (useLiftAnimation && liftTransform != null && liftTarget != null)
        {
            StopCoroutine(nameof(AnimateTransform));

            // Evita empezar ya dentro del near clip de la cámara
            ClampAgainstNearClip(liftTransform);

            StartCoroutine(AnimateTransform(
                liftTransform,
                _liftOrigPos, liftTarget.position,
                _liftOrigRot, liftTarget.rotation,
                liftDuration,
                () =>
                {
                    // Al terminar el lift, vuelve a asegurar la distancia mínima
                    ClampAgainstNearClip(liftTransform);
                }
            ));
        }


        if (contentType == ContentType.Video && videoPlayer != null)
        {
            SetupVideoVisualsOnly();

            // Fuente de video: URL si se proporcionó, si no, VideoClip
            videoPlayer.source = !string.IsNullOrEmpty(videoURL) ? VideoSource.Url : VideoSource.VideoClip;
            videoPlayer.url = videoURL;
            videoPlayer.clip = videoClip;

            // Audio
            if (useAudioSourceForVideo && videoAudioSource != null)
            {
                videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
                videoPlayer.EnableAudioTrack(0, true);
                videoPlayer.SetTargetAudioSource(0, videoAudioSource);
                videoAudioSource.mute = startMuted;
            }
            else
            {
                videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
                videoPlayer.EnableAudioTrack(0, true);
                if (startMuted) videoPlayer.SetDirectAudioMute(0, true);
            }

            videoPlayer.isLooping = true;

            _videoPlayPending = true;
            videoPlayer.Prepare();
        }

        if (mainCamera != null && screenViewpoint != null)
        {
            StartOrRestartTransition(
                _camOrigPos, screenViewpoint.position,
                _camOrigRot, screenViewpoint.rotation,
                OnEnteredViewMode
            );
        }
        else
        {
            OnEnteredViewMode();
        }
    }

    void OnEnteredViewMode()
    {
        navButtonPanel?.SetActive(true);
        bool isPres = contentType == ContentType.Presentation;
        prevButton?.gameObject.SetActive(isPres);
        nextButton?.gameObject.SetActive(isPres);
        closeButton?.gameObject.SetActive(true);

        switch (contentType)
        {
            case ContentType.Image:
                ShowImage(singleImage);
                _completed = true; _progressPct = 100;
                break;
            case ContentType.Video:
                BoostCanvasSorting();
                break;
            case ContentType.Presentation:
                _currentIndex = 0;
                ShowPresentationItem(0);
                break;
            case ContentType.TallImageScrollable:
                ShowTallImageScrollable();
                _completed = true; _progressPct = 100;
                break;
        }

        SetupDownloadUI();
    }

    void ExitViewMode()
    {
        SetTallScrollActive(false);

        _isViewing = false;
        promptUI?.SetActive(false);

        if (_transitionRoutine != null)
            StopCoroutine(_transitionRoutine);

        if (videoPlayer != null)
        {
            _videoPlayPending = false;
            videoPlayer.Stop();
        }

        navButtonPanel?.SetActive(false);
        closeButton?.gameObject.SetActive(false);

        if (useLiftAnimation && liftTransform != null && liftTarget != null)
        {
            StopCoroutine(nameof(AnimateTransform));
            StartCoroutine(AnimateTransform(
                liftTransform,
                liftTransform.position, _liftOrigPos,
                liftTransform.rotation, _liftOrigRot,
                liftDuration,
                () =>
                {
                    // Por si la cámara quedó cerca al volver
                    ClampAgainstNearClip(liftTransform);
                }
            ));
        }


        if (mainCamera != null)
        {
            StartOrRestartTransition(
                mainCamera.transform.position, _camOrigPos,
                mainCamera.transform.rotation, _camOrigRot,
                OnExitedViewMode
            );
        }
        else
        {
            OnExitedViewMode();
        }
    }

    void OnExitedViewMode()
    {
        if (_avatars.Count > 0)
        {
            ToggleAvatars(true);
        }
        else if (playerRoot != null && _disabledPlayerRoot)
        {
            playerRoot.SetActive(true);
            _disabledPlayerRoot = false;
        }

        playerUI?.SetActive(true);

        var ctrl = mainCamera ? mainCamera.GetComponentInParent<MonoBehaviour>() : null;
        if (ctrl) ctrl.enabled = true;

        if (unlockCursorDuringView)
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        SetVideoVisualActive(false);
        SetImageVisualActive(true);
        ShowIdleImage();

        if (useProximityTriggers && _playerInside)
        {
            promptUI?.SetActive(true);
            if (enableOutlineOnProximity && outline != null)
            {
                ApplyOutlineSettings();
                outline.enabled = true;
            }
        }

        int dur = Mathf.Max(0, Mathf.RoundToInt(Time.realtimeSinceStartup - _viewStart));

        if (contentType == ContentType.Video && videoPlayer != null && videoPlayer.length > 0.01)
        {
            int pct = Mathf.RoundToInt((float)(videoPlayer.time / videoPlayer.length) * 100f);
            _progressPct = Mathf.Clamp(pct, 0, 100);
            if (_progressPct >= 100) _completed = true;
        }

        string asset = string.IsNullOrEmpty(assetId) ? screenId : assetId;

        MetricsClient.I?.TrackContenidoVisualizado(
            standId, asset, ecosystemName, dur, _completed, _progressPct
        );

        ProgressCore.I?.Stand_AddViewedScreen(standId, asset);
        ProgressCore.I?.Stand_AddTime(standId, dur);
        ProgressCore.I?.Stand_SetLastVisitNow(standId);
        ProgressCore.I?.SaveNow("contenido_visualizado");

        if (_active == this) _active = null;
    }

    #endregion

    #region Imagen / Presentación / Video Visuals

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
        if (imageRawImage != null)
        {
            SetVideoVisualActive(false);
            SetImageVisualActive(true);
            imageRawImage.texture = tex;
        }
    }

    void SetupVideoVisualsOnly()
    {
        if (videoRawImage != null)
        {
            SetImageVisualActive(false);
            SetVideoVisualActive(true);
            videoRawImage.texture = _videoRT;
        }

        NudgeVideoTowardCamera();
    }

    void ShowPresentationItem(int idx)
    {
        if (!_isViewing || contentType != ContentType.Presentation) return;

        idx = Mathf.Clamp(idx, 0, _totalSlides - 1);
        _currentIndex = idx;
        ShowImage(slides[idx]);

        _progressPct = (_totalSlides <= 1) ? 100 : Mathf.RoundToInt(((idx + 1) / (float)_totalSlides) * 100f);
        _completed = (_currentIndex >= _totalSlides - 1);

        if (prevButton) prevButton.interactable = idx > 0;
        if (nextButton) nextButton.interactable = idx < _totalSlides - 1;
    }

    #endregion

    #region Transiciones / Animaciones

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
        if (mainCamera == null)
        {
            onDone?.Invoke();
            yield break;
        }

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
            float f = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            t.position = Vector3.Lerp(startPos, endPos, f);
            t.rotation = Quaternion.Slerp(startRot, endRot, f);
            elapsed += Time.deltaTime;
            yield return null;
        }
        t.SetPositionAndRotation(endPos, endRot);
        onComplete?.Invoke();
    }

    #endregion

    #region Audio & Video helpers

    public void UnmuteVideoAudio()
    {
        if (videoPlayer == null) return;

        if (useAudioSourceForVideo && videoAudioSource != null)
        {
            videoAudioSource.mute = false;
            videoAudioSource.volume = 1f;
        }
        else
        {
            videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
            videoPlayer.SetDirectAudioMute(0, false);
        }
    }

    void OnVideoPrepared(VideoPlayer vp)
    {
        if (_videoPlayPending)
        {
            _videoPlayPending = false;
            vp.Play();
        }
        ForceRebindVideoTexture();
    }

    void OnVideoFinished(VideoPlayer vp)
    {
        _completed = true;
        _progressPct = 100;
    }

    void OnVideoError(VideoPlayer vp, string msg)
    {
        Debug.LogError("[UnifiedScreenDisplay] Video error: " + msg + " | URL: " + vp.url);
    }

    void BoostCanvasSorting()
    {
        if (!videoCanvas) return;
        videoCanvas.overrideSorting = true;
        videoCanvas.sortingLayerName = "UI";
        videoCanvas.sortingOrder = 5000;
    }

    public float nearClipPadding = 0.05f; // 5 cm

    void ClampAgainstNearClip(Transform t)
    {
        if (!mainCamera || !t) return;
        float minD = mainCamera.nearClipPlane + nearClipPadding;
        // Proyección de t sobre el forward de la cámara
        float d = Vector3.Dot(mainCamera.transform.forward, t.position - mainCamera.transform.position);
        if (d < minD)
            t.position = mainCamera.transform.position + mainCamera.transform.forward * minD;
    }

    void ForceRebindVideoTexture()
    {
        if (videoRawImage && _videoRT)
        {
            videoRawImage.texture = null;
            videoRawImage.texture = _videoRT;
        }
    }

    private void SetTallScrollActive(bool on)
    {
        _tallActive = on;
        if (tallScrollRoot) tallScrollRoot.gameObject.SetActive(on);
    }

    private void ShowTallImageScrollable()
    {
        SetImageVisualActive(false);
        SetVideoVisualActive(false);
        SetTallScrollActive(true);

        if (tallImage) tallImage.texture = singleImage;
        StartCoroutine(SetupTallLayoutNextFrame());
    }

    private IEnumerator SetupTallLayoutNextFrame()
    {
        yield return null;
        RecalculateTallImageLayout();
        if (tallScrollRect) tallScrollRect.verticalNormalizedPosition = 1f;
        if (tallSlider) tallSlider.SetValueWithoutNotify(invertTallSlider ? 0f : 1f);
    }

    private void RecalculateTallImageLayout()
    {
        if (!tallViewport || !tallImage || !tallImage.texture) return;

        var vp = tallViewport.rect;
        float texW = tallImage.texture.width;
        float texH = tallImage.texture.height;

        float targetW = Mathf.Max(1f, vp.width);
        float targetH = targetW * (texH / Mathf.Max(1f, texW));

        var rt = tallImage.rectTransform;
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(targetW, targetH);
        rt.anchoredPosition = Vector2.zero;

        bool needScroll = targetH > vp.height + 1f;
        if (tallScrollRect)
        {
            tallScrollRect.vertical = needScroll;
            tallScrollRect.enabled = needScroll;
        }
        if (tallSlider) tallSlider.gameObject.SetActive(needScroll);
    }

    private void SetImageVisualActive(bool on)
    {
        if (imageRawImage != null) imageRawImage.gameObject.SetActive(on);
    }

    private void SetVideoVisualActive(bool on)
    {
        if (videoRawImage != null) videoRawImage.gameObject.SetActive(on);
    }

    #endregion
}

