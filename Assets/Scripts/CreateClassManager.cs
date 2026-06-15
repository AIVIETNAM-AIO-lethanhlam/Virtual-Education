using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Firebase.Extensions;


#if UNITY_EDITOR
using UnityEditor;
#endif

public class CreateClassManager : MonoBehaviour
{
    [Header("Input Fields")]
    public TMP_InputField classNameInput;
    public TMP_InputField classInfoInput;
    public TMP_InputField teacherNameInput;

    [Header("Required Marks")]
    public TMP_Text classNameRequiredMark;
    public TMP_Text classInfoRequiredMark;

    [Header("Messages")]
    public TMP_Text classNameMessageText;
    public TMP_Text classInfoMessageText;

    [Header("Upload Image")]
    public Image uploadPreviewImage;
    public GameObject uploadButtonObject;

    [Header("API")]
    public string baseUrl = "http://localhost:4000/api";
    // public string baseUrl = "https://e571-14-241-225-251.ngrok-free.app/api";

    [Header("Scene")]
    public string manageClassSceneName = "ManageClassScene";

    [Header("Title And Button Text")]
    public TMP_Text titleText;
    public TMP_Text createButtonText;

    private string classImageUrl = "";
    private bool isUploadingImage = false;
    private string modelFilePath = "";
    private string modelFirebaseUrl = "";

    private bool isEditMode = false;
    private string editClassId = "";

    private bool hasSelectedImage = false;
    private Sprite defaultUploadBoxSprite;
    private Color defaultUploadBoxColor;

    private void Start()
    {
        if (uploadPreviewImage != null)
        {
            defaultUploadBoxSprite = uploadPreviewImage.sprite;
            defaultUploadBoxColor = uploadPreviewImage.color;
        }
        LoadCurrentTeacherName();

        SetupRequiredMark(classNameRequiredMark);
        SetupRequiredMark(classInfoRequiredMark);

        SetupMessage(classNameMessageText);
        SetupMessage(classInfoMessageText);
        CheckEditMode();
        UpdateUploadButtonState();
    }

    // private void CheckEditMode()
    // {
    //     isEditMode = PlayerPrefs.GetInt("is_edit_class", 0) == 1;

    //     if (!isEditMode)
    //     {
    //         if (titleText != null)
    //             titleText.text = "TẠO MỚI LỚP HỌC";

    //         if (createButtonText != null)
    //             createButtonText.text = "Tạo lớp học";

    //         return;
    //     }

    //     editClassId = PlayerPrefs.GetString("edit_class_id", "");

    //     if (titleText != null)
    //         titleText.text = "CẬP NHẬT LỚP HỌC";

    //     if (createButtonText != null)
    //         createButtonText.text = "Cập nhật";

    //     if (classNameInput != null)
    //         classNameInput.text = PlayerPrefs.GetString("edit_class_name", "");

    //     if (classInfoInput != null)
    //         classInfoInput.text = PlayerPrefs.GetString("edit_summary_info", "");

    //     classImageUrl = PlayerPrefs.GetString("edit_class_img_url", "");

    //     if (!string.IsNullOrEmpty(classImageUrl) && uploadPreviewImage != null)
    //     {
    //         StartCoroutine(LoadImageFromUrl(classImageUrl, uploadPreviewImage));
    //     }
    // }

    private void CheckEditMode()
    {
        isEditMode = PlayerPrefs.GetInt("is_edit_class", 0) == 1;

        if (!isEditMode)
        {
            editClassId = "";
            classImageUrl = "";
            hasSelectedImage = false;

            if (uploadPreviewImage != null)
            {
                uploadPreviewImage.sprite = defaultUploadBoxSprite;
                uploadPreviewImage.color = defaultUploadBoxColor;
            }

            if (titleText != null)
                titleText.text = "THÊM LỚP HỌC";

            if (createButtonText != null)
                createButtonText.text = "Tạo lớp học";

            if (classNameInput != null)
                classNameInput.text = "";

            if (classInfoInput != null)
                classInfoInput.text = "";

            UpdateUploadButtonState();
            return;
        }

        editClassId = PlayerPrefs.GetString("edit_class_id", "");

        if (titleText != null)
            titleText.text = "CẬP NHẬT LỚP HỌC";

        if (createButtonText != null)
            createButtonText.text = "Cập nhật";

        if (classNameInput != null)
            classNameInput.text = PlayerPrefs.GetString("edit_class_name", "");

        if (classInfoInput != null)
            classInfoInput.text = PlayerPrefs.GetString("edit_summary_info", "");

        classImageUrl = PlayerPrefs.GetString("edit_class_img_url", "");

        if (!string.IsNullOrEmpty(classImageUrl) && uploadPreviewImage != null)
        {
            StartCoroutine(LoadImageFromUrl(classImageUrl, uploadPreviewImage));
        }

        UpdateUploadButtonState();
    }

    private IEnumerator LoadImageFromUrl(string imageUrl, Image targetImage)
    {
        UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageUrl);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning("Load edit image failed: " + request.error);
            yield break;
        }

        Texture2D texture = DownloadHandlerTexture.GetContent(request);

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0.5f)
        );

        targetImage.sprite = sprite;
        targetImage.color = Color.white;
        targetImage.preserveAspect = true;
    }

    private void SetupRequiredMark(TMP_Text mark)
    {
        if (mark == null) return;

        mark.text = "*";
        mark.color = Color.red;
        mark.gameObject.SetActive(false);
    }

    private void SetupMessage(TMP_Text msg)
    {
        if (msg == null) return;

        msg.text = "";
        msg.color = Color.red;
        msg.gameObject.SetActive(false);
    }

    private void LoadCurrentTeacherName()
    {
        string fullName = PlayerPrefs.GetString("full_name", "");

        if (teacherNameInput != null)
        {
            teacherNameInput.text = fullName;
            teacherNameInput.interactable = false;

            if (teacherNameInput.textComponent != null)
                teacherNameInput.textComponent.color = Color.black;
        }
    }

    public void OnCreateClassButtonClicked()
    {
        string className = classNameInput.text.Trim();
        string classInfo = classInfoInput.text.Trim();

        bool isValid = true;

        HideFieldError(classNameRequiredMark, classNameMessageText);
        HideFieldError(classInfoRequiredMark, classInfoMessageText);

        if (string.IsNullOrEmpty(className))
        {
            isValid = false;
            ShowFieldError(classNameRequiredMark, classNameMessageText, "Vui lòng nhập tên lớp học.");
        }

        if (string.IsNullOrEmpty(classInfo))
        {
            isValid = false;
            ShowFieldError(classInfoRequiredMark, classInfoMessageText, "Vui lòng nhập thông tin lớp học.");
        }

        if (!isValid) return;

        if (isUploadingImage)
        {
            ShowFieldError(classInfoRequiredMark, classInfoMessageText, "Ảnh đang được upload, vui lòng chờ.");
            return;
        }

        if (isEditMode)
            StartCoroutine(UpdateClassCoroutine(className, classInfo, classImageUrl));
        else
            StartCoroutine(CreateClassCoroutine(className, classInfo, classImageUrl));
    }

    private IEnumerator CreateClassCoroutine(string className, string classInfo, string classImgUrl)
    {
        string teacherId = PlayerPrefs.GetString("user_id", "");

        if (string.IsNullOrEmpty(teacherId))
        {
            ShowFieldError(classNameRequiredMark, classNameMessageText, "Không tìm thấy thông tin giáo viên.");
            yield break;
        }

        string url = baseUrl + "/classes";

        CreateClassRequest data = new CreateClassRequest();
        data.class_name = className;
        data.summary_info = classInfo;
        data.class_img = classImgUrl;
        data.class_img_url = classImgUrl;
        data.teacher_id = teacherId;

        string json = JsonUtility.ToJson(data);

        UnityWebRequest request = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);

        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Create class failed: " + request.error);
            Debug.LogError("Response: " + request.downloadHandler.text);

            ShowFieldError(classInfoRequiredMark, classInfoMessageText, "Tạo lớp học thất bại.");
            yield break;
        }

        Debug.Log("Create class response: " + request.downloadHandler.text);

        SceneManager.LoadScene(manageClassSceneName);
    }

    public void OnUploadImageButtonClicked()
    {
        #if UNITY_EDITOR
            string path = EditorUtility.OpenFilePanel("Chọn ảnh lớp học", "", "png,jpg,jpeg");

            if (string.IsNullOrEmpty(path))
                return;

            LoadPreviewImage(path);
            StartCoroutine(UploadClassImageCoroutine(path));
        #else
            if (NativeFilePicker.IsFilePickerBusy())
                return;

            string[] allowedTypes = new string[]
            {
                "image/png",
                "image/jpeg"
            };

            NativeFilePicker.PickFile((path) =>
            {
                if (string.IsNullOrEmpty(path))
                {
                    ShowFieldError(classInfoRequiredMark, classInfoMessageText, "Bạn chưa chọn ảnh.");
                    return;
                }

                string extension = Path.GetExtension(path);
                string newPath = Path.Combine(Application.temporaryCachePath, "class_image" + extension);

                File.Copy(path, newPath, true);

                LoadPreviewImage(newPath);
                StartCoroutine(UploadClassImageCoroutine(newPath));

            }, allowedTypes);
        #endif
    }

    private void LoadPreviewImage(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            ShowFieldError(classInfoRequiredMark, classInfoMessageText, "Không tìm thấy file ảnh.");
            return;
        }

        byte[] imageBytes = File.ReadAllBytes(path);

        Texture2D originalTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        bool loaded = originalTexture.LoadImage(imageBytes, false);

        if (!loaded)
        {
            ShowFieldError(classInfoRequiredMark, classInfoMessageText, "Không đọc được ảnh đã chọn.");
            return;
        }

        Texture2D finalTexture = CropCenterToClassImageRatio(originalTexture, 1024, 512);

        finalTexture.filterMode = FilterMode.Bilinear;
        finalTexture.wrapMode = TextureWrapMode.Clamp;
        finalTexture.Apply(false, false);

        Sprite sprite = Sprite.Create(
            finalTexture,
            new Rect(0, 0, finalTexture.width, finalTexture.height),
            new Vector2(0.5f, 0.5f),
            100f
        );

        if (uploadPreviewImage != null)
        {
            uploadPreviewImage.sprite = sprite;
            uploadPreviewImage.color = Color.white;
            uploadPreviewImage.preserveAspect = false;
            uploadPreviewImage.type = Image.Type.Simple;
        }

        hasSelectedImage = true;
        UpdateUploadButtonState();
    }

    private Texture2D CropCenterToClassImageRatio(Texture2D source, int targetWidth, int targetHeight)
    {
        float targetRatio = (float)targetWidth / targetHeight;
        float sourceRatio = (float)source.width / source.height;

        int cropWidth = source.width;
        int cropHeight = source.height;

        if (sourceRatio > targetRatio)
        {
            cropWidth = Mathf.RoundToInt(source.height * targetRatio);
        }
        else
        {
            cropHeight = Mathf.RoundToInt(source.width / targetRatio);
        }

        int startX = Mathf.Max(0, (source.width - cropWidth) / 2);
        int startY = Mathf.Max(0, (source.height - cropHeight) / 2);

        Color[] pixels = source.GetPixels(startX, startY, cropWidth, cropHeight);

        Texture2D croppedTexture = new Texture2D(cropWidth, cropHeight, TextureFormat.RGBA32, false);
        croppedTexture.SetPixels(pixels);
        croppedTexture.Apply();

        Texture2D resizedTexture = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);

        for (int y = 0; y < targetHeight; y++)
        {
            for (int x = 0; x < targetWidth; x++)
            {
                float u = (float)x / targetWidth;
                float v = (float)y / targetHeight;
                resizedTexture.SetPixel(x, y, croppedTexture.GetPixelBilinear(u, v));
            }
        }

        resizedTexture.Apply();
        return resizedTexture;
    }

    private IEnumerator UploadClassImageCoroutine(string path)
    {
        isUploadingImage = true;

        string url = baseUrl + "/upload/class-image";

        WWWForm form = new WWWForm();

        byte[] imageBytes = File.ReadAllBytes(path);
        string fileName = Path.GetFileName(path);
        string mimeType = GetMimeType(path);

        form.AddBinaryData("class_img", imageBytes, fileName, mimeType);

        UnityWebRequest request = UnityWebRequest.Post(url, form);
        yield return request.SendWebRequest();

        isUploadingImage = false;

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Upload image failed: " + request.error);
            Debug.LogError("Response: " + request.downloadHandler.text);

            classImageUrl = "";
            ShowFieldError(classInfoRequiredMark, classInfoMessageText, "Upload ảnh thất bại.");
            yield break;
        }

        Debug.Log("Upload image response: " + request.downloadHandler.text);

        UploadImageResponse response =
            JsonUtility.FromJson<UploadImageResponse>(request.downloadHandler.text);

        if (response != null && response.success)
        {
            classImageUrl = response.image_url;
            HideFieldError(classInfoRequiredMark, classInfoMessageText);
            UpdateUploadButtonState();
        }
        else
        {
            classImageUrl = "";
            ShowFieldError(classInfoRequiredMark, classInfoMessageText, "Không lấy được đường dẫn ảnh.");
        }
    }

    private string GetMimeType(string path)
    {
        string extension = Path.GetExtension(path).ToLower();

        if (extension == ".png")
            return "image/png";

        if (extension == ".jpg" || extension == ".jpeg")
            return "image/jpeg";

        return "image/jpeg";
    }

    public void OnBackButtonClicked()
    {
        SceneManager.LoadScene(manageClassSceneName);
    }

    private void ShowFieldError(TMP_Text mark, TMP_Text message, string text)
    {
        if (mark != null)
        {
            mark.text = "*";
            mark.color = Color.red;
            mark.gameObject.SetActive(true);
        }

        if (message != null)
        {
            message.text = text;
            message.color = Color.red;
            message.gameObject.SetActive(true);
        }
    }

    private void HideFieldError(TMP_Text mark, TMP_Text message)
    {
        if (mark != null)
            mark.gameObject.SetActive(false);

        if (message != null)
        {
            message.text = "";
            message.gameObject.SetActive(false);
        }
    }

    private IEnumerator UpdateClassCoroutine(string className, string classInfo, string classImgUrl)
    {
        if (string.IsNullOrEmpty(editClassId))
        {
            Debug.LogError("editClassId rỗng.");
            yield break;
        }

        string url = baseUrl + "/classes/" + editClassId;

        CreateClassRequest data = new CreateClassRequest();
        data.class_name = className;
        data.summary_info = classInfo;
        data.class_img = classImgUrl;
        data.class_img_url = classImgUrl;
        data.teacher_id = PlayerPrefs.GetString("user_id", "");

        string json = JsonUtility.ToJson(data);

        UnityWebRequest request = new UnityWebRequest(url, "PUT");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);

        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Update class failed: " + request.error);
            Debug.LogError("Response: " + request.downloadHandler.text);
            yield break;
        }

        Debug.Log("Update class response: " + request.downloadHandler.text);

        PlayerPrefs.DeleteKey("is_edit_class");
        PlayerPrefs.DeleteKey("edit_class_id");
        PlayerPrefs.DeleteKey("edit_class_name");
        PlayerPrefs.DeleteKey("edit_summary_info");
        PlayerPrefs.DeleteKey("edit_class_img_url");
        PlayerPrefs.Save();

        SceneManager.LoadScene(manageClassSceneName);
    }

    private void UpdateUploadButtonState()
    {
        if (uploadButtonObject == null) return;

        bool hasImage = hasSelectedImage || !string.IsNullOrEmpty(classImageUrl);

        uploadButtonObject.SetActive(!hasImage);
    }
}

[System.Serializable]
public class CreateClassRequest
{
    public string class_name;
    public string summary_info;
    public string class_img;
    public string class_img_url;
    public string teacher_id;
}

[System.Serializable]
public class UploadImageResponse
{
    public bool success;
    public string image_url;
    public string file_name;
}