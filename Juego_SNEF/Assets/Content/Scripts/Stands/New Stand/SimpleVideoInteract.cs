using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

[RequireComponent(typeof(Collider))]
public class SimpleVideoInteract : MonoBehaviour
{
    [Header("Prompt UI")]
    public GameObject promptUI;          // cartel "Presiona E"
    public bool lookAtCamera = true;
    public Camera mainCamera;

    [Header("Interacción")]
    public KeyCode interactKey = KeyCode.E;
    public KeyCode stopKey = KeyCode.Escape;

    [Header("UI de Video")]
    public RawImage videoRawImage;       // RawImage donde se verá el video
    [Tooltip("RT del asset (recomendado). Asigna uno por pantalla.")]
    public RenderTexture renderTexture;  // tu RT "Screen" (uno por instancia)

    [Header("Fuente de Video")]
    [Tooltip("Si hay URL se usa URL; si está vacío, se usa Clip.")]
    public string videoURL;
    public VideoClip videoClip;
    public VideoPlayer videoPlayer;      // en un GO vacío (recomendado)

    [Header("Audio")]
    public bool startMuted = true;
    public bool useAudioSource = true;
    public AudioSource audioSource;      // opcional, recomendado WebGL

    [Header("Opcional")]
    public bool loop = true;
    public int rtWidth = 1280;
    public int rtHeight = 720;

    // estado interno
    private bool _playerInside = false;
    private bool _pendingPlay = false;
    private bool _createdRT = false;

    void Awake()
    {
        // Prompt oculto
        if (promptUI) promptUI.SetActive(false);

        // Asegurar VideoPlayer
        if (!videoPlayer)
        {
            Debug.LogError("[SimpleVideoInteract] Asigna un VideoPlayer.");
            enabled = false;
            return;
        }

        // RenderTexture: usa el de asset si lo asignaste; si no, crea uno.
        if (!renderTexture)
        {
            renderTexture = new RenderTexture(rtWidth, rtHeight, 0, RenderTextureFormat.ARGB32);
            renderTexture.name = "SimpleVideo_RT_" + name;
            renderTexture.Create();
            _createdRT = true;
        }

        // Conectar VP -> RT y RT -> RawImage
        videoPlayer.targetTexture = renderTexture;
        if (videoRawImage) videoRawImage.texture = renderTexture;

#if UNITY_WEBGL
        videoPlayer.waitForFirstFrame = false;
        videoPlayer.skipOnDrop = true;
#else
        videoPlayer.waitForFirstFrame = true;
#endif
        videoPlayer.isLooping = loop;
        videoPlayer.errorReceived += OnVideoError;
        videoPlayer.prepareCompleted += OnVideoPrepared;

        // Audio
        if (useAudioSource && audioSource != null)
        {
            videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
            videoPlayer.EnableAudioTrack(0, true);
            videoPlayer.SetTargetAudioSource(0, audioSource);
            audioSource.mute = startMuted;
        }
        else
        {
            videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
            videoPlayer.EnableAudioTrack(0, true);
            videoPlayer.SetDirectAudioMute(0, startMuted);
        }
    }

    void OnDestroy()
    {
        if (videoPlayer != null)
        {
            videoPlayer.errorReceived -= OnVideoError;
            videoPlayer.prepareCompleted -= OnVideoPrepared;
        }
        if (_createdRT && renderTexture != null)
        {
            renderTexture.Release();
        }
    }

    void Update()
    {
        // Hacer que el prompt mire a la cámara (si es World Space)
        if (lookAtCamera && promptUI && promptUI.activeSelf && mainCamera)
        {
            var t = promptUI.transform;
            t.LookAt(mainCamera.transform);
            t.Rotate(0, 180, 0);
        }

        if (!_playerInside) return;

        if (Input.GetKeyDown(interactKey))
            StartVideo();

        if (Input.GetKeyDown(stopKey))
            StopVideo();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            _playerInside = true;
            if (promptUI) promptUI.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            _playerInside = false;
            if (promptUI) promptUI.SetActive(false);
        }
    }

    public void StartVideo()
    {
        if (!videoPlayer) return;

        // Selección de fuente
        if (!string.IsNullOrEmpty(videoURL))
        {
            videoPlayer.source = VideoSource.Url;
            videoPlayer.url = videoURL;
        }
        else
        {
            videoPlayer.source = VideoSource.VideoClip;
            videoPlayer.clip = videoClip;
        }

        // Activar la salida visual
        if (videoRawImage) videoRawImage.gameObject.SetActive(true);

        // Preparar primero (mejor en WebGL)
        _pendingPlay = true;
        videoPlayer.Prepare();

        // Oculta prompt al iniciar
        if (promptUI) promptUI.SetActive(false);
    }

    public void StopVideo()
    {
        if (!videoPlayer) return;
        _pendingPlay = false;
        videoPlayer.Stop();

        // Si quieres ocultar el RawImage al parar, descomenta:
        // if (videoRawImage) videoRawImage.gameObject.SetActive(false);

        // Mostrar prompt otra vez (si sigues en rango)
        if (_playerInside && promptUI) promptUI.SetActive(true);
    }

    // Botón para activar audio (si dejaste startMuted = true)
    public void Unmute()
    {
        if (useAudioSource && audioSource != null) audioSource.mute = false;
        else videoPlayer.SetDirectAudioMute(0, false);
    }

    // Eventos
    private void OnVideoPrepared(VideoPlayer vp)
    {
        if (_pendingPlay)
        {
            _pendingPlay = false;
            vp.Play();
        }
    }

    private void OnVideoError(VideoPlayer vp, string msg)
    {
        Debug.LogError("[SimpleVideoInteract] Video error: " + msg + " | URL: " + vp.url);
    }
}
