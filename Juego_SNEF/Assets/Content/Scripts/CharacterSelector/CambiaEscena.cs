using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CambiaEscena : MonoBehaviour
{
    public GameObject funciones;       // Objeto donde está el script GuardarPersonaje
    public string nombreEscena;        // Nombre de la escena a la que quieres cambiar

    public void cambiar()
    {
        funciones.GetComponent<GuardarPersonaje>().Guardar();
        SceneManager.LoadScene(nombreEscena); // Cambia a la escena indicada
    }
}
