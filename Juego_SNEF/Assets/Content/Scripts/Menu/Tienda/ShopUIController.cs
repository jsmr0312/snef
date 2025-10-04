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

        if (Stats.I != null)
            Stats.I.OnPresupuestoChanged += OnPresupuestoChanged; // <- handler con nombre

        if (defaultItem != null) SelectItem(defaultItem);
        else ClearPreview();

        RefreshBuyState();
    }

    void OnDisable()
    {
        if (comprarButton) comprarButton.onClick.RemoveListener(BuySelected);
        if (Stats.I != null)
            Stats.I.OnPresupuestoChanged -= OnPresupuestoChanged; // <- se desuscribe bien
    }

    void OnPresupuestoChanged(int _)
    {
        RefreshBuyState();
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
        if (_selected == null || Stats.I == null || ProgressCore.I == null) return;

        // ¿Ya lo tiene?
        if (ProgressCore.I.IsOwned(_selected.id))
        {
            Feedback("Ya lo tienes.");
            RefreshBuyState();
            return;
        }

        // ¿Alcanza el presupuesto?
        if (Stats.I.Presupuesto < _selected.price)
        {
            Feedback("Presupuesto insuficiente.");
            RefreshBuyState();
            return;
        }

        // 1) Cobrar en Stats
        Stats.I.AddPresupuesto(-_selected.price);

        // 2) Sincronizar ProgressCore con el nuevo total de Stats
        int oldBudget = ProgressCore.I.Progress.presupuesto; // capturar ANTES de setear
        ProgressCore.I.SetPresupuesto(Stats.I.Presupuesto);

        // 3) Guardado remoto (old → new)
        ProgressRemote.I?.UpdateWalletByChange(oldBudget, ProgressCore.I.Progress.presupuesto);

        // 4) Marcar propiedad
        bool added = ProgressCore.I.OwnItem(_selected.id);
        if (!added) { Feedback("Ya lo tenías."); RefreshBuyState(); return; }

        // 5) Logro "Coleccionista"
        int ownedCount = (ProgressCore.I?.Progress?.owned_items != null)
            ? ProgressCore.I.Progress.owned_items.Count
            : 0;
        AchievementsManager.I?.OnInventoryChanged(ownedCount);

        // 6) Logro "Ahorrador"
        AchievementsManager.I?.NotifyBudgetChanged(Stats.I.Presupuesto);

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

        bool owned = (ProgressCore.I != null) && ProgressCore.I.IsOwned(_selected.id);
        bool canAfford = (Stats.I != null) && Stats.I.Presupuesto >= _selected.price;

        if (precioText) precioText.color = canAfford ? precioOK : precioNoAlcanza;
        if (comprarButton) comprarButton.interactable = !owned;

        if (comprarButtonLabel)
            comprarButtonLabel.text = owned ? "COMPRADO" : "COMPRAR";
    }

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
