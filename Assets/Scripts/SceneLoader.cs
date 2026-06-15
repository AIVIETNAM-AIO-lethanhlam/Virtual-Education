using UnityEngine;
using UnityEngine.SceneManagement; 

public class SceneLoader : MonoBehaviour
{
    public void LoadRegisterScene()
    {
        SceneManager.LoadScene("RegisterScene"); 
    }

    public void LoadLoginScene()
    {
        SceneManager.LoadScene("LoginScene");
    }
}