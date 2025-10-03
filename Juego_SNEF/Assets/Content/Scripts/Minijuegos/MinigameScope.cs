using UnityEngine;

public class MinigameScope : MonoBehaviour
{
    public static MinigameScope I { get; private set; }

    [Header("Contexto activo")]
    public string standId;
    public string standNumber;
    public string ecosystemName;
    public string minigameId;     // ID base (el que pones en la Arcade)
    public string minigameName;   // Alias legible (opcional)

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
    }

    public void Begin(string standId, string standNumber, string ecosystemName,
                      string minigameId, string minigameName)
    {
        this.standId = standId;
        this.standNumber = standNumber;
        this.ecosystemName = ecosystemName;
        this.minigameId = minigameId;
        this.minigameName = minigameName;
    }

    // Clave compuesta para progreso/Stats
    public static string ScopedId(string baseMinigameId)
    {
        var s = I;
        string sId = (s && !string.IsNullOrEmpty(s.standId)) ? s.standId : "";
        string mId = string.IsNullOrEmpty(baseMinigameId) ? "minigame" : baseMinigameId;
        return string.IsNullOrEmpty(sId) ? mId : (sId + "::" + mId);
    }
}
