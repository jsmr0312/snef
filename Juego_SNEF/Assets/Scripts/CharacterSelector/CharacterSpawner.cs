using UnityEngine;

public class CharacterSpawner : MonoBehaviour
{
    public CharacterDatabase database;

    void Start()
    {
        int index = PlayerPrefs.GetInt("SelectedCharacter", 0); // Por si no hay uno seleccionado
        GameObject character = Instantiate(database.characterPrefabs[index], transform.position, Quaternion.identity);
        character.name = "Jugador";
    }
}
