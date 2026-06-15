using UnityEngine;

public class APIManager : MonoBehaviour
{
    public static APIManager Instance;

    [Header("Backend URL")]
    public string baseUrl = "http://localhost:4000";
    // public string baseUrl = "http://192.168.1.68:3000";
    // public string baseUrl = "https://e571-14-241-225-251.ngrok-free.app";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public string GetUrl(string endpoint)
    {
        return baseUrl + endpoint;
    }
}