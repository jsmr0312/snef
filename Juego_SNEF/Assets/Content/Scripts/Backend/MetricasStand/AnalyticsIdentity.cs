using UnityEngine;
using System;

public class AnalyticsIdentity : MonoBehaviour
{
    public static string UserId { get; private set; }
    public static string SessionId { get; private set; }

    [Header("Opcional (solo pruebas)")]
    public bool generateAnonUserIfEmpty = true;

    void Awake()
    {
        DontDestroyOnLoad(gameObject);

        // UserId: lo ideal es setearlo al hacer login. Para pruebas generamos uno anónimo si no existe.
        UserId = PlayerPrefs.GetString("SNEF_UserId", string.Empty);
        if (string.IsNullOrEmpty(UserId) && generateAnonUserIfEmpty)
        {
            UserId = "anon-" + Guid.NewGuid().ToString();
            PlayerPrefs.SetString("SNEF_UserId", UserId);
        }

        // SessionId: nueva cada vez que arranca la app/escena principal
        BeginNewSession();
    }

    public static void SetUserId(string userId)
    {
        UserId = userId;
        PlayerPrefs.SetString("SNEF_UserId", UserId);
    }

    public static void BeginNewSession()
    {
        SessionId = Guid.NewGuid().ToString();
        PlayerPrefs.SetString("SNEF_SessionId", SessionId);
    }
}
