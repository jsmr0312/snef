using UnityEngine;
using TMPro;

public class UsernameBootstrap : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI usernameLabel;   // arrástralo desde tu Canvas
    [Header("Opcional")]
    public string loginUrl = "/login.html"; // para enviar al login si no hay sesión

    void Start()
    {
        // 1) Lee el username guardado por la página web
        var username = BrowserStorage.GetItem("player_username");

        // 2) Si existe, muéstralo
        if (!string.IsNullOrEmpty(username))
        {
            if (usernameLabel) usernameLabel.text = username;
            Debug.Log("Usuario autenticado: " + username);
        }
        else
        {
            Debug.LogWarning("No hay username en localStorage");
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!string.IsNullOrEmpty(loginUrl))
                Application.OpenURL(loginUrl);
#endif
        }
    }
}
