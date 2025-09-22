using UnityEngine;

public class CargarPersonaje : MonoBehaviour
{
    [Header("Personajes")]
    public GameObject character1, character2, character3, character4, character5, character6,
                      character7, character8, character9, character10, character11, character12;

    void Start()
    {
        SetAll(false);
        int idx = AvatarSelectionBridge.GetSelectedIndexFromPrefs(); // 1..12 (o 0 si no hay)
        if (idx == 0) idx = AvatarSelectionBridge.GetSelectedIndexFromLegacyPrefs(); // fallback

        switch (idx)
        {
            case 1: character1.SetActive(true); break;
            case 2: character2.SetActive(true); break;
            case 3: character3.SetActive(true); break;
            case 4: character4.SetActive(true); break;
            case 5: character5.SetActive(true); break;
            case 6: character6.SetActive(true); break;
            case 7: character7.SetActive(true); break;
            case 8: character8.SetActive(true); break;
            case 9: character9.SetActive(true); break;
            case 10: character10.SetActive(true); break;
            case 11: character11.SetActive(true); break;
            case 12: character12.SetActive(true); break;
            default: /* ninguno: puedes activar un default si quieres */ break;
        }
    }

    void SetAll(bool v)
    {
        character1.SetActive(v); character2.SetActive(v); character3.SetActive(v);
        character4.SetActive(v); character5.SetActive(v); character6.SetActive(v);
        character7.SetActive(v); character8.SetActive(v); character9.SetActive(v);
        character10.SetActive(v); character11.SetActive(v); character12.SetActive(v);
    }
}
