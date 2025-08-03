using UnityEngine;

public class GuardarPersonaje : MonoBehaviour
{
    public bool personaje1;
    public bool personaje2;
    public bool personaje3;
    public bool personaje4;
    public bool personaje5;
    public bool personaje6;
    public bool personaje7;
    public bool personaje8;
    public bool personaje9;
    public bool personaje10;
    public bool personaje11;
    public bool personaje12;

    void Start()
    {
        // Cargar solo una vez al iniciar
        personaje1 = PlayerPrefs.GetInt("personaje1Select", 0) == 1;
        personaje2 = PlayerPrefs.GetInt("personaje2Select", 0) == 1;
        personaje3 = PlayerPrefs.GetInt("personaje3Select", 0) == 1;
        personaje4 = PlayerPrefs.GetInt("personaje4Select", 0) == 1;
        personaje5 = PlayerPrefs.GetInt("personaje5Select", 0) == 1;
        personaje6 = PlayerPrefs.GetInt("personaje6Select", 0) == 1;
        personaje7 = PlayerPrefs.GetInt("personaje7Select", 0) == 1;
        personaje8 = PlayerPrefs.GetInt("personaje8Select", 0) == 1;
        personaje9 = PlayerPrefs.GetInt("personaje9Select", 0) == 1;
        personaje10 = PlayerPrefs.GetInt("personaje10Select", 0) == 1;
        personaje11 = PlayerPrefs.GetInt("personaje11Select", 0) == 1;
        personaje12 = PlayerPrefs.GetInt("personaje12Select", 0) == 1;

        // Si ninguno está seleccionado, activar el 1 por defecto
        if (!personaje1 && !personaje2 && !personaje3 && !personaje4 &&
            !personaje5 && !personaje6 && !personaje7 && !personaje8 &&
            !personaje9 && !personaje10 && !personaje11 && !personaje12)
        {
            personaje1 = true;
            Guardar();
        }

        MostrarSeleccionado();
    }

    // Métodos públicos para seleccionar cada personaje:
    public void Personaje1() { Seleccionar(1); }
    public void Personaje2() { Seleccionar(2); }
    public void Personaje3() { Seleccionar(3); }
    public void Personaje4() { Seleccionar(4); }
    public void Personaje5() { Seleccionar(5); }
    public void Personaje6() { Seleccionar(6); }
    public void Personaje7() { Seleccionar(7); }
    public void Personaje8() { Seleccionar(8); }
    public void Personaje9() { Seleccionar(9); }
    public void Personaje10() { Seleccionar(10); }
    public void Personaje11() { Seleccionar(11); }
    public void Personaje12() { Seleccionar(12); }

    // Centraliza la lógica de selección de un solo personaje
    private void Seleccionar(int index)
    {
        // Primero, desactivar todos
        personaje1 = personaje2 = personaje3 = personaje4 =
        personaje5 = personaje6 = personaje7 = personaje8 =
        personaje9 = personaje10 = personaje11 = personaje12 = false;

        // Activar solo el elegido
        switch (index)
        {
            case 1: personaje1 = true; break;
            case 2: personaje2 = true; break;
            case 3: personaje3 = true; break;
            case 4: personaje4 = true; break;
            case 5: personaje5 = true; break;
            case 6: personaje6 = true; break;
            case 7: personaje7 = true; break;
            case 8: personaje8 = true; break;
            case 9: personaje9 = true; break;
            case 10: personaje10 = true; break;
            case 11: personaje11 = true; break;
            case 12: personaje12 = true; break;
        }

        Guardar();
    }

    public void Guardar()
    {
        PlayerPrefs.SetInt("personaje1Select", personaje1 ? 1 : 0);
        PlayerPrefs.SetInt("personaje2Select", personaje2 ? 1 : 0);
        PlayerPrefs.SetInt("personaje3Select", personaje3 ? 1 : 0);
        PlayerPrefs.SetInt("personaje4Select", personaje4 ? 1 : 0);
        PlayerPrefs.SetInt("personaje5Select", personaje5 ? 1 : 0);
        PlayerPrefs.SetInt("personaje6Select", personaje6 ? 1 : 0);
        PlayerPrefs.SetInt("personaje7Select", personaje7 ? 1 : 0);
        PlayerPrefs.SetInt("personaje8Select", personaje8 ? 1 : 0);
        PlayerPrefs.SetInt("personaje9Select", personaje9 ? 1 : 0);
        PlayerPrefs.SetInt("personaje10Select", personaje10 ? 1 : 0);
        PlayerPrefs.SetInt("personaje11Select", personaje11 ? 1 : 0);
        PlayerPrefs.SetInt("personaje12Select", personaje12 ? 1 : 0);
        PlayerPrefs.Save(); // Asegura que se guarde inmediatamente

        MostrarSeleccionado();
    }

    void MostrarSeleccionado()
    {
        if (personaje1) Debug.Log("Personaje 1 seleccionado");
        else if (personaje2) Debug.Log("Personaje 2 seleccionado");
        else if (personaje3) Debug.Log("Personaje 3 seleccionado");
        else if (personaje4) Debug.Log("Personaje 4 seleccionado");
        else if (personaje5) Debug.Log("Personaje 5 seleccionado");
        else if (personaje6) Debug.Log("Personaje 6 seleccionado");
        else if (personaje7) Debug.Log("Personaje 7 seleccionado");
        else if (personaje8) Debug.Log("Personaje 8 seleccionado");
        else if (personaje9) Debug.Log("Personaje 9 seleccionado");
        else if (personaje10) Debug.Log("Personaje 10 seleccionado");
        else if (personaje11) Debug.Log("Personaje 11 seleccionado");
        else if (personaje12) Debug.Log("Personaje 12 seleccionado");
    }
}
