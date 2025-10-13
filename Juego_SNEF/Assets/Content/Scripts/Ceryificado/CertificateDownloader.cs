using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Runtime.InteropServices;

public class CertificateDownloader : MonoBehaviour
{
    [Header("Datos")]
    [Tooltip("Nombre que quieres imprimir en el certificado (prueba desde el Inspector)")]
    public string nombrePersona = "Nombre Apellido";
    [Tooltip("Prefijo del archivo a descargar")]
    public string nombreArchivoPrefix = "Reconocimiento_";

    [Header("UI del Certificado")]
    [Tooltip("Raíz del certificado (panel con fondo y textos). Puede estar desactivado en escena.")]
    public GameObject certificateRoot;
    [Tooltip("Texto TMP donde se dibuja el nombre")]
    public TextMeshProUGUI nameTMP;

    [Header("Render Offscreen")]
    [Tooltip("Canvas del certificado (ponlo en Screen Space - Camera y asigna esta cámara)")]
    public Canvas certificateCanvas;
    [Tooltip("Cámara dedicada para renderizar solo el certificado")]
    public Camera exportCamera;
    [Tooltip("Resolución de salida")]
    public int width = 1920, height = 1080;

    [Header("UI Botón")]
    [Tooltip("Botón que disparará la descarga")]
    public Button downloadButton;
    public bool autoHookButton = true;

    [Header("Comportamiento")]
    public bool hideRootAfterExport = true;

#if UNITY_WEBGL && !UNITY_EDITOR
    // En WebGL llamamos a la función JS que descarga un Base64 como archivo
    [DllImport("__Internal")] private static extern void DownloadBase64File(string base64Data, string filename, string mimeType);
#endif

    void Awake()
    {
        if (downloadButton != null && autoHookButton)
            downloadButton.onClick.AddListener(ExportAsPng);
    }

    [ContextMenu("Export PNG (test)")]
    public void ExportAsPng()
    {
        // 1) Asegura nombre en el TMP
        if (nameTMP != null) nameTMP.text = nombrePersona;

        // 2) Asegura que el certificado está activo para que se renderice
        bool wasActive = certificateRoot != null && certificateRoot.activeSelf;
        if (certificateRoot != null) certificateRoot.SetActive(true);

        // 3) Render a RenderTexture
        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        var prevTarget = exportCamera.targetTexture;
        var prevActive = RenderTexture.active;

        exportCamera.targetTexture = rt;
        RenderTexture.active = rt;
        exportCamera.Render();

        // 4) Copiar a Texture2D y codificar a PNG
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
        tex.Apply();
        byte[] png = tex.EncodeToPNG();

        // 5) Limpieza
        exportCamera.targetTexture = prevTarget;
        RenderTexture.active = prevActive;
        Destroy(rt);
        Destroy(tex);

        // 6) Nombre de archivo seguro
        string file = MakeSafe($"{nombreArchivoPrefix}{nombrePersona}.png");

        // 7) Descargar / Guardar según plataforma
#if UNITY_WEBGL && !UNITY_EDITOR
        string b64 = System.Convert.ToBase64String(png);
        DownloadBase64File(b64, file, "image/png");
#else
        string path = System.IO.Path.Combine(Application.persistentDataPath, file);
        System.IO.File.WriteAllBytes(path, png);
        Debug.Log($"Certificado guardado en: {path}");
#endif

        // 8) Restaurar visibilidad si hacía falta
        if (hideRootAfterExport && certificateRoot != null && !wasActive)
            certificateRoot.SetActive(false);
    }

    public void SetNombre(string nuevo)
    {
        nombrePersona = nuevo;
        if (nameTMP != null) nameTMP.text = nuevo;
    }

    private string MakeSafe(string s)
    {
        foreach (char c in System.IO.Path.GetInvalidFileNameChars())
            s = s.Replace(c, '_');
        return s.Replace(' ', '_');
    }
}
