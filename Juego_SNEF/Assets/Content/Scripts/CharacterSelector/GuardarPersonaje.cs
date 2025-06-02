using UnityEngine;

public class GuardarPersonaje : MonoBehaviour
{

    public bool personaje1;
    public bool personaje2;
    public bool personaje3;

    private void Update()
    {
        if (personaje1 == false && personaje2 == false && personaje3 == false)
        {
            personaje1 = true;
        }
        personaje1 = PlayerPrefs.GetInt("personaje1Select") == 1;
        personaje2 = PlayerPrefs.GetInt("personaje2Select") == 1;
        personaje3 = PlayerPrefs.GetInt("personaje3Select") == 1;
    }

    public void Personaje1()
    {
        personaje1 = true;
        personaje2 = false;
        personaje3 = false;
        Guardar();
    }


    public void Personaje2()
    {
        personaje1 = false;
        personaje2 = true;
        personaje3 = false;
        Guardar();
    }


    public void Personaje3()
    {
        personaje1 = false;
        personaje2 = false;
        personaje3 = true;
        Guardar();
    }

    public void Guardar()
    {
        PlayerPrefs.SetInt("personaje1Select", personaje1 ? 1 : 0);
        PlayerPrefs.SetInt("personaje2Select", personaje2 ? 1 : 0);
        PlayerPrefs.SetInt("personaje3Select", personaje3 ? 1 : 0);

    }
    void Start()
    {
        
    }

  
}
