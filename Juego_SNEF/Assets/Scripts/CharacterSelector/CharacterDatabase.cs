using UnityEngine;

[CreateAssetMenu(fileName = "CharacterDatabase", menuName = "Character Selection/Database")]
public class CharacterDatabase : ScriptableObject
{
    public GameObject[] characterPrefabs;
}
