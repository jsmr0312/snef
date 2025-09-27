using UnityEngine;

public class StandContext : MonoBehaviour
{
    public static StandContext I { get; private set; }

    [Header("Último stand activo")]
    public string standId;
    public string standNumber;
    public string ecosystemName;
    public string lastMiniGameId;

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SetCurrentStand(string id, string number, string ecosystem)
    {
        standId = id; standNumber = number; ecosystemName = ecosystem;
    }

    public void SetMiniGame(string miniGameId)
    {
        lastMiniGameId = miniGameId;
    }
}
