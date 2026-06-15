// using UnityEngine;
// using UnityEngine.SceneManagement;

// public class HomeNavigationManager : MonoBehaviour
// {
//     [Header("Reset login when opening public home")]
//     public bool resetLoginOnStart = false;

//     [Header("Scene Names")]
//     public string registerSceneName = "RegisterScene";
//     public string loginSceneName = "LoginScene";
//     public string userInfoSceneName = "UserInfo";
//     public string manageClassSceneName = "ManageClassScene";
//     public string publicHomeSceneName = "new_home_scene";

//     private void Start()
//     {
//         if (resetLoginOnStart)
//         {
//             ClearLoginSession();
//         }
//     }

//     public void GoToRegisterScene()
//     {
//         SceneManager.LoadScene(registerSceneName);
//     }

//     public void GoToLoginScene()
//     {
//         SceneManager.LoadScene(loginSceneName);
//     }

//     public void GoToUserInfoScene()
//     {
//         if (IsLoggedIn())
//         {
//             SceneManager.LoadScene(userInfoSceneName);
//         }
//         else
//         {
//             SceneManager.LoadScene(loginSceneName);
//         }
//     }

//     public void GoToTeacherOption()
//     {
//         GoToManageClassWithRole("teacher");
//     }

//     public void GoToStudentOption()
//     {
//         GoToManageClassWithRole("student");
//     }

//     private void GoToManageClassWithRole(string role)
//     {
//         Debug.Log("Clicked role button: " + role);

//         if (!IsLoggedIn())
//         {
//             Debug.LogWarning("User chưa đăng nhập. Chuyển sang LoginScene.");

//             PlayerPrefs.SetString("pending_role", role);
//             PlayerPrefs.Save();

//             SceneManager.LoadScene(loginSceneName);
//             return;
//         }

//         PlayerPrefs.SetString("current_role", role);
//         PlayerPrefs.Save();

//         Debug.Log("User đã đăng nhập. current_role = " + role);
//         SceneManager.LoadScene(manageClassSceneName);
//     }

//     public void Logout()
//     {
//         ClearLoginSession();
//         SceneManager.LoadScene(publicHomeSceneName);
//     }

//     private bool IsLoggedIn()
//     {
//         int loginStatus = PlayerPrefs.GetInt("is_logged_in", 0);
//         string userId = PlayerPrefs.GetString("user_id", "");

//         Debug.Log("is_logged_in = " + loginStatus);
//         Debug.Log("user_id = " + userId);

//         if (loginStatus != 1)
//         {
//             return false;
//         }

//         if (string.IsNullOrEmpty(userId))
//         {
//             return false;
//         }

//         return true;
//     }

//     private void ClearLoginSession()
//     {
//         PlayerPrefs.DeleteKey("is_logged_in");
//         PlayerPrefs.DeleteKey("user_id");
//         PlayerPrefs.DeleteKey("username");
//         PlayerPrefs.DeleteKey("email");
//         PlayerPrefs.DeleteKey("full_name");
//         PlayerPrefs.DeleteKey("phone_number");
//         PlayerPrefs.DeleteKey("avatar_url");
//         PlayerPrefs.DeleteKey("role");
//         PlayerPrefs.DeleteKey("current_role");
//         PlayerPrefs.DeleteKey("pending_role");
//         PlayerPrefs.DeleteKey("google_id_token");

//         PlayerPrefs.Save();

//         Debug.Log("Login session cleared.");
//     }
// }

using UnityEngine;
using UnityEngine.SceneManagement;

public class HomeNavigationManager : MonoBehaviour
{
    [Header("Reset login when opening public home")]
    public bool resetLoginOnStart = true;

    [Header("Scene Names")]
    public string registerSceneName = "RegisterScene";
    public string loginSceneName = "LoginScene";
    public string userInfoSceneName = "UserInfo";
    public string manageClassSceneName = "ManageClassScene";
    public string publicHomeSceneName = "new_home_scene";

    private void Start()
    {
        if (resetLoginOnStart)
        {
            Debug.Log("Public home opened -> reset login session.");
            DropdownPanelUserManager.ClearLoginSession();
        }
    }

    public void GoToRegisterScene()
    {
        SceneManager.LoadScene(registerSceneName);
    }

    public void GoToLoginScene()
    {
        SceneManager.LoadScene(loginSceneName);
    }

    public void GoToUserInfoScene()
    {
        if (IsLoggedIn())
        {
            SceneManager.LoadScene(userInfoSceneName);
        }
        else
        {
            SceneManager.LoadScene(loginSceneName);
        }
    }

    public void GoToTeacherOption()
    {
        GoToManageClassWithRole("teacher");
    }

    public void GoToStudentOption()
    {
        GoToManageClassWithRole("student");
    }

    private void GoToManageClassWithRole(string role)
    {
        Debug.Log("Clicked role button: " + role);

        PlayerPrefs.SetString("pending_role", role);
        PlayerPrefs.Save();

        if (!IsLoggedIn())
        {
            Debug.LogWarning("User chưa đăng nhập. Chuyển sang LoginScene.");
            SceneManager.LoadScene(loginSceneName);
            return;
        }

        PlayerPrefs.SetString("current_role", role);
        PlayerPrefs.Save();

        Debug.Log("User đã đăng nhập. current_role = " + role);
        SceneManager.LoadScene(manageClassSceneName);
    }

    public void Logout()
    {
        DropdownPanelUserManager.ClearLoginSession();
        SceneManager.LoadScene(publicHomeSceneName);
    }

    private bool IsLoggedIn()
    {
        int loginStatus = PlayerPrefs.GetInt("is_logged_in", 0);
        string userId = PlayerPrefs.GetString("user_id", "");

        Debug.Log("CHECK_LOGIN_HOME");
        Debug.Log("is_logged_in = " + loginStatus);
        Debug.Log("user_id = " + userId);

        return loginStatus == 1 && !string.IsNullOrEmpty(userId);
    }
}