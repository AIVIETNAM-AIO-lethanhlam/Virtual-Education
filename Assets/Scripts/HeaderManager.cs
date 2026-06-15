using UnityEngine;
using UnityEngine.SceneManagement;

public class HeaderManager : MonoBehaviour
{
    [Header("Dropdown Panel")]
    public GameObject dropdownPanel;

    [Header("Settings")]
    public bool hideDropdownOnStart = true;
    public bool bringDropdownToFront = true;

    private void Start()
    {
        if (dropdownPanel != null && hideDropdownOnStart)
        {
            dropdownPanel.SetActive(false);
        }
    }

    public void ToggleDropdown()
    {
        if (dropdownPanel == null)
        {
            Debug.LogError("Dropdown Panel is not assigned on " + gameObject.name);
            return;
        }

        bool newState = !dropdownPanel.activeSelf;
        dropdownPanel.SetActive(newState);

        if (newState && bringDropdownToFront)
        {
            dropdownPanel.transform.SetAsLastSibling();
        }

        Debug.Log("Toggle dropdown: " + dropdownPanel.name + " = " + newState);
    }

    public void OpenDropdown()
    {
        if (dropdownPanel == null) return;

        dropdownPanel.SetActive(true);

        if (bringDropdownToFront)
            dropdownPanel.transform.SetAsLastSibling();
    }

    public void CloseDropdown()
    {
        if (dropdownPanel == null) return;

        dropdownPanel.SetActive(false);
    }

    public void GoToUserInfoScene()
    {
        CloseDropdown();

        if (IsLoggedIn())
            SceneManager.LoadScene("UserInfo");
        else
            SceneManager.LoadScene("LoginScene");
    }

    public void GoToLoginScene()
    {
        CloseDropdown();
        SceneManager.LoadScene("LoginScene");
    }

    public void GoToRegisterScene()
    {
        CloseDropdown();
        SceneManager.LoadScene("RegisterScene");
    }

    public void Logout()
    {
        CloseDropdown();

        PlayerPrefs.DeleteKey("is_logged_in");
        PlayerPrefs.DeleteKey("user_id");
        PlayerPrefs.DeleteKey("username");
        PlayerPrefs.DeleteKey("email");
        PlayerPrefs.DeleteKey("full_name");
        PlayerPrefs.DeleteKey("phone_number");
        PlayerPrefs.DeleteKey("avatar_url");
        PlayerPrefs.DeleteKey("role");
        PlayerPrefs.DeleteKey("current_role");
        PlayerPrefs.DeleteKey("google_id_token");

        PlayerPrefs.Save();

        SceneManager.LoadScene("new_home_scene");
    }

    private bool IsLoggedIn()
    {
        int loginStatus = PlayerPrefs.GetInt("is_logged_in", 0);
        string userId = PlayerPrefs.GetString("user_id", "");

        return loginStatus == 1 && !string.IsNullOrEmpty(userId);
    }
}