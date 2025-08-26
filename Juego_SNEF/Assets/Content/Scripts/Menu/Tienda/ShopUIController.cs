using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShopUIController : MonoBehaviour
{
    [Header("UI de la ficha izquierda")]
    public Image previewImage;
    public TextMeshProUGUI nombreText;
    public TextMeshProUGUI precioText;
    public TextMeshProUGUI descripcionText;
    public Button comprarButton;
    public TextMeshProUGUI comprarButtonLabel;

    [Header("Aspectos visuales")]
    public Color precioOK = Color.white;
    public Color precioNoAlcanza = new Color(0.9f, 0.2f, 0.2f);

    [Header("Selección inicial (opcional)")]
    public ShopItemData defaultItem;

    private ShopItemData _selected;

    void OnEnable()
    {
        if (comprarButton) comprarButton.onClick.AddListener(BuySelected);

        // Si Stats existe, suscríbete para refrescar el estado de compra cuando cambie el presupuesto
        if (Stats.I != null)
            Stats.I.OnPresupuestoChanged += _ => RefreshBuyState();

        if (defaultItem != null) SelectItem(defaultItem);
        else ClearPreview();

        RefreshBuyState();
    }

    void OnDisable()
    {
        if (comprarButton) comprarButton.onClick.RemoveListener(BuySelected);
        if (Stats.I != null)
            Stats.I.OnPresupuestoChanged -= _ => RefreshBuyState(); // safe aunque no coincida la ref
    }

    public void SelectItem(ShopItemData item)
    {
        _selected = item;

        if (previewImage) previewImage.sprite = item.sprite;
        if (nombreText) nombreText.text = item.displayName.ToUpper();
        if (precioText) precioText.text = $"${item.price}";
        if (descripcionText) descripcionText.text = item.description;

        RefreshBuyState();
    }

    public void BuySelected()
    {
        if (_selected == null || Stats.I == null) return;

        if (IsOwned(_selected))
        {
            Feedback("Ya lo tienes.");
            return;
        }

        // ¿Alcanza el presupuesto?
        if (Stats.I.Presupuesto < _selected.price)
        {
            Feedback("Presupuesto insuficiente.");
            return;
        }

        // Cobrar y guardar propiedad
        Stats.I.AddPresupuesto(-_selected.price);
        PlayerPrefs.SetInt($"shop_owned_{_selected.id}", 1);
        PlayerPrefs.Save();

        Feedback("¡Comprado!");
        RefreshBuyState();
    }

    private void RefreshBuyState()
    {
        if (_selected == null)
        {
            if (comprarButton) comprarButton.interactable = false;
            return;
        }

        bool owned = IsOwned(_selected);
        bool canAfford = Stats.I != null && Stats.I.Presupuesto >= _selected.price;

        if (precioText) precioText.color = canAfford ? precioOK : precioNoAlcanza;

        if (comprarButton) comprarButton.interactable = !owned;

        if (comprarButtonLabel)
            comprarButtonLabel.text = owned ? "COMPRADO" : "COMPRAR";
    }

    private bool IsOwned(ShopItemData item)
        => PlayerPrefs.GetInt($"shop_owned_{item.id}", 0) == 1;

    private void Feedback(string msg)
    {
        Debug.Log($"[Shop] {msg}");
    }

    private void ClearPreview()
    {
        if (previewImage) previewImage.sprite = null;
        if (nombreText) nombreText.text = "";
        if (precioText) precioText.text = "";
        if (descripcionText) descripcionText.text = "";
        if (comprarButton) comprarButton.interactable = false;
        if (comprarButtonLabel) comprarButtonLabel.text = "COMPRAR";
    }
}
