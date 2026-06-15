using UnityEngine;
using UnityEngine.SceneManagement;
using Firebase.Auth;
using Google;
using System.Collections;

public class DropdownPanelUserManager : MonoBehaviour
{
    [Header("Scene Names")]
    public string userInfoScene = "UserInfo";
    public string manageClassScene = "ManageClassScene";
    public string loginScene = "LoginScene";
    public string guestHomeScene = "new_home_scene";

    public void GoToUserInfoScene()
    {
        LoadSceneIfLoggedIn(userInfoScene);
    }

    public void GoToManageClassScene()
    {
        GoToManageClassWithRole("teacher");
    }

    public void GoToRegisteredClassScene()
    {
        GoToManageClassWithRole("student");
    }

    private void GoToManageClassWithRole(string role)
    {
        Debug.Log("Clicked dropdown role: " + role);

        PlayerPrefs.SetString("pending_role", role);
        PlayerPrefs.Save();

        if (!IsLoggedIn())
        {
            Debug.LogWarning("User chưa đăng nhập. Chuyển sang LoginScene.");
            SceneManager.LoadScene(loginScene);
            return;
        }

        PlayerPrefs.SetString("current_role", role);
        PlayerPrefs.Save();

        Debug.Log("User đã đăng nhập. current_role = " + role);
        SceneManager.LoadScene(manageClassScene);
    }

    public void Logout()
    {
        StartCoroutine(LogoutCoroutine());
    }

    private IEnumerator LogoutCoroutine()
    {
        Debug.Log("LOGOUT_START");

        ClearLoginSession();

        try
        {
            if (FirebaseAuth.DefaultInstance != null)
            {
                FirebaseAuth.DefaultInstance.SignOut();
                Debug.Log("LOGOUT: Firebase SignOut OK");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("LOGOUT: Firebase SignOut ERROR = " + e.Message);
        }

        try
        {
            GoogleSignIn.DefaultInstance.SignOut();
            Debug.Log("LOGOUT: Google SignOut OK");
        }
        catch (System.Exception e)
        {
            Debug.LogError("LOGOUT: Google SignOut ERROR = " + e.Message);
        }

        yield return new WaitForSecondsRealtime(0.5f);

        Debug.Log("LOGOUT_END -> Load guest scene");
        SceneManager.LoadScene(guestHomeScene);
    }

    public void ClosePanel()
    {
        gameObject.SetActive(false);
    }

    private void LoadSceneIfLoggedIn(string sceneName)
    {
        if (IsLoggedIn())
        {
            SceneManager.LoadScene(sceneName);
        }
        else
        {
            SceneManager.LoadScene(loginScene);
        }
    }

    private bool IsLoggedIn()
    {
        int loginStatus = PlayerPrefs.GetInt("is_logged_in", 0);
        string userId = PlayerPrefs.GetString("user_id", "");

        Debug.Log("CHECK_LOGIN_DROPDOWN");
        Debug.Log("is_logged_in = " + loginStatus);
        Debug.Log("user_id = " + userId);

        return loginStatus == 1 && !string.IsNullOrEmpty(userId);
    }

    public static void ClearLoginSession()
    {
        PlayerPrefs.DeleteKey("is_logged_in");
        PlayerPrefs.DeleteKey("is_google_login");

        PlayerPrefs.DeleteKey("user_id");
        PlayerPrefs.DeleteKey("firebase_uid");
        PlayerPrefs.DeleteKey("username");
        PlayerPrefs.DeleteKey("email");
        PlayerPrefs.DeleteKey("full_name");
        PlayerPrefs.DeleteKey("phone_number");
        PlayerPrefs.DeleteKey("avatar_url");
        PlayerPrefs.DeleteKey("user_img");

        PlayerPrefs.DeleteKey("role");
        PlayerPrefs.DeleteKey("current_role");
        PlayerPrefs.DeleteKey("pending_role");

        PlayerPrefs.DeleteKey("google_id_token");
        PlayerPrefs.DeleteKey("selected_class_id");
        PlayerPrefs.DeleteKey("selected_lesson_id");
        PlayerPrefs.DeleteKey("selected_quiz_id");

        PlayerPrefs.Save();

        Debug.Log("Login session cleared.");
    }
}