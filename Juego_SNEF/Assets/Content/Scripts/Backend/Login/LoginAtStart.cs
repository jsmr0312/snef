// Assets/Scripts/LoginAtStart.cs
using UnityEngine;
using TMPro;

public class LoginAtStart : MonoBehaviour
{
    [Header("Refs")]
    public APIClient api;                 // arrastra el componente con la base URL
    public TextMeshProUGUI usernameLabel; // opcional

    [Header("Credenciales de PRUEBA")]
    public string username = "bunchyta";
    public string password = "1234";

    void Start()
    {
        if (api == null)
        {
            Debug.LogError("[LoginAtStart] Falta APIClient en la escena.");
            return;
        }

        StartCoroutine(api.PostJson<LoginBody, LoginResponse>(
            "/game/login-player",
            new LoginBody { username = username, password = password },
            onOk: (res) =>
            {
                if (string.IsNullOrEmpty(res.token))
                {
                    Debug.LogError("[LoginAtStart] Respuesta sin token.");
                    return;
                }

                PlayerPrefs.SetString("auth_token", res.token);       // <<--- clave única
                PlayerPrefs.SetString("player_username", username);
                PlayerPrefs.Save();

                if (usernameLabel) usernameLabel.text = username;
                Debug.Log("[LoginAtStart] Token guardado. Len=" + res.token.Length);
            },
            onErr: (err) =>
            {
                Debug.LogError("[LoginAtStart] Error login: " + err);
                if (usernameLabel) usernameLabel.text = "(login falló)";
            }
        ));
    }
}
