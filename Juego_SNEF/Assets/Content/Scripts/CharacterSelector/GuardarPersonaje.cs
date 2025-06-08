using UnityEngine;

public class GuardarPersonaje : MonoBehaviour
{
    public bool personaje1;
    public bool personaje2;
    public bool personaje3;

    void Start()
    {
        // Cargar solo una vez al iniciar
        personaje1 = PlayerPrefs.GetInt("personaje1Select", 0) == 1;
        personaje2 = PlayerPrefs.GetInt("personaje2Select", 0) == 1;
        personaje3 = PlayerPrefs.GetInt("personaje3Select", 0) == 1;

        // Si ninguno está seleccionado, activar el 1 por defecto
        if (!personaje1 && !personaje2 && !personaje3)
        {
            personaje1 = true;
            Guardar();
        }

        MostrarSeleccionado();
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
        PlayerPrefs.Save(); // Asegura que se guarde inmediatamente

        MostrarSeleccionado();
    }

    void MostrarSeleccionado()
    {
        if (personaje1)
        {
            Debug.Log("Personaje 1 seleccionado");
        }
        else if (personaje2)
        {
            Debug.Log("Personaje 2 seleccionado");
        }
        else if (personaje3)
        {
            Debug.Log("Personaje 3 seleccionado");
        }
    }
}
