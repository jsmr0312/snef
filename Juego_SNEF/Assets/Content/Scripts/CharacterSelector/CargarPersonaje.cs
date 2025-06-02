using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CargarPersonaje : MonoBehaviour
{

    
    public GameObject character1;
    public GameObject character2;
    public GameObject character3;
    public bool personaje1;
    public bool personaje2;
    public bool personaje3;
    private void Update()
    {


    personaje1 = PlayerPrefs.GetInt("personaje1Select") == 1;
        personaje2 = PlayerPrefs.GetInt("personaje2Select") == 1;
        personaje3 = PlayerPrefs.GetInt("personaje3Select") == 1;
        
        if (personaje1 == true)
        {
            character1.SetActive(true);
            Destroy(character2);
            Destroy(character3);
        }

        if (personaje2 == true)
        {
            character2.SetActive(true);
            Destroy(character1);
            Destroy(character3);
        }
        
        if (personaje3 == true)
        {
            character3.SetActive(true);
            Destroy(character2);
            Destroy(character1);
        }
    }
}
