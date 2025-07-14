using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Controla la pantalla de introducción: muestra tras un delay, pausa el juego,
/// anima el fondo y el sprite, y al pulsar "Entendido" invierte las animaciones y reanuda el juego.
/// También gestiona la visibilidad del cursor.
/// </summary>
public class IntroScreenController : MonoBehaviour
{
    [Header("UI Elements")]
    [Tooltip("Panel raíz de Bienvenida (se activa con alpha)")]
    public CanvasGroup bienvenidaGroup;
    [Tooltip("Imagen de fondo que crece")]
    public RectTransform bgIntroduccion;
    [Tooltip("Sprite de Dusef que sube")]
    public RectTransform dusefSprite;
    [Tooltip("Botón 'Entendido'")]
    public Button btnEntendido;

    [Header("Timings")]
    [Tooltip("Retraso antes de mostrar la pantalla (s)")]
    public float delayBeforeShow = 2f;
    [Tooltip("Duración de la animación de entrada/salida (s)")]
    public float animDuration = 0.5f;

    // Posiciones / escalas originales
    private Vector2 _spriteStartPos;
    private Vector2 _spriteEndPos;
    private Vector3 _bgStartScale;
    private Vector3 _bgEndScale = Vector3.one;

    void Awake()
    {
        // Guardar valores iniciales
        _bgStartScale = Vector3.zero;
        _spriteEndPos = dusefSprite.anchoredPosition;
        _spriteStartPos = _spriteEndPos + Vector2.down * 200f; // 200px debajo

        // Set inicial: invisible / escalado a 0 / sprite abajo
        bienvenidaGroup.alpha = 0f;
        bienvenidaGroup.interactable = false;
        bienvenidaGroup.blocksRaycasts = false;

        bgIntroduccion.localScale = _bgStartScale;
        dusefSprite.anchoredPosition = _spriteStartPos;
    }

    void Start()
    {
        // Lanzar la secuencia sin bloquear Update cuando timeScale=0
        StartCoroutine(ShowIntroCoroutine());
    }

    private IEnumerator ShowIntroCoroutine()
    {
        // Espera real (unscaled)
        yield return new WaitForSecondsRealtime(delayBeforeShow);

        // Pausar juego
        Time.timeScale = 0f;

        // Mostrar UI y activar interacciones
        bienvenidaGroup.alpha = 1f;
        bienvenidaGroup.interactable = true;
        bienvenidaGroup.blocksRaycasts = true;

        // Mostrar cursor para el botón
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // Ani entrada
        StartCoroutine(AnimateScale(bgIntroduccion, _bgStartScale, _bgEndScale));
        StartCoroutine(AnimatePosition(dusefSprite, _spriteStartPos, _spriteEndPos));

        // Botón
        btnEntendido.onClick.AddListener(HideIntro);
    }

    private void HideIntro()
    {
        // Desactivar interacción durante salida
        bienvenidaGroup.interactable = false;
        bienvenidaGroup.blocksRaycasts = false;

        // Animar salida inversa
        StartCoroutine(AnimateScale(bgIntroduccion, _bgEndScale, _bgStartScale));
        StartCoroutine(AnimatePosition(dusefSprite, _spriteEndPos, _spriteStartPos));

        // Después de animDuration, reanudar juego y ocultar panel
        StartCoroutine(DelayedResume());
    }

    private IEnumerator DelayedResume()
    {
        // Espera real para la animación
        yield return new WaitForSecondsRealtime(animDuration);

        // Ocultar completamente
        bienvenidaGroup.alpha = 0f;

        // Ocultar cursor y bloquear
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        // Reanudar juego
        Time.timeScale = 1f;
    }

    private IEnumerator AnimateScale(RectTransform rt, Vector3 from, Vector3 to)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / animDuration;
            rt.localScale = Vector3.Lerp(from, to, t);
            yield return null;
        }
        rt.localScale = to;
    }

    private IEnumerator AnimatePosition(RectTransform rt, Vector2 from, Vector2 to)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / animDuration;
            rt.anchoredPosition = Vector2.Lerp(from, to, t);
            yield return null;
        }
        rt.anchoredPosition = to;
    }
}
