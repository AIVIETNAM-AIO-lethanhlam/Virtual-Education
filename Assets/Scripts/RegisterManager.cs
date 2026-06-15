using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.Networking;
using System.Collections;

using Firebase;
using Firebase.Auth;
using Firebase.Extensions;
using Google;

public class RegisterManager : MonoBehaviour
{
    [Header("Google Login")]
    public string googleWebClientId = "770601229175-l7cmu917a3ooe02d3mvfofs3tg6sqnvc.apps.googleusercontent.com";

    private FirebaseAuth auth;

    [Header("Input Fields")]
    public TMP_InputField fullNameInput;
    public TMP_InputField usernameInput;
    public TMP_InputField emailInput;
    public TMP_InputField passwordInput;
    public TMP_InputField confirmPasswordInput;
    public TMP_InputField phoneInput;

    [Header("Message Text")]
    public TMP_Text messageText;

    private void Start()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.Result == DependencyStatus.Available)
            {
                auth = FirebaseAuth.DefaultInstance;

                GoogleSignIn.Configuration = new GoogleSignInConfiguration
                {
                    WebClientId = googleWebClientId,
                    RequestIdToken = true,
                    RequestEmail = true
                };

                ShowMessage("");
                Debug.Log("Firebase Google Register ready.");
            }
            else
            {
                ShowMessage("Firebase chưa sẵn sàng: " + task.Result);
            }
        });
    }

    // =========================
    // NORMAL REGISTER
    // =========================
    public void Register()
    {
        string fullName = fullNameInput.text.Trim();
        string username = usernameInput.text.Trim();
        string email = emailInput.text.Trim();
        string password = passwordInput.text;
        string confirmPassword = confirmPasswordInput.text;
        string phone = phoneInput.text.Trim();

        if (fullName == "" || username == "" || email == "" ||
            password == "" || confirmPassword == "" || phone == "")
        {
            ShowMessage("Vui lòng nhập đầy đủ thông tin.");
            return;
        }

        if (password != confirmPassword)
        {
            ShowMessage("Mật khẩu xác nhận không khớp.");
            return;
        }

        StartCoroutine(RegisterCoroutine(
            fullName,
            username,
            email,
            password,
            confirmPassword,
            phone
        ));
    }

    private IEnumerator RegisterCoroutine(
        string fullName,
        string username,
        string email,
        string password,
        string confirmPassword,
        string phone
    )
    {
        if (APIManager.Instance == null)
        {
            ShowMessage("Lỗi: APIManager.Instance null");
            yield break;
        }

        string url = APIManager.Instance.GetUrl("/api/register");

        string jsonData = JsonUtility.ToJson(new RegisterData
        {
            full_name = fullName,
            username = username,
            email = email,
            password = password,
            confirm_password = confirmPassword,
            phone_number = phone
        });

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);

            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("ngrok-skip-browser-warning", "true");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                ShowMessage("Đăng ký thành công!");
                yield return new WaitForSeconds(1f);
                SceneManager.LoadScene("LoginScene");
            }
            else
            {
                ShowMessage("Đăng ký thất bại!\n" + request.downloadHandler.text);
            }
        }
    }

    public void GoToLoginScene()
    {
        SceneManager.LoadScene("LoginScene");
    }

    // =========================
    // GOOGLE REGISTER / LOGIN
    // =========================
    public void LoginWithGoogle()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (auth == null)
        {
            ShowMessage("Firebase Auth chưa sẵn sàng.");
            return;
        }

        ShowMessage("B1: Bắt đầu Google Register");

        GoogleSignIn.Configuration = new GoogleSignInConfiguration
        {
            WebClientId = googleWebClientId,
            RequestIdToken = true,
            RequestEmail = true
        };
        GoogleSignIn.DefaultInstance.SignIn().ContinueWithOnMainThread(task =>
        {
            ShowMessage("B2: Google callback");

            if (task.IsCanceled)
            {
                ShowMessage("User đã hủy Google Register");
                return;
            }

            if (task.IsFaulted)
            {
                ShowMessage("Google Sign-In thất bại");
                return;
            }

            GoogleSignInUser googleUser = task.Result;

            if (googleUser == null || string.IsNullOrEmpty(googleUser.IdToken))
            {
                ShowMessage("Google user hoặc IdToken bị null");
                return;
            }

            ShowMessage("B3: Lấy Google user OK");

            Credential credential = GoogleAuthProvider.GetCredential(
                googleUser.IdToken,
                null
            );

            ShowMessage("B4: Đang đăng nhập Firebase Auth");

            auth.SignInAndRetrieveDataWithCredentialAsync(credential)
                .ContinueWithOnMainThread(authTask =>
                {
                    ShowMessage("B5: Firebase callback");

                    if (authTask.IsCanceled)
                    {
                        ShowMessage("Firebase Auth bị hủy");
                        return;
                    }

                    if (authTask.IsFaulted)
                    {
                        ShowMessage("Firebase Auth thất bại");
                        return;
                    }

                    FirebaseUser firebaseUser = authTask.Result.User;

                    if (firebaseUser == null)
                    {
                        ShowMessage("firebaseUser null");
                        return;
                    }

                    ShowMessage("B6: Firebase Auth OK");

                    StartCoroutine(SyncGoogleUserToBackend(firebaseUser));
                });
        });
#else
        ShowMessage("Google Register chỉ test được trên Android thật.");
#endif
    }

    private IEnumerator SyncGoogleUserToBackend(FirebaseUser firebaseUser)
    {
        if (APIManager.Instance == null)
        {
            ShowMessage("Lỗi: APIManager.Instance null");
            yield break;
        }

        string url = APIManager.Instance.GetUrl("/api/google-login");

        GoogleLoginData data = new GoogleLoginData
        {
            firebase_uid = firebaseUser.UserId,
            email = firebaseUser.Email ?? "",
            full_name = firebaseUser.DisplayName ?? "",
            avatar_url = firebaseUser.PhotoUrl != null ? firebaseUser.PhotoUrl.ToString() : ""
        };

        string json = JsonUtility.ToJson(data);

        ShowMessage("B7: Gọi backend");

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);

            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("ngrok-skip-browser-warning", "true");
            request.timeout = 20;

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                ShowMessage("Backend fail: " + request.responseCode + "\n" + request.downloadHandler.text);
                yield break;
            }

            GoogleLoginResponse response =
                JsonUtility.FromJson<GoogleLoginResponse>(request.downloadHandler.text);

            if (response == null || !response.success || response.user == null)
            {
                ShowMessage("Backend trả về lỗi.");
                yield break;
            }

            PlayerPrefs.SetInt("is_logged_in", 1);
            PlayerPrefs.SetString("user_id", response.user.user_id ?? "");
            PlayerPrefs.SetString("username", response.user.username ?? "");
            PlayerPrefs.SetString("email", response.user.email ?? "");
            PlayerPrefs.SetString("full_name", response.user.full_name ?? "");
            PlayerPrefs.SetString("user_img", response.user.user_img ?? "");
            PlayerPrefs.SetString("role", "student");
            PlayerPrefs.Save();

            ShowMessage("B8: Đăng ký Google thành công");

            SceneManager.LoadScene("new_home_scene_user");
        }
    }

    private void ShowMessage(string msg)
    {
        if (messageText != null)
            messageText.text = msg;

        Debug.Log(msg);
    }

    [System.Serializable]
    public class RegisterData
    {
        public string full_name;
        public string username;
        public string email;
        public string password;
        public string confirm_password;
        public string phone_number;
    }

    [System.Serializable]
    public class GoogleLoginData
    {
        public string firebase_uid;
        public string email;
        public string full_name;
        public string avatar_url;
    }

    [System.Serializable]
    public class GoogleLoginResponse
    {
        public bool success;
        public string message;
        public GoogleLoginUser user;
    }

    [System.Serializable]
    public class GoogleLoginUser
    {
        public string user_id;
        public string full_name;
        public string username;
        public string email;
        public string user_img;
    }
}