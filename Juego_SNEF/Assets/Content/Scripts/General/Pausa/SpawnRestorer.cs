using UnityEngine;

public class SpawnRestorer : MonoBehaviour
{
    public GameObject player; // Asigna al jugador en el inspector

    void Start()
    {
        if (PlayerPrefs.HasKey("SavedX"))
        {
            float x = PlayerPrefs.GetFloat("SavedX");
            float y = PlayerPrefs.GetFloat("SavedY");
            float z = PlayerPrefs.GetFloat("SavedZ");

            player.transform.position = new Vector3(x, y, z);

            // Limpia la posición para evitar reusos innecesarios
            PlayerPrefs.DeleteKey("SavedX");
            PlayerPrefs.DeleteKey("SavedY");
            PlayerPrefs.DeleteKey("SavedZ");
            PlayerPrefs.DeleteKey("ReturnTo");
        }
    }
}
