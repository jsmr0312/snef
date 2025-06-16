using UnityEngine;

public class CargarPersonaje : MonoBehaviour
{
    [Header("Personajes")]
    public GameObject character1;
    public GameObject character2;
    public GameObject character3;

    void Start()
    {
        // Desactiva todos al arrancar
        character1.SetActive(false);
        character2.SetActive(false);
        character3.SetActive(false);

        // Activa sólo el seleccionado
        if (PlayerPrefs.GetInt("personaje1Select") == 1)
            character1.SetActive(true);
        else if (PlayerPrefs.GetInt("personaje2Select") == 1)
            character2.SetActive(true);
        else if (PlayerPrefs.GetInt("personaje3Select") == 1)
            character3.SetActive(true);
    }
}
