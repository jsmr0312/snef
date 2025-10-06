using UnityEngine;
using StarterAssets;

public class CargarPersonaje : MonoBehaviour
{
    [Header("Personajes")]
    public GameObject character1, character2, character3, character4, character5, character6,
                      character7, character8, character9, character10, character11, character12;

    [Header("Integración con UI touch")]
    public string playerTag = "Player";
    public bool setPlayerTag = true;

    void Start()
    {
        SetAll(false);

        int idx = AvatarSelectionBridge.GetSelectedIndexFromPrefs();
        if (idx == 0) idx = AvatarSelectionBridge.GetSelectedIndexFromLegacyPrefs();

        GameObject selected = null;
        switch (idx)
        {
            case 1: selected = character1; break;
            case 2: selected = character2; break;
            case 3: selected = character3; break;
            case 4: selected = character4; break;
            case 5: selected = character5; break;
            case 6: selected = character6; break;
            case 7: selected = character7; break;
            case 8: selected = character8; break;
            case 9: selected = character9; break;
            case 10: selected = character10; break;
            case 11: selected = character11; break;
            case 12: selected = character12; break;
            default: break;
        }

        if (selected != null)
        {
            selected.SetActive(true);
            FocusSelected(selected); // ← AQUÍ el joysticks ya controla al elegido
        }
    }


    void SetAll(bool v)
    {
        character1.SetActive(v); character2.SetActive(v); character3.SetActive(v);
        character4.SetActive(v); character5.SetActive(v); character6.SetActive(v);
        character7.SetActive(v); character8.SetActive(v); character9.SetActive(v);
        character10.SetActive(v); character11.SetActive(v); character12.SetActive(v);
    }

    void FocusSelected(GameObject go)
    {
        if (go == null) return;

        // 1) Opcional: etiqueta "Player" solo al seleccionado (para Prefer Tag)
        if (setPlayerTag)
        {
            ClearPlayerTag();
            try { go.tag = playerTag; } catch { } // por si el tag no existe
        }

        // 2) Notificar al UICanvasControllerInput (joysticks) el target activo
        var ui = FindObjectOfType<StarterAssets.UICanvasControllerInput>(true);
        if (ui != null)
        {
            var sai = go.GetComponent<StarterAssetsInputs>();
            if (sai != null)
            {
                ui.RegisterTarget(sai);
                ui.SetTarget(sai);               // ← aquí queda como CurrentTarget
            }
        }
    }

    void ClearPlayerTag()
    {
        // Asegura que solo el activo lleve "Player"
        if (character1) character1.tag = "Untagged";
        if (character2) character2.tag = "Untagged";
        if (character3) character3.tag = "Untagged";
        if (character4) character4.tag = "Untagged";
        if (character5) character5.tag = "Untagged";
        if (character6) character6.tag = "Untagged";
        if (character7) character7.tag = "Untagged";
        if (character8) character8.tag = "Untagged";
        if (character9) character9.tag = "Untagged";
        if (character10) character10.tag = "Untagged";
        if (character11) character11.tag = "Untagged";
        if (character12) character12.tag = "Untagged";
    }
}
