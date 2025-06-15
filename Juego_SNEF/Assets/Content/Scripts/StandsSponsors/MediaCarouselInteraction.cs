using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using System.Collections;

public class MediaCarouselInteraction : MonoBehaviour,
    Interactor.IInteractable,
    Interactor.IInteractableFeedback
{
    public enum ContentType { Image, Video, Presentation }

    [Header("Contenido")]
    public ContentType contentType;

    [Header("– Imagen individual –")]
    [Tooltip("Textura que se mostrará si eliges 'Image'")]
    public Texture2D singleImage;

    [Header("– Video –")]
    [Tooltip("URL del video (alternativa a VideoClip)")]
    public string videoURL;
    [Tooltip("(Opcional) VideoClip si no usas URL")]
    public VideoClip videoClip;
    [Tooltip("Componente VideoPlayer que reproducirá el video")]
    public VideoPlayer videoPlayer;

    [Header("– Presentación –")]
    [Tooltip("Slides que se mostrarán si eliges 'Presentation'")]
    public Texture2D[] slides;
    [Tooltip("Sólo en 'Presentation': botón Anterior")]
    public Button prevButton;
    [Tooltip("Sólo en 'Presentation': botón Siguiente")]
    public Button nextButton;
    [Tooltip("Sólo en 'Presentation': botón Cerrar")]
    public Button closeButton;

    [Header("Interacción y cámara")]
    [Tooltip("Canvas 'Presiona E' para mostrar al acercarse")]
    public Canvas promptCanvas;
    [Tooltip("¿Debe el prompt mirar a la cámara?")]
    public bool lookAtCamera = true;

    [Tooltip("Objeto raíz del jugador para ocultar durante la vista")]
    public GameObject playerRoot;
    [Tooltip("Cámara principal del jugador")]
    public Camera mainCamera;
    [Tooltip("Punto de vista (Empty) frente a la pantalla")]
    public Transform screenViewpoint;
    [Tooltip("Duración en segundos de la transición de cámara")]
    public float transitionDuration = 0.5f;

    // Estado interno
    private bool isViewing = false;
    private Vector3 camOrigPos;
    private Quaternion camOrigRot;
    private int currentIndex;

    void Start()
    {
        // Ocultar "Presiona E"
        if (promptCanvas) promptCanvas.gameObject.SetActive(false);

        // Sólo mostrar botones en Presentation
        if (contentType != ContentType.Presentation)
        {
            if (prevButton) prevButton.gameObject.SetActive(false);
            if (nextButton) nextButton.gameObject.SetActive(false);
            if (closeButton) closeButton.gameObject.SetActive(false);
        }

        // Preparar VideoPlayer
        if (contentType == ContentType.Video && videoPlayer != null)
        {
            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = false;
        }

        // Enlazar botones
        if (prevButton) prevButton.onClick.AddListener(PrevSlide);
        if (nextButton) nextButton.onClick.AddListener(NextSlide);
        if (closeButton) closeButton.onClick.AddListener(CloseViewer);
    }

    public void OnGazeEnter()
    {
        if (promptCanvas) promptCanvas.gameObject.SetActive(true);
    }

    public void OnGazeExit()
    {
        if (promptCanvas) promptCanvas.gameObject.SetActive(false);
        if (!isViewing) EndViewing();
    }

    public void Interact()
    {
        if (isViewing) return;
        StartViewing();
    }

    private void StartViewing()
    {
        isViewing = true;

        // Guardar cámara original y ocultar jugador
        camOrigPos = mainCamera.transform.position;
        camOrigRot = mainCamera.transform.rotation;
        if (playerRoot) playerRoot.SetActive(false);

        // Transición de cámara
        StartCoroutine(MoveCamera(screenViewpoint.position,
                                  screenViewpoint.rotation,
                                  transitionDuration,
                                  () =>
                                  {
                                      // Iniciar según tipo
                                      switch (contentType)
                                      {
                                          case ContentType.Image:
                                              ShowImage();
                                              break;
                                          case ContentType.Video:
                                              PlayVideo();
                                              break;
                                          case ContentType.Presentation:
                                              ShowPresentation();
                                              break;
                                      }
                                  }));
    }

    private void EndViewing()
    {
        // Detener video si corresponde
        if (contentType == ContentType.Video && videoPlayer != null && videoPlayer.isPlaying)
            videoPlayer.Stop();

        // Ocultar botones de Presentation
        if (prevButton) prevButton.gameObject.SetActive(false);
        if (nextButton) nextButton.gameObject.SetActive(false);
        if (closeButton) closeButton.gameObject.SetActive(false);

        StartCoroutine(MoveCamera(camOrigPos,
                                  camOrigRot,
                                  transitionDuration,
                                  () =>
                                  {
                                      if (playerRoot) playerRoot.SetActive(true);
                                      isViewing = false;
                                  }));
    }

    // – IMAGE –
    private RawImage tmpImageUI; // opcional: si usas RawImage world-space
    private void ShowImage()
    {
        if (tmpImageUI != null)
            tmpImageUI.texture = singleImage;
    }

    // – VIDEO –
    private void PlayVideo()
    {
        if (videoPlayer != null)
        {
            if (!string.IsNullOrEmpty(videoURL))
                videoPlayer.url = videoURL;
            else if (videoClip != null)
                videoPlayer.clip = videoClip;

            videoPlayer.Play();
        }
    }

    // – PRESENTATION –
    private void ShowPresentation()
    {
        currentIndex = 0;
        ChangeSlide(currentIndex);
        if (prevButton) prevButton.gameObject.SetActive(true);
        if (nextButton) nextButton.gameObject.SetActive(true);
        if (closeButton) closeButton.gameObject.SetActive(true);
    }

    private void PrevSlide() => ChangeSlide(currentIndex - 1);
    private void NextSlide() => ChangeSlide(currentIndex + 1);

    private void ChangeSlide(int i)
    {
        currentIndex = Mathf.Clamp(i, 0, slides.Length - 1);

        // Si usas quad + PropertyBlock:
        var rend = GetComponent<Renderer>();
        var block = new MaterialPropertyBlock();
        rend.GetPropertyBlock(block);
        block.SetTexture("_MainTex", slides[currentIndex]);
        rend.SetPropertyBlock(block);

        // Actualizar botones
        if (prevButton) prevButton.interactable = currentIndex > 0;
        if (nextButton) nextButton.interactable = currentIndex < slides.Length - 1;
    }

    private void CloseViewer()
    {
        EndViewing();
    }

    private IEnumerator MoveCamera(Vector3 targetPos, Quaternion targetRot, float duration, System.Action onComplete)
    {
        float elapsed = 0f;
        Vector3 startPos = mainCamera.transform.position;
        Quaternion startRot = mainCamera.transform.rotation;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            mainCamera.transform.position = Vector3.Lerp(startPos, targetPos, t);
            mainCamera.transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            yield return null;
        }

        mainCamera.transform.position = targetPos;
        mainCamera.transform.rotation = targetRot;
        onComplete?.Invoke();
    }

    private void LateUpdate()
    {
        if (lookAtCamera && promptCanvas != null && promptCanvas.gameObject.activeSelf)
        {
            Transform cam = Camera.main.transform;
            promptCanvas.transform.LookAt(cam);
            promptCanvas.transform.Rotate(0, 180, 0);
        }
    }
}