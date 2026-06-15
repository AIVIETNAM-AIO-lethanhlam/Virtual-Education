using UnityEngine;
using UnityEngine.SceneManagement;

public class DropdownPanelGuestManager : MonoBehaviour
{
    public string registerScene = "RegisterScene";
    public string loginScene = "LoginScene";

    public void GoToRegisterScene()
    {
        SceneManager.LoadScene(registerScene);
    }

    public void GoToLoginScene()
    {
        SceneManager.LoadScene(loginScene);
    }

    public void ClosePanel()
    {
        gameObject.SetActive(false);
    }
}