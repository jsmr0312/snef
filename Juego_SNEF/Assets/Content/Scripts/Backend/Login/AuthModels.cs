// Assets/Scripts/AuthModels.cs
[System.Serializable]
public class LoginBody
{
    public string username;
    public string password;
}

[System.Serializable]
public class LoginResponse
{
    public string token; // la API devuelve { "token": "..." }
}
