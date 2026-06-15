using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.Networking;
using System.Collections;

using Firebase;
using Firebase.Auth;
using Firebase.Extensions;
using Google;

public class LoginManager : MonoBehaviour
{
    [Header("Google Login")]
    public string googleWebClientId = "770601229175-l7cmu917a3ooe02d3mvfofs3tg6sqnvc.apps.googleusercontent.com";

    private FirebaseAuth auth;
    private bool googleReady = false;
    private bool isGoogleSigningIn = false;
    private float googleLoginStartTime = 0f;

    [Header("Input Fields")]
    public TMP_InputField usernameOrEmailInput;
    public TMP_InputField passwordInput;

    [Header("Message Text")]
    public TMP_Text messageText;

    private void Start()
    {
        googleReady = true;
        isGoogleSigningIn = false;

        Debug.Log("LOGIN_SCENE_START");

        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            Debug.Log("FIREBASE_DEPENDENCY_RESULT = " + task.Result);

            if (task.Result == DependencyStatus.Available)
            {
                auth = FirebaseAuth.DefaultInstance;

                GoogleSignIn.Configuration = new GoogleSignInConfiguration
                {
                    WebClientId = googleWebClientId,
                    RequestIdToken = true,
                    RequestEmail = true,
                    UseGameSignIn = false
                };

                Debug.Log("LOGIN_SCENE: Firebase ready.");
                Debug.Log("LOGIN_SCENE: Current Firebase user = " + 
                    (auth.CurrentUser != null ? auth.CurrentUser.Email : "null"));

                ClearAllLoginKeys();
            }
            else
            {
                Debug.LogError("Firebase dependency error = " + task.Result);
                ShowMessage("Firebase chưa sẵn sàng.");
            }
        });
    }

    private IEnumerator ResetAllLoginSessionOnEnterLoginScene()
    {
        googleReady = false;
        ShowMessage("Đang reset phiên Google...");

        Debug.Log("LOGIN_SCENE: Reset started.");

        ClearAllLoginKeys();

        try
        {
            if (auth != null)
            {
                auth.SignOut();
                Debug.Log("LOGIN_SCENE: FirebaseAuth SignOut OK.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("LOGIN_SCENE: FirebaseAuth SignOut error = " + e.Message);
        }

        try
        {
            GoogleSignIn.DefaultInstance.SignOut();
            Debug.Log("LOGIN_SCENE: GoogleSignIn SignOut OK.");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("LOGIN_SCENE: GoogleSignIn SignOut error = " + e.Message);
        }

        yield return new WaitForSecondsRealtime(2f);

        googleReady = true;
        isGoogleSigningIn = false;

        Debug.Log("LOGIN_SCENE: Reset finished. googleReady = " + googleReady);
        ShowMessage("");
    }

    private void ClearAllLoginKeys()
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
        PlayerPrefs.DeleteKey("force_google_reset");
        PlayerPrefs.Save();
    }

    public void Login()
    {
        string account = usernameOrEmailInput.text.Trim();
        string password = passwordInput.text.Trim();

        if (string.IsNullOrEmpty(account) || string.IsNullOrEmpty(password))
        {
            ShowMessage("Vui lòng nhập đầy đủ thông tin.");
            return;
        }

        StartCoroutine(LoginCoroutine(account, password));
    }

    private IEnumerator LoginCoroutine(string account, string password)
    {
        string url = APIManager.Instance.GetUrl("/api/login");
        Debug.Log("Đang gửi request đăng nhập tới URL: " + url);

        LoginData loginData = new LoginData
        {
            account = account,
            password = password
        };

        string jsonData = JsonUtility.ToJson(loginData);

        UnityWebRequest request = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);

        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("ngrok-skip-browser-warning", "true");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("LỖI KẾT NỐI MẠNG / HTTP ERROR: " + request.error);
            Debug.LogError("Chi tiết từ server: " + request.downloadHandler.text);
            ShowMessage("Không thể kết nối đến Server. Xem Console log!");
        }

        string responseText = request.downloadHandler.text;
        Debug.Log("Backend trả về: " + responseText);
        LoginResponse response = JsonUtility.FromJson<LoginResponse>(request.downloadHandler.text);

        if (response == null || !response.success || response.user == null)
        {
            string serverMsg = (response != null && !string.IsNullOrEmpty(response.message)) 
                                ? response.message 
                                : "Đăng nhập thất bại (Không rõ lỗi).";
            ShowMessage("Đăng nhập thất bại.");
            yield break;
        }

        Debug.Log("Đăng nhập thành công! Chuyển scene...");
        SaveLoginData(response);
        SceneManager.LoadScene("new_home_scene_user");
    }

    public void LoginWithGoogle()
    {
        #if UNITY_ANDROID && !UNITY_EDITOR
            Debug.Log("GOOGLE_LOGIN_CLICK");
            Debug.Log("auth null = " + (auth == null));
            Debug.Log("isGoogleSigningIn = " + isGoogleSigningIn);
            Debug.Log("googleReady = " + googleReady);

            if (auth == null)
            {
                ShowMessage("Firebase Auth chưa sẵn sàng.");
                return;
            }

            if (isGoogleSigningIn)
            {
                if (Time.realtimeSinceStartup - googleLoginStartTime > 5f)
                {
                    Debug.LogWarning("GOOGLE_LOGIN: force reset stuck signing state.");
                    isGoogleSigningIn = false;
                }
                else
                {
                    ShowMessage("Google Login đang xử lý...");
                    return;
                }
            }

            StartCoroutine(GoogleLoginFlow());
        #else
            ShowMessage("Google Login chỉ test được trên Android thật");
        #endif
    }

    private IEnumerator GoogleLoginFlow()
    {
        isGoogleSigningIn = true;
        googleLoginStartTime = Time.realtimeSinceStartup;

        ShowMessage("B1: Reset Google trước khi mở popup...");
        Debug.Log("GOOGLE_LOGIN_FLOW_START");

        try
        {
            GoogleSignIn.DefaultInstance.SignOut();
            Debug.Log("GOOGLE_LOGIN: SignOut before SignIn OK");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("GOOGLE_LOGIN: SignOut before SignIn ERROR = " + e.Message);
        }

        yield return new WaitForSecondsRealtime(0.8f);

        ShowMessage("B2: Mở Google popup...");

        var signInTask = GoogleSignIn.DefaultInstance.SignIn();

        float timer = 0f;
        while (!signInTask.IsCompleted && timer < 30f)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        isGoogleSigningIn = false;

        if (!signInTask.IsCompleted)
        {
            ShowMessage("Google Login timeout.");
            Debug.LogError("GOOGLE_LOGIN_TIMEOUT");
            yield break;
        }

        if (signInTask.IsCanceled)
        {
            ShowMessage("Bạn đã hủy Google Login.");
            Debug.LogWarning("GOOGLE_LOGIN_CANCELED");
            yield break;
        }

        if (signInTask.IsFaulted)
        {
            ShowMessage("Google Login lỗi.");
            Debug.LogError("GOOGLE_LOGIN_ERROR: " + signInTask.Exception);
            yield break;
        }

        GoogleSignInUser googleUser = signInTask.Result;

        Debug.Log("GOOGLE_LOGIN_USER_EMAIL = " + googleUser.Email);
        Debug.Log("GOOGLE_LOGIN_USER_NAME = " + googleUser.DisplayName);

        if (googleUser == null || string.IsNullOrEmpty(googleUser.IdToken))
        {
            ShowMessage("Không lấy được Google IdToken.");
            yield break;
        }

        ShowMessage("B3: Google OK");

        Credential credential = GoogleAuthProvider.GetCredential(googleUser.IdToken, null);
        var authTask = auth.SignInAndRetrieveDataWithCredentialAsync(credential);

        while (!authTask.IsCompleted)
            yield return null;

        if (authTask.IsFaulted || authTask.IsCanceled)
        {
            ShowMessage("Firebase Auth lỗi.");
            Debug.LogError("FIREBASE_AUTH_ERROR: " + authTask.Exception);
            yield break;
        }

        FirebaseUser firebaseUser = authTask.Result.User;

        if (firebaseUser == null)
        {
            ShowMessage("firebaseUser null.");
            yield break;
        }

        Debug.Log("FIREBASE_USER_UID = " + firebaseUser.UserId);
        Debug.Log("FIREBASE_USER_EMAIL = " + firebaseUser.Email);

        StartCoroutine(SyncGoogleUserToBackend(firebaseUser));
    }

    private IEnumerator SyncGoogleUserToBackend(FirebaseUser firebaseUser)
    {
        string url = APIManager.Instance.GetUrl("/api/google-login");

        GoogleLoginData data = new GoogleLoginData
        {
            firebase_uid = firebaseUser.UserId,
            email = firebaseUser.Email ?? "",
            full_name = firebaseUser.DisplayName ?? "",
            avatar_url = firebaseUser.PhotoUrl != null ? firebaseUser.PhotoUrl.ToString() : ""
        };

        string json = JsonUtility.ToJson(data);

        UnityWebRequest request = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);

        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("ngrok-skip-browser-warning", "true");
        request.timeout = 20;

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            ShowMessage("Backend fail: " + request.responseCode);
            Debug.LogError(request.downloadHandler.text);
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
        PlayerPrefs.SetInt("is_google_login", 1);
        PlayerPrefs.SetString("user_id", response.user.user_id ?? "");
        PlayerPrefs.SetString("firebase_uid", firebaseUser.UserId ?? "");
        PlayerPrefs.SetString("username", response.user.username ?? "");
        PlayerPrefs.SetString("email", response.user.email ?? "");
        PlayerPrefs.SetString("full_name", response.user.full_name ?? "");
        PlayerPrefs.SetString("user_img", response.user.user_img ?? "");
        PlayerPrefs.SetString("avatar_url", response.user.user_img ?? "");
        PlayerPrefs.Save();

        SceneManager.LoadScene("new_home_scene_user");
    }

    private void SaveLoginData(LoginResponse response)
    {
        PlayerPrefs.SetInt("is_logged_in", 1);
        PlayerPrefs.SetInt("is_google_login", 0);

        PlayerPrefs.SetString("user_id", response.user.user_id ?? "");
        PlayerPrefs.SetString("username", response.user.username ?? "");
        PlayerPrefs.SetString("email", response.user.email ?? "");
        PlayerPrefs.SetString("full_name", response.user.full_name ?? "");
        PlayerPrefs.SetString("phone_number", response.user.phone_number ?? "");
        PlayerPrefs.SetString("avatar_url", response.user.avatar_url ?? "");

        PlayerPrefs.Save();
    }

    public void GoToRegisterScene()
    {
        SceneManager.LoadScene("RegisterScene");
    }

    private void ShowMessage(string msg)
    {
        if (messageText != null)
            messageText.text = msg;

        Debug.Log(msg);
    }

    [System.Serializable]
    public class LoginData
    {
        public string account;
        public string password;
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
    public class LoginResponse
    {
        public bool success;
        public string message;
        public UserData user;
    }

    [System.Serializable]
    public class UserData
    {
        public string user_id;
        public string full_name;
        public string username;
        public string email;
        public string phone_number;
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