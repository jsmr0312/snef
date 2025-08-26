using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ShopItemButton : MonoBehaviour
{
    public ShopItemData item;
    public ShopUIController shop;

    void Awake()
    {
        GetComponent<Button>().onClick.AddListener(() =>
        {
            if (shop != null && item != null)
                shop.SelectItem(item);
        });
    }
}
