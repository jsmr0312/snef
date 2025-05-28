using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class CharacterSelectorUI : MonoBehaviour
{
    public CharacterDatabase database;
    public Button[] characterButtons;
    public string nextSceneName = "Bosque"; // Cambia por el nombre real

    void Start()
    {
        for (int i = 0; i < characterButtons.Length; i++)
        {
            int index = i;
            characterButtons[i].onClick.AddListener(() => SelectCharacter(index));
        }
    }

    public void SelectCharacter(int index)
    {
        PlayerPrefs.SetInt("SelectedCharacter", index);
        Debug.Log("Seleccionaste al personaje: " + index);
    }

    public void GoToNextScene()
    {
        SceneManager.LoadScene(nextSceneName);
    }
}
