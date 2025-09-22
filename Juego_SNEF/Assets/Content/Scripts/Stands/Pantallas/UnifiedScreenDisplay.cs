using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using System.Collections;

[RequireComponent(typeof(Collider), typeof(Renderer))]
public class UnifiedScreenDisplay : MonoBehaviour,
    Interactor.IInteractable,
    Interactor.IInteractableFeedback
{
    public enum ContentType { Image, Video, Presentation }

    #region Inspector

    [Header("Prompt UI")]
    public GameObject promptUI;
    public bool lookAtCamera = true;

    [Header("Stand (Progress)")]
    public string standId;              // ej. eco1_banco_master
    public string screenId = "screen1"; // screen1, screen2, screen3, screen4
    public string standType = "master"; // master/premier/excellence/punto
    public bool reportViewToProgress = true;


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

    [Header("Canvas Targets")]
    [Tooltip("RawImage para IMAGEN/SLIDES/IDLE")]
    public RawImage imageRawImage;
    [Tooltip("RawImage para VIDEO")]
    public RawImage videoRawImage;

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

    // ——— NUEVO: Avatares múltiples ———
    [Header("Avatares del jugador (multi)")]
    [Tooltip("Padre que contiene a todos los avatares (p.ej. 'Personajes'). Tomará sus hijos DIRECTOS.")]
    public Transform avatarsParent;

    [Tooltip("Opcional: si prefieres seleccionar avatares uno por uno, úsalos aquí. Si hay al menos uno, se ignora avatarsParent.")]
    public GameObject[] avatarList;

    [Tooltip("Si está activo y hay avatarsParent, también incluirá hijos que estén inicialmente inactivos.")]
    public bool includeInactiveAvatars = false;

    // Estado interno (no tocar)
    private readonly System.Collections.Generic.List<GameObject> _avatars = new System.Collections.Generic.List<GameObject>();
    private readonly System.Collections.Generic.Dictionary<GameObject, bool> _avatarPrevActive = new System.Collections.Generic.Dictionary<GameObject, bool>();
    private bool _disabledPlayerRoot = false;

    [Header("Cursor")]
    public bool unlockCursorDuringView = true;

    [Header("Outline (QuickOutline)")]
    public Outline outline;                                  // arrastra el componente Outline
    public bool enableOutlineOnProximity = true;             // activar al acercarse
    public Outline.Mode outlineModeNear = Outline.Mode.OutlineVisible;
    public Color outlineColorNear = Color.cyan;
    [Range(0, 10f)] public float outlineWidthNear = 4f;
    public bool disableOutlineWhileViewing = true;           // opcional: apagar outline durante la vista

    [Header("Configuración del brillo")]
    public Color brilloColor = Color.cyan;
    public float pulsoVelocidad = 2f;
    public float pulsoIntensidadMax = 2f;

    [Header("Tablet Lift (Opcional)")]
    public bool useLiftAnimation = false;
    public Transform liftTransform;
    public Transform liftTarget;
    public float liftDuration = 0.5f;

    [Header("Audio (WebGL-friendly)")]
    [Tooltip("Iniciar el video muteado para cumplir políticas de autoplay")]
    public bool startMuted = true;
    [Tooltip("Rutar el audio a un AudioSource (recomendado WebGL)")]
    public bool useAudioSourceForVideo = true;
    public AudioSource videoAudioSource; // opcional

    [Header("Fallback local (StreamingAssets)")]
    [Tooltip("En WebGL, usa un MP4 local del mismo origen (evita CORS).")]
    public bool forceLocalOnWebGL = true;
    [Tooltip("Nombre del archivo dentro de Assets/StreamingAssets/ (ej. fallback.mp4)")]
    public string localStreamingAssetsFileName = "fallback.mp4";

    [Header("RenderTexture (opcional)")]
    [Tooltip("Si lo asignas, usará ESTE RT (ej. 'Screen') en lugar de crear uno por código.")]
    public RenderTexture overrideRenderTexture;
    public int rtWidth = 1280, rtHeight = 720;

    [Header("Auto Viewpoint (opcional)")]
    public bool autoCreateViewpointIfNull = true;
    public Vector3 autoViewLocalOffset = new Vector3(0f, 1.6f, 2.0f);

    [Header("Integración Interactor")]
    [Tooltip("Si está activo, también podrás interactuar por proximidad (legacy). Recomiendo dejarlo en OFF y usar el Interactor unificado.")]
    public bool useProximityTriggers = false;
    [Tooltip("Sólo tiene efecto si 'useProximityTriggers' está activo. Permite presionar E en proximidad (legacy).")]
    public bool legacyKeyToInteract = false;

    #endregion

    #region Estado interno

    private bool _isViewing;
    private Vector3 _camOrigPos;
    private Quaternion _camOrigRot;
    private int _currentIndex;
    private int _totalSlides;
    private Coroutine _transitionRoutine;
    private RenderTexture _videoRT;
    private bool _videoPlayPending = false;
    public bool Viewed { get; private set; }
    private bool _playerInside = false; // sólo si usas triggers

    // Pantalla activa global (sólo una “abierta”)
    private static UnifiedScreenDisplay _active;

    // Brillo
    private MaterialPropertyBlock _brilloProp;
    private Renderer _brilloRend;
    private bool _brilloActivo = false;

    // Lift
    private Vector3 _liftOrigPos;
    private Quaternion _liftOrigRot;

    // Fallback tracking
    private bool _usingLocalFallback = false;

    #endregion

    #region Setup

    public void EnableHighlight() { _brilloActivo = true; }

    void ApplyOutlineSettings()
    {
        if (outline == null) return;
        outline.OutlineMode = outlineModeNear;
        outline.OutlineColor = outlineColorNear;
        outline.OutlineWidth = outlineWidthNear;
    }

    // Construye la lista de avatares desde el array o desde el padre
    private void BuildAvatarsList()
    {
        _avatars.Clear();

        // Si el array tiene al menos 1, uso el array
        if (avatarList != null && avatarList.Length > 0)
        {
            foreach (var go in avatarList)
                if (go != null) _avatars.Add(go);
            return;
        }

        // Si no, uso los hijos DIRECTOS del padre
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

    // Apaga/prende los avatares guardando/restaurando su estado previo
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
        // UI inicial
        promptUI?.SetActive(false);
        navButtonPanel?.SetActive(false);
        closeButton?.gameObject.SetActive(false);

        // Brillo
        _brilloRend = GetComponent<Renderer>();
        _brilloProp = new MaterialPropertyBlock();
        _brilloRend.GetPropertyBlock(_brilloProp);
        _brilloProp.SetColor("_EmissionColor", Color.black);
        _brilloRend.material.EnableKeyword("_EMISSION");
        _brilloRend.SetPropertyBlock(_brilloProp);

        // Outline (si no fue asignado)
        if (outline == null) outline = GetComponent<Outline>() ?? GetComponentInChildren<Outline>();
        if (outline != null) outline.enabled = false;

        // RenderTexture para video (UI)
        if (videoPlayer != null)
        {
            if (overrideRenderTexture != null)
            {
                _videoRT = overrideRenderTexture; // usa RT de asset (tu "Screen")
            }
            else
            {
                _videoRT = new RenderTexture(rtWidth, rtHeight, 0, RenderTextureFormat.ARGB32);
                _videoRT.name = "UnifiedScreen_RT";
                _videoRT.Create();
            }

            videoPlayer.targetTexture = _videoRT;

#if UNITY_WEBGL
            videoPlayer.waitForFirstFrame = false; // evita quedarse esperando frame 1
            videoPlayer.skipOnDrop = true;
#else
            videoPlayer.waitForFirstFrame = true;
#endif
            videoPlayer.errorReceived += OnVideoError;
            videoPlayer.prepareCompleted += OnVideoPrepared;
        }

        _totalSlides = slides?.Length ?? 0;

        // Lift
        if (liftTransform != null)
        {
            _liftOrigPos = liftTransform.position;
            _liftOrigRot = liftTransform.rotation;
        }

        // Botones
        prevButton?.onClick.AddListener(() => ShowPresentationItem(_currentIndex - 1));
        nextButton?.onClick.AddListener(() => ShowPresentationItem(_currentIndex + 1));
        closeButton?.onClick.AddListener(ExitViewMode);

        // Auto-Viewpoint si está vacío
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

        // Estado visual inicial (priorizar UI)
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
        }
        if (_active == this) _active = null;
    }

    void OnDisable()
    {
        if (_active == this) _active = null;
    }

    #endregion

    #region Update & Triggers (legacy opcional)

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

        // Entrada de usuario LEGACY (sólo si activaste proximidad)
        if (useProximityTriggers && legacyKeyToInteract)
        {
            if (!_isViewing && _playerInside && promptUI != null && promptUI.activeSelf && Input.GetKeyDown(KeyCode.E))
                EnterViewMode();
            else if (_isViewing && Input.GetKeyDown(KeyCode.Escape))
                ExitViewMode();
        }

        // Billboarding del prompt
        if (lookAtCamera && promptUI != null && promptUI.activeSelf && mainCamera != null)
        {
            var camT = mainCamera.transform;
            promptUI.transform.LookAt(camT);
            promptUI.transform.Rotate(0, 180, 0);
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
                    outline.enabled = true; // 🔵 encender outline junto con el prompt
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

                if (outline != null)
                    outline.enabled = false; // 🔴 apagar outline al salir
            }
        }
    }

    #endregion

    #region Interactor integration (UNIFICADO)

    // Llamado por el Interactor cuando presionas "Interact" y esta pantalla es el objetivo
    public void Interact()
    {
        if (!_isViewing) EnterViewMode();
    }

    // Llamado por el Interactor cuando esta pantalla gana el foco
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

    // Llamado por el Interactor cuando esta pantalla pierde el foco
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
        // Cierra cualquier otra pantalla activa para evitar conflictos
        if (_active != null && _active != this)
            _active.ExitViewMode();

        _active = this;

        _isViewing = true;
        promptUI?.SetActive(false);

        // Desactiva brillo y marca como visto
        _brilloActivo = false;
        _brilloProp.SetColor("_EmissionColor", Color.black);
        _brilloRend.SetPropertyBlock(_brilloProp);
        Viewed = true;

        // Reportar primera vista de pantalla al progreso
        if (reportViewToProgress && ProgressCore.I != null)
        {
            ProgressCore.I.Stand_MarkScreenViewed(standId, screenId, standType);
            // si quieres enviar inmediatamente:
            // ProgressCore.I.SaveNow("stand_screen_viewed_" + standId + "_" + screenId);
        }


        // Guarda cámara y oculta jugador/UI
        if (mainCamera != null)
        {
            _camOrigPos = mainCamera.transform.position;
            _camOrigRot = mainCamera.transform.rotation;
        }

        // AHORA
        // 1) Oculta avatares si hay lista; en caso contrario, fallback al playerRoot (como antes)
        if (_avatars.Count > 0)
        {
            ToggleAvatars(false);
            _disabledPlayerRoot = false; // no tocamos el playerRoot en este caso
        }
        else
        {
            if (playerRoot != null)
            {
                _disabledPlayerRoot = playerRoot.activeSelf;
                playerRoot.SetActive(false);
            }
        }

        // 2) UI del jugador como antes
        playerUI?.SetActive(false);

        // Apaga outline durante la vista (opcional)
        if (disableOutlineWhileViewing && outline != null)
            outline.enabled = false;

        // Desactiva control (idealmente referencia explícita a tu controller)
        var ctrl = mainCamera ? mainCamera.GetComponentInParent<MonoBehaviour>() : null;
        if (ctrl) ctrl.enabled = false;

        // Cursor
        if (unlockCursorDuringView)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        // Lift opcional
        if (useLiftAnimation && liftTransform != null && liftTarget != null)
        {
            StopCoroutine(nameof(AnimateTransform));
            StartCoroutine(AnimateTransform(
                liftTransform,
                _liftOrigPos, liftTarget.position,
                _liftOrigRot, liftTarget.rotation,
                liftDuration,
                null
            ));
        }

        // VIDEO — Setup visuals + Prepare() → Play() en OnVideoPrepared
        if (contentType == ContentType.Video && videoPlayer != null)
        {
            SetupVideoVisualsOnly(); // asegura RT en UI y activa el RawImage correcto

            bool useLocal =
                forceLocalOnWebGL &&
                Application.platform == RuntimePlatform.WebGLPlayer &&
                !string.IsNullOrEmpty(localStreamingAssetsFileName);

            _usingLocalFallback = false;

            if (useLocal)
            {
                videoPlayer.source = VideoSource.Url;
                videoPlayer.url = GetLocalStreamingURL();
                _usingLocalFallback = true;
            }
            else
            {
                videoPlayer.source = !string.IsNullOrEmpty(videoURL) ? VideoSource.Url : VideoSource.VideoClip;
                videoPlayer.url = videoURL;
                videoPlayer.clip = videoClip;
            }

            // Audio routing
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

            // Clave: preparar primero (WebGL-friendly)
            _videoPlayPending = true;
            videoPlayer.Prepare();
        }

        // Transición de cámara
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
            // sin cámara o viewpoint; aplicar estado y continuar
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
                break;
            case ContentType.Video:
                // visuals ya activos; Play ocurre en OnVideoPrepared
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

        if (videoPlayer != null)
        {
            _videoPlayPending = false;
            videoPlayer.Stop();
        }

        navButtonPanel?.SetActive(false);
        closeButton?.gameObject.SetActive(false);

        // Lift down
        if (useLiftAnimation && liftTransform != null && liftTarget != null)
        {
            StopCoroutine(nameof(AnimateTransform));
            StartCoroutine(AnimateTransform(
                liftTransform,
                liftTransform.position, _liftOrigPos,
                liftTransform.rotation, _liftOrigRot,
                liftDuration,
                null
            ));
        }

        // Cámara de vuelta
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
        // Restaurar avatares o fallback al playerRoot
        if (_avatars.Count > 0)
        {
            ToggleAvatars(true);
        }
        else if (playerRoot != null && _disabledPlayerRoot)
        {
            playerRoot.SetActive(true);
            _disabledPlayerRoot = false;
        }

        // UI del jugador
        playerUI?.SetActive(true);

        var ctrl = mainCamera ? mainCamera.GetComponentInParent<MonoBehaviour>() : null;
        if (ctrl) ctrl.enabled = true;

        if (unlockCursorDuringView)
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        // Restaurar idle (priorizar UI)
        SetVideoVisualActive(false);
        SetImageVisualActive(true);
        ShowIdleImage();

        // Si sigues dentro del trigger legacy, vuelve a encender outline + prompt
        if (useProximityTriggers && _playerInside)
        {
            promptUI?.SetActive(true);

            if (enableOutlineOnProximity && outline != null)
            {
                ApplyOutlineSettings();
                outline.enabled = true;
            }
        }

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

    // === Imagen (UI) ===
    void ShowImage(Texture2D tex)
    {
        if (imageRawImage != null)
        {
            SetVideoVisualActive(false);
            SetImageVisualActive(true);
            imageRawImage.texture = tex;
        }
    }

    // Sólo asegura la salida visual del video (no hace Play)
    void SetupVideoVisualsOnly()
    {
        if (videoRawImage != null)
        {
            SetImageVisualActive(false);
            SetVideoVisualActive(true);
            videoRawImage.texture = _videoRT;
        }
    }

    void ShowPresentationItem(int idx)
    {
        if (!_isViewing || contentType != ContentType.Presentation) return;

        idx = Mathf.Clamp(idx, 0, _totalSlides - 1);
        _currentIndex = idx;
        ShowImage(slides[idx]);

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

    // Botón "Activar audio"
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

    string GetLocalStreamingURL()
    {
        return Application.streamingAssetsPath + "/" + localStreamingAssetsFileName;
    }

    void OnVideoPrepared(VideoPlayer vp)
    {
        if (_videoPlayPending)
        {
            _videoPlayPending = false;
            vp.Play(); // reproducir SOLO tras prepareCompleted
        }
    }

    void OnVideoError(VideoPlayer vp, string msg)
    {
        Debug.LogError("[UnifiedScreenDisplay] Error de VideoPlayer: " + msg + " | URL: " + vp.url);

        if (!_usingLocalFallback &&
            Application.platform == RuntimePlatform.WebGLPlayer &&
            !string.IsNullOrEmpty(localStreamingAssetsFileName))
        {
            PlayFallbackFromStreamingAssets();
        }
    }

    void PlayFallbackFromStreamingAssets()
    {
        if (videoPlayer == null) return;

        string localUrl = GetLocalStreamingURL();
        videoPlayer.source = VideoSource.Url;
        videoPlayer.url = localUrl;
        videoPlayer.isLooping = true;

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
            videoPlayer.SetDirectAudioMute(0, true);
        }

        _usingLocalFallback = true;

        Debug.Log("[UnifiedScreenDisplay] Reintentando con StreamingAssets: " + localUrl);

        // Igual que el flujo principal: Prepare() → Play() en OnVideoPrepared
        _videoPlayPending = true;
        videoPlayer.Prepare();
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
