using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class MinigameBannerByScope : MonoBehaviour
{
    public enum KeySource { StandId, StandNumber, EcosystemName, MinigameId, MinigameName }

    [System.Serializable]
    public struct Mapping
    {
        public string key;   // ej. "stand_fintech_01"  (o el que uses)
        public Sprite sprite;
    }

    [Header("Destino (se auto-detecta si los dejas vacíos)")]
    public SpriteRenderer targetSpriteRenderer;    // Para SpriteRenderer (como tu BannerSNEF)
    public Image targetUIImage;                    // Por si algún banner fuera UI (Image)

    [Header("De qué campo tomar la clave")]
    public KeySource keySource = KeySource.StandId;
    public bool ignoreCase = true;
    public bool trim = true;

    [Header("Tabla de mapeo clave → sprite")]
    public List<Mapping> table = new List<Mapping>();
    public Sprite defaultSprite;                   // Opcional, si no hay match

    [Header("Debug")]
    public bool log;

    void Awake() { Apply(); }
    void OnEnable() { Apply(); }

    [ContextMenu("Apply now")]
    public void Apply()
    {
        // Autodetección del destino
        if (!targetSpriteRenderer) targetSpriteRenderer = GetComponent<SpriteRenderer>();
        if (!targetUIImage) targetUIImage = GetComponent<Image>();

        var scope = MinigameScope.I;
        if (scope == null)
        {
            SetSprite(defaultSprite);
            if (log) Debug.Log("MinigameBannerByScope: no hay MinigameScope; uso default.", this);
            return;
        }

        string key = GetKey(scope);
        Sprite picked = Lookup(key) ?? defaultSprite;
        SetSprite(picked);

        if (log) Debug.Log($"MinigameBannerByScope: key '{key}' → sprite '{(picked ? picked.name : "null")}'", this);
    }

    string GetKey(MinigameScope s)
    {
        string k = keySource switch
        {
            KeySource.StandId => s.standId,
            KeySource.StandNumber => s.standNumber,
            KeySource.EcosystemName => s.ecosystemName,
            KeySource.MinigameId => s.minigameId,
            KeySource.MinigameName => s.minigameName,
            _ => ""
        };
        if (k == null) k = "";
        if (trim) k = k.Trim();
        if (ignoreCase) k = k.ToLowerInvariant();
        return k;
    }

    Sprite Lookup(string key)
    {
        for (int i = 0; i < table.Count; i++)
        {
            string t = table[i].key ?? "";
            if (trim) t = t.Trim();
            if (ignoreCase) t = t.ToLowerInvariant();
            if (t == key) return table[i].sprite;
        }
        return null;
    }

    void SetSprite(Sprite s)
    {
        if (targetSpriteRenderer) targetSpriteRenderer.sprite = s;
        if (targetUIImage) targetUIImage.sprite = s;
    }
}
