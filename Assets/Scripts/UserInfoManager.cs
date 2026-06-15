using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Text;

public class UserInfoManager : MonoBehaviour
{
    [Header("Profile")]
    public TMP_Text profileNameText;
    public TMP_Text profileEmailText;
    public Image avatarImage;
    public Sprite defaultAvatar;

    [Header("Input Fields")]
    public TMP_InputField fullNameInput;
    public TMP_InputField usernameInput;
    public TMP_InputField emailInput;
    public TMP_InputField passwordInput;
    public TMP_InputField phoneNumberInput;

    [Header("Edit Buttons")]
    public Button fullNameEditBtn;
    public Button usernameEditBtn;
    public Button emailEditBtn;
    public Button passwordEditBtn;
    public Button phoneEditBtn;

    [Header("Sign Out")]
    public Button signOutBtn;

    private string userId = "";

    private void Start()
    {
        userId = PlayerPrefs.GetString("user_id", "");

        if (string.IsNullOrEmpty(userId))
        {
            SceneManager.LoadScene("LoginScene");
            return;
        }

        SetAllInputsInteractable(false);

        if (fullNameEditBtn != null)
            fullNameEditBtn.onClick.AddListener(() => ToggleEdit("full_name", fullNameInput, fullNameEditBtn));

        if (usernameEditBtn != null)
            usernameEditBtn.onClick.AddListener(() => ToggleEdit("username", usernameInput, usernameEditBtn));

        if (emailEditBtn != null)
            emailEditBtn.onClick.AddListener(() => ToggleEdit("email", emailInput, emailEditBtn));

        if (passwordEditBtn != null)
            passwordEditBtn.onClick.AddListener(() => ToggleEdit("password", passwordInput, passwordEditBtn));

        if (phoneEditBtn != null)
            phoneEditBtn.onClick.AddListener(() => ToggleEdit("phone_number", phoneNumberInput, phoneEditBtn));

        if (signOutBtn != null) signOutBtn.onClick.AddListener(SignOut);

        StartCoroutine(GetUserInfo());
    }

    private IEnumerator GetUserInfo()
    {
        string url = APIManager.Instance.GetUrl("/api/users/" + userId);

        UnityWebRequest request = UnityWebRequest.Get(url);
        yield return request.SendWebRequest();

        string json = request.downloadHandler.text;
        Debug.Log("User JSON: " + json);

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError(json);
            yield break;
        }

        UserInfoResponse response = JsonUtility.FromJson<UserInfoResponse>(json);

        if (response == null || !response.success || response.user == null)
        {
            Debug.LogError("Invalid user data.");
            yield break;
        }

        ShowUserInfo(response.user);
    }

    private void ShowUserInfo(UserData user)
    {
        if (profileNameText != null) profileNameText.text = user.full_name;
        if (profileEmailText != null) profileEmailText.text = user.email;

        if (fullNameInput != null) fullNameInput.text = user.full_name;
        if (usernameInput != null) usernameInput.text = user.username;
        if (emailInput != null) emailInput.text = user.email;
        if (passwordInput != null) passwordInput.text = user.password;
        if (phoneNumberInput != null) phoneNumberInput.text = user.phone_number;

        if (avatarImage != null)
        {
            if (!string.IsNullOrEmpty(user.avatar_url))
                StartCoroutine(LoadAvatar(user.avatar_url));
            else if (defaultAvatar != null)
                avatarImage.sprite = defaultAvatar;
        }
    }

    private void ToggleEdit(string field, TMP_InputField input, Button editButton)
    {
        if (input == null || editButton == null) return;

        TMP_Text buttonText = editButton.GetComponentInChildren<TMP_Text>();

        if (!input.interactable)
        {
            input.interactable = true;
            input.Select();
            input.ActivateInputField();

            if (buttonText != null)
                buttonText.text = "Update";
        }
        else
        {
            input.interactable = false;

            if (buttonText != null)
                buttonText.text = "Edit";

            StartCoroutine(UpdateUserField(field, input.text.Trim()));
        }
    }

    private IEnumerator UpdateUserField(string field, string value)
    {
        string url = APIManager.Instance.GetUrl("/api/users/" + userId);

        UpdateUserRequest data = new UpdateUserRequest
        {
            field = field,
            value = value
        };

        string jsonData = JsonUtility.ToJson(data);

        UnityWebRequest request = new UnityWebRequest(url, "PUT");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);

        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        Debug.Log("Update URL: " + url);
        Debug.Log("Update JSON: " + jsonData);

        yield return request.SendWebRequest();

        string responseText = request.downloadHandler.text;
        Debug.Log("Update Response: " + responseText);

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Update failed: " + responseText);
            yield break;
        }

        if (field == "full_name")
        {
            PlayerPrefs.SetString("full_name", value);
            if (profileNameText != null) profileNameText.text = value;
        }
        else if (field == "username")
        {
            PlayerPrefs.SetString("username", value);
        }
        else if (field == "email")
        {
            PlayerPrefs.SetString("email", value);
            if (profileEmailText != null) profileEmailText.text = value;
        }
        else if (field == "phone_number")
        {
            PlayerPrefs.SetString("phone_number", value);
        }

        PlayerPrefs.Save();
    }

    private void SetAllInputsInteractable(bool value)
    {
        if (fullNameInput != null) fullNameInput.interactable = value;
        if (usernameInput != null) usernameInput.interactable = value;
        if (emailInput != null) emailInput.interactable = value;
        if (passwordInput != null) passwordInput.interactable = value;
        if (phoneNumberInput != null) phoneNumberInput.interactable = value;
    }

    private IEnumerator LoadAvatar(string imageUrl)
    {
        UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageUrl);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            if (defaultAvatar != null) avatarImage.sprite = defaultAvatar;
            yield break;
        }

        Texture2D texture = DownloadHandlerTexture.GetContent(request);

        avatarImage.sprite = Sprite.Create(
            texture,
            new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0.5f)
        );
    }

    public void SignOut()
    {
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

    [System.Serializable]
    public class UpdateUserRequest
    {
        public string field;
        public string value;
    }

    [System.Serializable]
    public class UserInfoResponse
    {
        public bool success;
        public UserData user;
    }

    [System.Serializable]
    public class UserData
    {
        public string user_id;
        public string full_name;
        public string username;
        public string email;
        public string password;
        public string phone_number;
        public string avatar_url;
        public string[] roles;
    }
}