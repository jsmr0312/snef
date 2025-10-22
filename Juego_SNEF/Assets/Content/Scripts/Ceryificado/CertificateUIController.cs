using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DefaultExecutionOrder(100)] // corre después de otros setup para poder reenganchar botones
public class CertificateUIController : MonoBehaviour
{
    [Header("Botones principales")]
    public Button openCertificateButton;      // ObtenTuCertificadoButton
    public Button closePanelButton;           // BotonCerrarPanel

    [Header("Ventana / Secciones")]
    public GameObject panelVentanaCertificado;    // PanelVentanaCertificado (raíz)
    public GameObject panelIngresarNombre;        // PanelIngresarNombre
    public GameObject panelDescargarCertificado;  // PanelDescargarCertificado

    [Header("Encuesta")]
    public Button responderEncuestaButton;    // ResponderEncuestaButton
    [TextArea(1, 3)] public string encuestaURL = "https://tu-encuesta.ejemplo.com";

    [Header("Nombre del certificado")]
    public TMP_InputField inputNameTMP;       // InputField (TMP)
    public Button enviarNombreButton;         // EnviarNombreButton
    public bool guardarNombreEnPlayerPrefs = true;
    public string playerPrefsKey = "certificado_nombre";

    [Header("Previsualización")]
    public Image imagePrevisualizacionCertificado; // ImagePrevisualizaciónCertificado (UI.Image)
    public bool generarPreviewDinamica = true;

    [Header("Descarga")]
    public Button descargarCertificadoButton; // Botón que detona la descarga
    public CertificateDownloader downloader;  // Tu downloader existente

    void Awake()
    {
        // Ventana cerrada al inicio
        if (panelVentanaCertificado) panelVentanaCertificado.SetActive(false);

        openCertificateButton?.onClick.AddListener(OpenWindow);
        closePanelButton?.onClick.AddListener(CloseWindow);
        responderEncuestaButton?.onClick.AddListener(OpenSurvey);
        enviarNombreButton?.onClick.AddListener(OnSubmitName);

        // Reemplazar listeners del botón de descarga por nuestro wrapper (evita doble enganche)
        if (descargarCertificadoButton != null)
        {
            descargarCertificadoButton.onClick.RemoveAllListeners();
            descargarCertificadoButton.onClick.AddListener(DownloadAndTrack);
        }

        // Si ProgressCore ya tiene nombre/descarga, respetarlo
        var cert = ProgressCore.I?.Data?.certificate;
        if (cert != null)
        {
            if (!string.IsNullOrWhiteSpace(cert.name))
            {
                downloader?.SetNombre(cert.name);
                inputNameTMP?.SetTextWithoutNotify(cert.name);
            }

            if (cert.downloaded || !string.IsNullOrWhiteSpace(cert.name))
                ShowDownloadPhase();
            else
                ShowNamePhase();
        }
        else
        {
            // Fallback: PlayerPrefs (opcional)
            if (guardarNombreEnPlayerPrefs && PlayerPrefs.HasKey(playerPrefsKey))
            {
                string n = PlayerPrefs.GetString(playerPrefsKey, "");
                if (!string.IsNullOrWhiteSpace(n))
                {
                    downloader?.SetNombre(n);
                    inputNameTMP?.SetTextWithoutNotify(n);
                    ShowDownloadPhase();
                    return;
                }
            }
            ShowNamePhase();
        }
    }

    void OpenWindow()
    {
        if (!panelVentanaCertificado) return;
        panelVentanaCertificado.SetActive(true);

        // Si ya hay nombre, pasa directo a fase descarga
        var n = GetCurrentName();
        if (string.IsNullOrWhiteSpace(n))
        {
            var cert = ProgressCore.I?.Data?.certificate;
            if (!string.IsNullOrWhiteSpace(cert?.name)) n = cert.name;
        }

        if (string.IsNullOrWhiteSpace(n)) ShowNamePhase();
        else ShowDownloadPhase();
    }

    void CloseWindow()
    {
        if (!panelVentanaCertificado) return;
        panelVentanaCertificado.SetActive(false);
    }

    void OpenSurvey()
    {
        if (string.IsNullOrEmpty(encuestaURL)) return;
        Application.OpenURL(encuestaURL);
        // Si algún día quieres guardar esto, podrías añadir:
        // ProgressCore.I?.MarkSurveyOpened(); (si implementas ese flag)
    }

    void OnSubmitName()
    {
        string nombre = GetCurrentName();
        if (string.IsNullOrWhiteSpace(nombre))
        {
            Debug.LogWarning("[Certificado] Ingresa un nombre válido.");
            return;
        }

        // Entregar el nombre al generador/descargador
        if (downloader != null) downloader.SetNombre(nombre);

        // Guardar en progreso y opcionalmente en PlayerPrefs
        ProgressCore.I?.SetCertificateName(nombre);

        if (guardarNombreEnPlayerPrefs)
        {
            PlayerPrefs.SetString(playerPrefsKey, nombre);
            PlayerPrefs.Save();
        }

        ShowDownloadPhase();
    }

    void DownloadAndTrack()
    {
        // 1) Asegurar nombre
        string nombre = GetCurrentName();
        if (string.IsNullOrWhiteSpace(nombre))
        {
            // intenta leer el del downloader si ya lo tenía
            nombre = downloader != null ? (downloader.nombrePersona ?? "").Trim() : "";
        }

        if (string.IsNullOrWhiteSpace(nombre))
        {
            Debug.LogWarning("[Certificado] Ingresa un nombre antes de descargar.");
            return;
        }

        // 2) Preparar downloader y exportar
        downloader?.SetNombre(nombre);
        downloader?.ExportAsPng(); // tu implementación de descarga

        // 3) Persistir y enviar métrica mínima
        ProgressCore.I?.SetCertificateName(nombre);
        ProgressCore.I?.MarkCertificateDownloaded();
        MetricsClient.I?.TrackCertificadoDescargado(nombre, true);
    }

    string GetCurrentName()
    {
        return inputNameTMP != null ? inputNameTMP.text.Trim() : "";
    }

    void ShowNamePhase()
    {
        if (panelIngresarNombre) panelIngresarNombre.SetActive(true);
        if (panelDescargarCertificado) panelDescargarCertificado.SetActive(false);
    }

    void ShowDownloadPhase()
    {
        if (panelIngresarNombre) panelIngresarNombre.SetActive(false);
        if (panelDescargarCertificado) panelDescargarCertificado.SetActive(true);

        // Previsualización dinámica (opcional)
        if (generarPreviewDinamica && imagePrevisualizacionCertificado && downloader)
        {
            var tex = RenderPreviewFromDownloader(downloader);
            if (tex != null)
            {
                var spr = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                imagePrevisualizacionCertificado.sprite = spr;
                imagePrevisualizacionCertificado.preserveAspect = true;
            }
        }
    }

    /// <summary>
    /// Renderiza el certificado offscreen (como hace el export) y devuelve un Texture2D para la previsualización.
    /// No descarga, solo genera la imagen.
    /// </summary>
    Texture2D RenderPreviewFromDownloader(CertificateDownloader d)
    {
        if (d.exportCamera == null) return null;

        // Asegura que el panel del certificado esté visible para renderizar
        bool wasActive = d.certificateRoot != null && d.certificateRoot.activeSelf;
        if (d.certificateRoot != null) d.certificateRoot.SetActive(true);

        var rt = new RenderTexture(d.width, d.height, 24, RenderTextureFormat.ARGB32);
        var prevTarget = d.exportCamera.targetTexture;
        var prevActive = RenderTexture.active;

        d.exportCamera.targetTexture = rt;
        RenderTexture.active = rt;
        d.exportCamera.Render();

        var tex = new Texture2D(d.width, d.height, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, d.width, d.height), 0, 0, false);
        tex.Apply();

        d.exportCamera.targetTexture = prevTarget;
        RenderTexture.active = prevActive;
        Destroy(rt);

        // Si el certificado no estaba visible antes, vuelve a ocultarlo si corresponde
        if (!wasActive && d.certificateRoot != null && d.hideRootAfterExport)
            d.certificateRoot.SetActive(false);

        return tex;
    }
}
