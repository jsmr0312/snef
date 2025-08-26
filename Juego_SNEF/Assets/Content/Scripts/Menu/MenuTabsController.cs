using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class MenuTabsController : MonoBehaviour
{
    [System.Serializable]
    public class Tab
    {
        [Tooltip("Nombre solo para referencia")]
        public string name;
        [Tooltip("Botón que activa esta pestaña")]
        public Button button;
        [Tooltip("Panel (contenedor) que se muestra al activar")]
        public GameObject panel;
        [Tooltip("Imagen del botón (para resaltar activo). Opcional")]
        public Image buttonImage;
    }

    [Header("Pestañas")]
    public Tab[] tabs;

    [Header("Inicio")]
    [Tooltip("Índice de pestaña activa al empezar")]
    public int defaultTabIndex = 0;
    [Tooltip("Recordar la última pestaña usada")]
    public bool rememberLastTab = true;

    [Header("Resaltado del botón activo (opcional)")]
    public Color normalColor = Color.white;
    public Color activeColor = new Color32(255, 230, 0, 255);

    [Header("Transición de panel (opcional)")]
    public bool fadePanels = true;
    [Range(0.05f, 0.4f)] public float fadeDuration = 0.15f;

    int _current = -1;
    Coroutine _fadeRoutine;

    void Awake()
    {
        // Conectar botones
        for (int i = 0; i < tabs.Length; i++)
        {
            int idx = i;
            if (tabs[i].button != null)
                tabs[i].button.onClick.AddListener(() => ShowTab(idx));
        }

        // Activar pestaña inicial
        int startIndex = defaultTabIndex;
        if (rememberLastTab && PlayerPrefs.HasKey("LastMenuTab"))
            startIndex = PlayerPrefs.GetInt("LastMenuTab", defaultTabIndex);

        ShowTab(Mathf.Clamp(startIndex, 0, tabs.Length - 1), instant: true);
    }

    public void ShowTab(int index, bool instant = false)
    {
        if (index == _current) return;
        _current = index;

        // Guardar última pestaña (si aplica)
        if (rememberLastTab)
        {
            PlayerPrefs.SetInt("LastMenuTab", _current);
            PlayerPrefs.Save();
        }

        for (int i = 0; i < tabs.Length; i++)
        {
            bool active = (i == index);

            // Panel
            if (tabs[i].panel != null)
            {
                if (fadePanels && !instant)
                    DoFade(tabs[i].panel, active);
                else
                    tabs[i].panel.SetActive(active);
            }

            // Color del botón (si asignaste la Image)
            if (tabs[i].buttonImage != null)
                tabs[i].buttonImage.color = active ? activeColor : normalColor;
        }
    }

    void DoFade(GameObject panel, bool show)
    {
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(Fade(panel, show));
    }

    IEnumerator Fade(GameObject panel, bool show)
    {
        // Asegurar CanvasGroup
        var cg = panel.GetComponent<CanvasGroup>();
        if (cg == null) cg = panel.AddComponent<CanvasGroup>();

        panel.SetActive(true);

        float from = cg.alpha;
        float to = show ? 1f : 0f;
        float t = 0f;

        // Interacción solo si visible
        cg.blocksRaycasts = show;

        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / fadeDuration;
            cg.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }

        cg.alpha = to;
        panel.SetActive(show);
    }
}
