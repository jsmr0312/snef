using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
    public Button descargarCertificadoButton; // (si no lo usa el downloader, déjalo null)
    public CertificateDownloader downloader;  // referencia al que ya tienes en escena

    void Awake()
    {
        // Ventana cerrada al inicio
        if (panelVentanaCertificado) panelVentanaCertificado.SetActive(false);

        openCertificateButton?.onClick.AddListener(OpenWindow);
        closePanelButton?.onClick.AddListener(CloseWindow);

        responderEncuestaButton?.onClick.AddListener(OpenSurvey);

        enviarNombreButton?.onClick.AddListener(OnSubmitName);

        // (Opcional) si quieres que el botón de "Descargar" llame al downloader
        if (descargarCertificadoButton != null && downloader != null)
            descargarCertificadoButton.onClick.AddListener(downloader.ExportAsPng);

        // Si hay nombre guardado, saltar directo a la fase de descarga
        if (guardarNombreEnPlayerPrefs && PlayerPrefs.HasKey(playerPrefsKey))
        {
            string n = PlayerPrefs.GetString(playerPrefsKey, "");
            if (!string.IsNullOrWhiteSpace(n))
            {
                downloader?.SetNombre(n);
                inputNameTMP?.SetTextWithoutNotify(n);
                ShowDownloadPhase();
            }
            else
            {
                ShowNamePhase();
            }
        }
        else
        {
            ShowNamePhase();
        }
    }

    void OpenWindow()
    {
        if (!panelVentanaCertificado) return;
        panelVentanaCertificado.SetActive(true);
        // Siempre que abras, si no hay nombre válido, muestra fase de nombre
        if (string.IsNullOrWhiteSpace(GetCurrentName()))
            ShowNamePhase();
        else
            ShowDownloadPhase();
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
    }

    void OnSubmitName()
    {
        string nombre = GetCurrentName();
        if (string.IsNullOrWhiteSpace(nombre))
        {
            // feedback mínimo: bordes rojos o log
            Debug.LogWarning("[Certificado] Ingresa un nombre válido.");
            return;
        }

        // Entregar el nombre al generador/descargador
        if (downloader != null)
            downloader.SetNombre(nombre); // <- usa tu método público del script existente

        if (guardarNombreEnPlayerPrefs)
        {
            PlayerPrefs.SetString(playerPrefsKey, nombre);
            PlayerPrefs.Save();
        }

        ShowDownloadPhase();
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

        // Si el certificado no estaba visible antes, vuelve a ocultarlo
        if (!wasActive && d.certificateRoot != null && d.hideRootAfterExport)
            d.certificateRoot.SetActive(false);

        return tex;
    }
}
