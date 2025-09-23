// ProgressRemote.cs
using System.Collections;
using UnityEngine;

public class ProgressRemote : MonoBehaviour
{
    public static ProgressRemote I { get; private set; }

    [Header("Endpoints")]
    public string profilePath = "/game/profile";                    // PUT { avatar_id }
    public string walletPath = "/game/progress/presupuesto";       // PATCH { value } o { delta }
    public string scorePath = "/game/progress/puntaje";           // PATCH { value } o { delta }
    public string checkpointPath = "/game/world/checkpoint";           // PUT { scene,x,y,z }
    public string standPathTemplate = "/game/stands/{stand_id}";          // PUT { stand_id, type, phase, screens_viewed[], quiz_unlocked }
    public string standScreenAddPath = "/game/stands/{stand_id}/screens";  // POST { screen_id }
    public string quizResultPath = "/game/minigames/{id}/result";      // POST { score, stars, correct, total, ms }

    [Header("Opciones")]
    public bool walletUsesDelta = true;  // si el back expone /presupuesto con {delta}
    public bool scoreUsesDelta = true;  // idem para puntaje

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
    }

    // === Profile ===
    // dentro de ProgressRemote.cs
    public void UpdateProfileAvatar(string avatarId)
    {
        if (string.IsNullOrEmpty(avatarId)) return;
        if (string.IsNullOrEmpty(profilePath))
        {  // <- evita 405 si no configuraste endpoint
            Debug.Log("[ProgressRemote] profilePath vacío; no se envía.");
            return;
        }
        var payload = JsonUtility.ToJson(new AvatarDto { avatar_id = avatarId });
        StartCoroutine(ApiClient.PutJson(profilePath, payload, _ => { }, LogErr));
    }


    // === Wallet / Score ===
    public void SetWallet(int value)
        => StartCoroutine(ApiClient.PatchJson(walletPath, JsonUtility.ToJson(new IntDto { value = value }), _ => { }, LogErr));

    public void AddWallet(int delta)
        => StartCoroutine(ApiClient.PatchJson(walletPath, JsonUtility.ToJson(new IntDto { delta = delta }), _ => { }, LogErr));

    public void SetScore(int value)
        => StartCoroutine(ApiClient.PatchJson(scorePath, JsonUtility.ToJson(new IntDto { value = value }), _ => { }, LogErr));

    public void AddScore(int delta)
        => StartCoroutine(ApiClient.PatchJson(scorePath, JsonUtility.ToJson(new IntDto { delta = delta }), _ => { }, LogErr));

    // Helpers según flags
    public void UpdateWalletByChange(int oldValue, int newValue)
    {
        var diff = newValue - oldValue;
        if (walletUsesDelta) AddWallet(diff); else SetWallet(newValue);
    }
    public void UpdateScoreByChange(int oldValue, int newValue)
    {
        var diff = newValue - oldValue;
        if (scoreUsesDelta) AddScore(diff); else SetScore(newValue);
    }

    // === Checkpoint ===
    public void SaveCheckpoint(string scene, Vector3 pos)
    {
        var dto = new CheckpointDto { scene = scene, x = pos.x, y = pos.y, z = pos.z };
        StartCoroutine(ApiClient.PutJson(checkpointPath, JsonUtility.ToJson(dto), _ => { }, LogErr));
    }

    // === Stand ===
    public void UpdateStand(string standId, string standType, string phase, string[] screensViewed = null, bool? quizUnlocked = null)
    {
        var path = standPathTemplate.Replace("{stand_id}", standId);
        var dto = new StandDto { stand_id = standId, type = standType, phase = phase, screens_viewed = screensViewed, quiz_unlocked = quizUnlocked };
        StartCoroutine(ApiClient.PutJson(path, JsonUtility.ToJson(dto), _ => { }, LogErr));
    }

    public void AddStandScreen(string standId, string screenId)
    {
        var path = standScreenAddPath.Replace("{stand_id}", standId);
        var dto = new ScreenDto { screen_id = screenId };
        StartCoroutine(ApiClient.PostJson(path, JsonUtility.ToJson(dto), _ => { }, LogErr));
    }

    // === Quiz ===
    public void PostQuizResult(string minigameId, int score, int stars, int correct, int total, int ms)
    {
        var dto = new QuizDto { id = minigameId, score = score, stars = stars, correct = correct, total = total, ms = ms };
        StartCoroutine(ApiClient.PostJson(quizResultPath.Replace("{id}", minigameId), JsonUtility.ToJson(dto), _ => { }, LogErr));
    }

    void LogErr(long code, string err) => Debug.LogWarning($"[ProgressRemote] HTTP {code}: {err}");

    // DTOs
    [System.Serializable] struct AvatarDto { public string avatar_id; }
    [System.Serializable] struct IntDto { public int value; public int delta; }
    [System.Serializable] struct CheckpointDto { public string scene; public float x, y, z; }
    [System.Serializable] struct StandDto { public string stand_id; public string type; public string phase; public string[] screens_viewed; public bool? quiz_unlocked; }
    [System.Serializable] struct ScreenDto { public string screen_id; }
    [System.Serializable] struct QuizDto { public string id; public int score, stars, correct, total, ms; }
}
