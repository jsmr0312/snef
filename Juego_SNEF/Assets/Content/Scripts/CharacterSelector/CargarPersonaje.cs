using UnityEngine;

public class CargarPersonaje : MonoBehaviour
{
    [Header("Personajes")]
    public GameObject character1;
    public GameObject character2;
    public GameObject character3;
    public GameObject character4;
    public GameObject character5;
    public GameObject character6;
    public GameObject character7;
    public GameObject character8;
    public GameObject character9;
    public GameObject character10;
    public GameObject character11;
    public GameObject character12;

    void Start()
    {
        // Desactiva todos al arrancar
        character1.SetActive(false);
        character2.SetActive(false);
        character3.SetActive(false);
        character4.SetActive(false);
        character5.SetActive(false);
        character6.SetActive(false);
        character7.SetActive(false);
        character8.SetActive(false);
        character9.SetActive(false);
        character10.SetActive(false);
        character11.SetActive(false);
        character12.SetActive(false);

        // Activa sólo el seleccionado en PlayerPrefs
        if (PlayerPrefs.GetInt("personaje1Select", 0) == 1)
            character1.SetActive(true);
        else if (PlayerPrefs.GetInt("personaje2Select", 0) == 1)
            character2.SetActive(true);
        else if (PlayerPrefs.GetInt("personaje3Select", 0) == 1)
            character3.SetActive(true);
        else if (PlayerPrefs.GetInt("personaje4Select", 0) == 1)
            character4.SetActive(true);
        else if (PlayerPrefs.GetInt("personaje5Select", 0) == 1)
            character5.SetActive(true);
        else if (PlayerPrefs.GetInt("personaje6Select", 0) == 1)
            character6.SetActive(true);
        else if (PlayerPrefs.GetInt("personaje7Select", 0) == 1)
            character7.SetActive(true);
        else if (PlayerPrefs.GetInt("personaje8Select", 0) == 1)
            character8.SetActive(true);
        else if (PlayerPrefs.GetInt("personaje9Select", 0) == 1)
            character9.SetActive(true);
        else if (PlayerPrefs.GetInt("personaje10Select", 0) == 1)
            character10.SetActive(true);
        else if (PlayerPrefs.GetInt("personaje11Select", 0) == 1)
            character11.SetActive(true);
        else if (PlayerPrefs.GetInt("personaje12Select", 0) == 1)
            character12.SetActive(true);
        // Si ninguno estaba en prefs, podrías activar uno por defecto aquí si lo deseas
    }
}
