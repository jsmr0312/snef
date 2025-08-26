using UnityEngine;

[CreateAssetMenu(menuName = "SNEF/Shop Item", fileName = "ShopItemData")]
public class ShopItemData : ScriptableObject
{
    [Header("Datos del artículo")]
    public string id = "consola";           // clave única para guardado
    public string displayName = "Consola";
    public int price = 1000;
    [TextArea] public string description = "Descripción...";
    public Sprite sprite;                    // imagen para la previsualización
}
