using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CargarPersonaje : MonoBehaviour
{
    public GameObject character1;
    public GameObject character2;
    public GameObject character3;

    void Start()
    {
        bool personaje1 = PlayerPrefs.GetInt("personaje1Select") == 1;
        bool personaje2 = PlayerPrefs.GetInt("personaje2Select") == 1;
        bool personaje3 = PlayerPrefs.GetInt("personaje3Select") == 1;

        if (personaje1)
        {
            character1.SetActive(true);
            Destroy(character2);
            Destroy(character3);
        }
        else if (personaje2)
        {
            character2.SetActive(true);
            Destroy(character1);
            Destroy(character3);
        }
        else if (personaje3)
        {
            character3.SetActive(true);
            Destroy(character1);
            Destroy(character2);
        }
    }
}

