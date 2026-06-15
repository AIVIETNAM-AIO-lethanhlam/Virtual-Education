using System;
using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Firebase.Storage;
using Firebase.Extensions;
using NativeFilePickerNamespace;
#if UNITY_EDITOR
using UnityEditor;
#endif


public class CreateLessonManager : MonoBehaviour
{
    [Header("Calendar Colors")]
    public Color normalDayColor = Color.white;
    public Color selectedDayColor = new Color(0.0f, 0.55f, 1.0f);
    public Color todayDayColor = new Color(0.0f, 0.55f, 1.0f);
    public Color normalTextColor = Color.black;
    public Color selectedTextColor = Color.white;
    [Header("Input Fields")]
    public TMP_InputField lessonNameInput;
    public TMP_InputField lessonInfoInput;
    public TMP_InputField teacherNameInput;
    public TMP_InputField timeExerciseInput;
    public TMP_InputField deadlineInput;
    [Header("Video Upload")]
    public Button uploadVideoButton; 
    public Transform videoFileListContent; 

    [Header("Title")]
    public TMP_Text titleText;

    [Header("Buttons")]
    public Button createLessonButton;
    public Button backBtn;
    public TMP_Text createLessonButtonText;
    public TMP_Text messageText;

    [Header("Background Image")]
    public Button uploadBackgroundButton;
    public Image backgroundPreview;

    [Header("Lesson PDF")]
    public Button uploadLessonButton;
    public Transform lessonFileListContent;
    public GameObject fileItemPrefab;
    [Header("Model 3D")]
    public Button uploadModelButton;
    public Button uploadModelBoxButton; // thêm dòng này
    public Transform modelFileListContent;
    public GameObject modelItemPrefab;

    [Header("Exercise PDF")]
    public Button uploadExerciseButton;
    public Transform exerciseFileListContent;

    [Header("Calendar")]
    public GameObject calendarPanel;
    public Button openCalendarButton;
    public Button prevMonthButton;
    public Button nextMonthButton;
    public TMP_Text monthYearText;
    public Transform dayButtonContent;
    public GameObject dayButtonPrefab;
    public TMP_InputField deadlineTimeInput;

    [Header("API")]
    public string baseUrl = "http://localhost:4000/api";
    // public string baseUrl = "https://e571-14-241-225-251.ngrok-free.app/api";

    [Header("Scene")]
    public string lessonInClassSceneName = "LessonInClass";

    private string selectedClassId = "";
    private string teacherId = "";
    private string editLessonId = "";

    private string backgroundImagePath = "";
    private string lessonPdfPath = "";
    private string exercisePdfPath = "";

    private string backgroundImageUrl = "";
    private string lessonPdfUrl = "";
    private string exercisePdfUrl = "";
    private System.Collections.Generic.List<string> modelFilePaths = new System.Collections.Generic.List<string>();
    private System.Collections.Generic.List<ModelData> existingModels = new System.Collections.Generic.List<ModelData>();
    private System.Collections.Generic.List<string> modelFirebaseUrls = new System.Collections.Generic.List<string>();

    private bool isEditMode = false;
    private bool isUploading = false;

    private DateTime currentCalendarDate = DateTime.Now;
    private DateTime? selectedDeadlineDate = null;

    private bool lessonPdfRemoved = false;
    private bool exercisePdfRemoved = false;
    private string videoPath = "";
    private string videoUrl = "";
    private bool videoRemoved = false;

    private void Start()
    {
        selectedClassId = PlayerPrefs.GetString("selected_class_id", "");
        teacherId = PlayerPrefs.GetString("user_id", "");

        isEditMode = PlayerPrefs.GetInt("is_edit_lesson", 0) == 1;
        editLessonId = PlayerPrefs.GetString("edit_lesson_id", "");

        LoadCurrentTeacherName();
        SetupButtons();
        SetupEditMode();
        SetupCalendar();

        if (calendarPanel != null)
            calendarPanel.SetActive(false);
    }

    private void SetupButtons()
    {
        if (createLessonButton != null)
            createLessonButton.onClick.AddListener(OnCreateLessonClicked);

        if (backBtn != null)
            backBtn.onClick.AddListener(OnBackButtonClicked);

        if (uploadBackgroundButton != null)
            uploadBackgroundButton.onClick.AddListener(PickBackgroundImage);

        if (uploadLessonButton != null)
            uploadLessonButton.onClick.AddListener(PickLessonPdf);

        if (uploadExerciseButton != null)
            uploadExerciseButton.onClick.AddListener(PickExercisePdf);


        if (openCalendarButton != null)
            openCalendarButton.onClick.AddListener(ToggleCalendar);

        if (prevMonthButton != null)
            prevMonthButton.onClick.AddListener(PreviousMonth);

        if (nextMonthButton != null)
            nextMonthButton.onClick.AddListener(NextMonth);
        if (uploadVideoButton != null)
            uploadVideoButton.onClick.AddListener(PickVideoFile);

        if (uploadModelButton != null)
            uploadModelButton.onClick.AddListener(PickModelFile);

        if (uploadModelBoxButton != null)
            uploadModelBoxButton.onClick.AddListener(PickModelFile);
    }

    
    private void SetupEditMode()
    {
        if (isEditMode)
        {
            if (titleText != null)
                titleText.text = "CẬP NHẬT BÀI HỌC";

            if (createLessonButtonText != null)
                createLessonButtonText.text = "Cập nhật";

            lessonNameInput.text = PlayerPrefs.GetString("edit_lesson_title", "");
            StartCoroutine(LoadEditLessonDataFromApi());
            lessonInfoInput.text = PlayerPrefs.GetString("edit_lesson_description", "");

            backgroundImageUrl = PlayerPrefs.GetString("edit_lesson_img_url", "");
            lessonPdfUrl = PlayerPrefs.GetString("edit_lesson_pdf_url", "");
            exercisePdfUrl = PlayerPrefs.GetString("edit_exercise_pdf_url", "");
            videoUrl = PlayerPrefs.GetString("edit_video_url", "");
            if (!string.IsNullOrEmpty(backgroundImageUrl))
                StartCoroutine(LoadImageFromUrl(backgroundImageUrl, backgroundPreview));
        }
        else
        {
            if (titleText != null)
                titleText.text = "TẠO MỚI BÀI HỌC";

            if (createLessonButtonText != null)
                createLessonButtonText.text = "Tạo bài học";
        }
    }

    private void LoadCurrentTeacherName()
    {
        string fullName = PlayerPrefs.GetString("full_name", "");

        if (teacherNameInput != null)
        {
            teacherNameInput.text = fullName;
            teacherNameInput.interactable = false;
        }
    }

    // private void PickModelFile()
    // {
    // #if UNITY_EDITOR
    //     string path = EditorUtility.OpenFilePanel("Chọn Model 3D", "", "glb");

    //     if (string.IsNullOrEmpty(path)) return;

    //     // Thêm vào danh sách thay vì ghi đè
    //     modelFilePaths.Add(path);
        
    //     // Gọi hàm AddFileItem với Prefab mới và bật chế độ isMultiple = true
    //     AddFileItem(
    //         modelFileListContent,
    //         uploadModelButton,
    //         Path.GetFileName(path),
    //         () =>
    //         {
    //             // Khi bấm nút X, xóa path này khỏi danh sách
    //             modelFilePaths.Remove(path);
    //         },
    //         modelItemPrefab, // Dùng prefab 3D
    //         true             // isMultiple = true
    //     );

    //     ShowMessage("Đã thêm model: " + Path.GetFileName(path));
    // #else
    //     ShowMessage("Chọn Model trên APK cần plugin Native File Picker.");
    // #endif
    // }

    private void PickModelFile()
    {
        #if UNITY_EDITOR
            string path = EditorUtility.OpenFilePanel("Chọn Model 3D", "", "glb");
            if (string.IsNullOrEmpty(path)) return;
            HandleModelPicked(path);
        #else
            NativeFilePicker.PickFile((path) =>
            {
                if (string.IsNullOrEmpty(path)) return;
                HandleModelPicked(path);
            }, new string[] { "application/octet-stream", "*/*" });
        #endif
    }

    private void HandleModelPicked(string path)
    {
        modelFilePaths.Add(path);

        AddFileItem(
            modelFileListContent,
            uploadModelButton,
            Path.GetFileName(path),
            () =>
            {
                modelFilePaths.Remove(path);
                StartCoroutine(UpdateModelUploadButtonAfterDestroy());
            },
            modelItemPrefab,
            true
        );

        UpdateModelUploadButtonState();
        ShowMessage("Đã thêm model: " + Path.GetFileName(path));
    }

    private void OnCreateLessonClicked()
    {
        if (string.IsNullOrWhiteSpace(lessonNameInput.text))
        {
            ShowMessage("Vui lòng nhập tên bài học.");
            return;
        }

        if (string.IsNullOrWhiteSpace(lessonInfoInput.text))
        {
            ShowMessage("Vui lòng nhập thông tin bài học.");
            return;
        }

        if (string.IsNullOrWhiteSpace(timeExerciseInput.text))
        {
            ShowMessage("Vui lòng nhập thời gian làm bài.");
            return;
        }

        if (string.IsNullOrWhiteSpace(deadlineInput.text))
        {
            ShowMessage("Vui lòng chọn ngày nộp bài.");
            return;
        }

        if (string.IsNullOrEmpty(selectedClassId))
        {
            ShowMessage("Không tìm thấy selected_class_id.");
            return;
        }

        if (string.IsNullOrEmpty(teacherId))
        {
            ShowMessage("Không tìm thấy teacher_id.");
            return;
        }

        if (isUploading)
        {
            ShowMessage("File đang được upload, vui lòng chờ.");
            return;
        }

        StartCoroutine(SubmitLessonFlow());
    }

    private IEnumerator SubmitLessonFlow()
    {
        if (modelFilePaths.Count > 0)
    {
        modelFirebaseUrls.Clear(); // Xóa sạch list URL cũ trước khi up

        foreach (string path in modelFilePaths)
        {
            byte[] modelBytes = File.ReadAllBytes(path);
            string fileName = Path.GetFileName(path);

            FirebaseStorage storage = FirebaseStorage.DefaultInstance;
            StorageReference storageRef = storage.GetReferenceFromUrl("gs://virtual-education-d056a.firebasestorage.app");
            StorageReference modelRef = storageRef.Child("models/" + fileName);

            var uploadTask = modelRef.PutBytesAsync(modelBytes);
            yield return new WaitUntil(() => uploadTask.IsCompleted);

            if (uploadTask.IsFaulted || uploadTask.IsCanceled) 
            {
                Debug.LogError("Upload Model thất bại: " + fileName);
            } 
            else 
            {
                var urlTask = modelRef.GetDownloadUrlAsync();
                yield return new WaitUntil(() => urlTask.IsCompleted);

                if (!urlTask.IsFaulted && !urlTask.IsCanceled) 
                {
                    // Thêm URL lấy được vào danh sách
                    modelFirebaseUrls.Add(urlTask.Result.ToString());
                    Debug.Log("Upload thành công: " + fileName);
                }
            }
        }
    }

        if (!string.IsNullOrEmpty(backgroundImagePath))
        {
            yield return StartCoroutine(UploadFile(
                "/upload/lesson-image",
                "lesson_img",
                backgroundImagePath,
                "image_url",
                value => backgroundImageUrl = value
            ));
        }

        if (!string.IsNullOrEmpty(lessonPdfPath))
        {
            yield return StartCoroutine(UploadFile(
                "/upload/lesson-pdf",
                "pdf",
                lessonPdfPath,
                "pdf_url",
                value => lessonPdfUrl = value
            ));
        }

        if (!string.IsNullOrEmpty(videoPath))
        {
            yield return StartCoroutine(UploadFile(
                "/upload/lesson-video", 
                "video_file",
                videoPath,
                "video_url",
                value => videoUrl = value
            ));
        }

        // KHÔNG upload exercisePdfPath ở đây nữa.
        // File quiz PDF sẽ upload sau khi tạo lesson thành công,
        // bằng API: /classes/{classId}/lessons/{lessonId}/upload-quiz-pdf

        isUploading = false;

        if (isEditMode)
        {
            if (!string.IsNullOrEmpty(exercisePdfPath))
            {
                yield return StartCoroutine(UploadQuizPdfCoroutine(editLessonId));
            }

            yield return StartCoroutine(UpdateLessonCoroutine());
        }
        else
        {
            yield return StartCoroutine(CreateLessonCoroutine());
        }
    }

    private IEnumerator CreateLessonCoroutine()
    {
        string url = baseUrl + "/lessons";

        LessonRequest data = BuildLessonRequest();
        string json = JsonUtility.ToJson(data);

        UnityWebRequest request = CreateJsonRequest(url, "POST", json);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            ShowMessage("Tạo bài học thất bại.");
            Debug.LogError(request.downloadHandler.text);
            yield break;
        }

        LessonCreateResponse response =
            JsonUtility.FromJson<LessonCreateResponse>(request.downloadHandler.text);

        if (response == null || !response.success || string.IsNullOrEmpty(response.lesson_id))
        {
            ShowMessage("Không lấy được lesson_id sau khi tạo bài học.");
            Debug.LogError(request.downloadHandler.text);
            yield break;
        }

        editLessonId = response.lesson_id;

        PlayerPrefs.SetString("selected_lesson_id", editLessonId);
        PlayerPrefs.SetString("selected_class_id", selectedClassId);
        PlayerPrefs.Save();

        if (!string.IsNullOrEmpty(exercisePdfPath))
        {
            yield return StartCoroutine(UploadQuizPdfCoroutine(editLessonId));
        }

        ClearEditPrefs();
        SceneManager.LoadScene(lessonInClassSceneName);
    }

    private IEnumerator UpdateLessonCoroutine()
    {
        if (string.IsNullOrEmpty(editLessonId))
        {
            ShowMessage("Không tìm thấy lesson_id để cập nhật.");
            yield break;
        }

        string url = baseUrl + "/lessons/" + editLessonId;

        LessonRequest data = BuildLessonRequest();
        string json = JsonUtility.ToJson(data);

        UnityWebRequest request = CreateJsonRequest(url, "PUT", json);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            ShowMessage("Cập nhật bài học thất bại.");
            Debug.LogError(request.downloadHandler.text);
            yield break;
        }

        ClearEditPrefs();
        SceneManager.LoadScene(lessonInClassSceneName);
    }

    private IEnumerator UploadQuizPdfCoroutine(string lessonId)
    {
        if (string.IsNullOrEmpty(exercisePdfPath))
            yield break;

        if (!File.Exists(exercisePdfPath))
        {
            ShowMessage("File PDF bài tập không tồn tại.");
            yield break;
        }

        if (string.IsNullOrEmpty(lessonId))
        {
            ShowMessage("lessonId rỗng, không thể upload quiz PDF.");
            Debug.LogError("lessonId rỗng.");
            yield break;
        }

        string url = baseUrl
            + "/classes/"
            + selectedClassId
            + "/lessons/"
            + lessonId
            + "/upload-quiz-pdf";

        byte[] fileBytes = File.ReadAllBytes(exercisePdfPath);
        string fileName = Path.GetFileName(exercisePdfPath);

        WWWForm form = new WWWForm();

        form.AddBinaryData(
            "quiz_pdf",
            fileBytes,
            fileName,
            "application/pdf"
        );

        form.AddField("deadline_date", deadlineInput != null ? deadlineInput.text : "");

        form.AddField("deadline_time", deadlineTimeInput != null ? deadlineTimeInput.text : "00:00:00");

        using (UnityWebRequest request = UnityWebRequest.Post(url, form))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Upload quiz PDF failed: " + request.error);
                Debug.LogError("Response: " + request.downloadHandler.text);
                ShowMessage("Upload bài tập thất bại.");
                yield break;
            }

            Debug.Log("Upload quiz PDF success:");
            Debug.Log(request.downloadHandler.text);

            UploadQuizResponse response =
                JsonUtility.FromJson<UploadQuizResponse>(request.downloadHandler.text);

            if (response != null && response.success && response.quiz != null)
            {
                PlayerPrefs.SetString("selected_quiz_id", response.quiz.quiz_id);

                if (!string.IsNullOrEmpty(response.quiz.quiz_pdf_url))
                {
                    exercisePdfUrl = response.quiz.quiz_pdf_url;
                    PlayerPrefs.SetString("selected_exercise_pdf_url", exercisePdfUrl);
                }

                string openTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                string closeTime =
                    (deadlineInput != null ? deadlineInput.text.Trim() : "") + " " +
                    (deadlineTimeInput != null ? deadlineTimeInput.text.Trim() : "00:00:00");

                PlayerPrefs.SetString("quiz_open_time", openTime);
                PlayerPrefs.SetString("quiz_close_time", closeTime);

                PlayerPrefs.Save();
            }
        }
    }

    private LessonRequest BuildLessonRequest()
    {
        LessonRequest data = new LessonRequest();

        data.class_id = selectedClassId;
        data.teacher_id = teacherId;
        data.lesson_title = lessonNameInput.text.Trim();
        data.lesson_info = lessonInfoInput.text.Trim();
        data.time_exercise = timeExerciseInput.text.Trim();
        data.deadline_date = deadlineInput.text.Trim();
        data.deadline_time = deadlineTimeInput != null
            ? deadlineTimeInput.text.Trim()
            : "00:00:00";

        data.lesson_img = backgroundImageUrl;
        data.lesson_img_url = backgroundImageUrl;

        data.lesson_pdf_url = lessonPdfUrl;
        data.exercise_pdf_url = exercisePdfUrl;
        data.models = new System.Collections.Generic.List<ModelData>();

        for (int i = 0; i < existingModels.Count; i++)
        {
            data.models.Add(existingModels[i]);
        }
        data.video_url = videoUrl;
        for (int i = 0; i < modelFirebaseUrls.Count; i++)
        {
            ModelData mData = new ModelData();
            mData.model_url = modelFirebaseUrls[i];
            
            // Lấy tên file gốc làm tên hiển thị
            if (i < modelFilePaths.Count)
                mData.model_name = Path.GetFileName(modelFilePaths[i]);
            else
                mData.model_name = "Model_3D_" + i;

            data.models.Add(mData);
        }

        return data;
    }

    private UnityWebRequest CreateJsonRequest(string url, string method, string json)
    {
        UnityWebRequest request = new UnityWebRequest(url, method);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);

        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        return request;
    }

    private IEnumerator UploadFile(
        string endpoint,
        string fieldName,
        string filePath,
        string responseUrlField,
        Action<string> onSuccess
    )
    {
        string url = baseUrl + endpoint;

        WWWForm form = new WWWForm();
        byte[] bytes = File.ReadAllBytes(filePath);
        string fileName = Path.GetFileName(filePath);
        string mimeType = GetMimeType(filePath);

        form.AddBinaryData(fieldName, bytes, fileName, mimeType);

        UnityWebRequest request = UnityWebRequest.Post(url, form);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            ShowMessage("Upload file thất bại.");
            Debug.LogError(request.downloadHandler.text);
            yield break;
        }

        string json = request.downloadHandler.text;
        UploadResponse response = JsonUtility.FromJson<UploadResponse>(json);

        if (response == null || !response.success)
        {
            ShowMessage("Không lấy được URL file.");
            yield break;
        }

        string resultUrl = "";

        if (responseUrlField == "image_url")
            resultUrl = response.image_url;
        else if (responseUrlField == "pdf_url")
            resultUrl = response.pdf_url;
        else if (responseUrlField == "video_url") 
            resultUrl = response.video_url;       
        onSuccess(resultUrl);
    }

    private string GetMimeType(string path)
    {
        string ext = Path.GetExtension(path).ToLower();

        if (ext == ".png") return "image/png";
        if (ext == ".jpg" || ext == ".jpeg") return "image/jpeg";
        if (ext == ".pdf") return "application/pdf";
        if (ext == ".mp4") return "video/mp4";
        if (ext == ".mov") return "video/quicktime";
        if (ext == ".mp3") return "video/mp3";
        if (ext == ".mp5") return "video/mp5";
        return "application/octet-stream";
    }

    private void PickBackgroundImage()
    {
        #if UNITY_EDITOR
            string path = EditorUtility.OpenFilePanel("Chọn ảnh bài học", "", "png,jpg,jpeg");
            if (string.IsNullOrEmpty(path)) return;
            HandleBackgroundPicked(path);
        #else
            NativeFilePicker.PickFile((path) =>
            {
                if (string.IsNullOrEmpty(path)) return;
                HandleBackgroundPicked(path);
            }, new string[] { "image/*" });
        #endif
    }

    private void HandleBackgroundPicked(string path)
    {
        backgroundImagePath = path;
        StartCoroutine(LoadPreviewImage(path));
    }

    private void PickLessonPdf()
    {
        #if UNITY_EDITOR
            string path = EditorUtility.OpenFilePanel("Chọn PDF bài học", "", "pdf");
            if (string.IsNullOrEmpty(path)) return;
            HandleLessonPdfPicked(path);
        #else
            NativeFilePicker.PickFile((path) =>
            {
                if (string.IsNullOrEmpty(path)) return;
                HandleLessonPdfPicked(path);
            }, new string[] { "application/pdf" });
        #endif
    }

    private void HandleLessonPdfPicked(string path)
    {
        lessonPdfPath = path;
        lessonPdfRemoved = false;

        AddFileItem(
            lessonFileListContent,
            uploadLessonButton,
            Path.GetFileName(path),
            () =>
            {
                lessonPdfPath = "";
                lessonPdfUrl = "";
                lessonPdfRemoved = true;
            }
        );
    }

    private void PickExercisePdf()
    {
        #if UNITY_EDITOR
            string path = EditorUtility.OpenFilePanel("Chọn PDF bài tập", "", "pdf");
            if (string.IsNullOrEmpty(path)) return;
            HandleExercisePdfPicked(path);
        #else
            NativeFilePicker.PickFile((path) =>
            {
                if (string.IsNullOrEmpty(path)) return;
                HandleExercisePdfPicked(path);
            }, new string[] { "application/pdf" });
        #endif
    }

    private void HandleExercisePdfPicked(string path)
    {
        exercisePdfPath = path;
        exercisePdfRemoved = false;

        AddFileItem(
            exerciseFileListContent,
            uploadExerciseButton,
            Path.GetFileName(path),
            () =>
            {
                exercisePdfPath = "";
                exercisePdfUrl = "";
                exercisePdfRemoved = true;
            }
        );
    }
    private void PickVideoFile()
    {
        #if UNITY_EDITOR
            string path = EditorUtility.OpenFilePanel("Chọn Video bài học", "", "mp4,mov");
            if (string.IsNullOrEmpty(path)) return;
            HandleVideoPicked(path);
        #else
            NativeFilePicker.PickFile((path) =>
            {
                if (string.IsNullOrEmpty(path)) return;
                HandleVideoPicked(path);
            }, new string[] { "video/*" });
        #endif
    }

    private void HandleVideoPicked(string path)
    {
        videoPath = path;
        videoRemoved = false;

        AddFileItem(
            videoFileListContent,
            uploadVideoButton,
            Path.GetFileName(path),
            () =>
            {
                videoPath = "";
                videoUrl = "";
                videoRemoved = true;
            }
        );

        ShowMessage("Đã chọn video: " + Path.GetFileName(path));
    }

    private IEnumerator LoadPreviewImage(string path)
    {
        byte[] imageBytes = File.ReadAllBytes(path);

        Texture2D texture = new Texture2D(2, 2);
        bool loaded = texture.LoadImage(imageBytes);

        if (!loaded)
        {
            ShowMessage("Không load được ảnh preview.");
            yield break;
        }

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0.5f)
        );

        if (backgroundPreview != null)
        {
            backgroundPreview.gameObject.SetActive(true);
            backgroundPreview.sprite = sprite;
            backgroundPreview.color = Color.white;
            backgroundPreview.preserveAspect = true;

            // Đưa ảnh lên trên trong cùng parent
            backgroundPreview.transform.SetAsLastSibling();
        }
        else
        {
            Debug.LogError("backgroundPreview chưa được gắn trong Inspector.");
        }

        yield return null;
    }

    private IEnumerator LoadImageFromUrl(string imageUrl, Image targetImage)
    {
        if (targetImage == null) yield break;

        UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageUrl);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
            yield break;

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

    private void AddFileItem(
        Transform parent,
        Button uploadButton,
        string fileName,
        Action onDelete,
        GameObject customPrefab = null,
        bool isMultiple = false
    )
    {
        GameObject prefabToUse = customPrefab != null ? customPrefab : fileItemPrefab;
        if (parent == null || prefabToUse == null)
            return;

        if (!isMultiple)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Destroy(parent.GetChild(i).gameObject);
            }
        }
        

        GameObject item = Instantiate(prefabToUse, parent);

        FileItemUI fileItemUI = item.GetComponent<FileItemUI>();

        if (fileItemUI != null)
        {
            fileItemUI.Setup(fileName, () =>
            {
                if (onDelete != null)
                    onDelete.Invoke();

                Destroy(item);

                if (!isMultiple && uploadButton != null)
                {
                    uploadButton.gameObject.SetActive(true);
                }
            });
        }
        
        else
        {
            TMP_Text text = item.GetComponentInChildren<TMP_Text>();
            if (text != null)
                text.text = fileName;

            Debug.LogError("FileItem prefab chưa gắn FileItemUI.cs");
        }

        if (!isMultiple && uploadButton != null)
            uploadButton.gameObject.SetActive(false);
    }

    private string GetFileNameFromUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
            return "File đã tải lên";

        try
        {
            Uri uri = new Uri(url);
            string fileName = Path.GetFileName(uri.LocalPath);

            if (string.IsNullOrEmpty(fileName))
                return "File đã tải lên";

            return fileName;
        }
        catch
        {
            return "File đã tải lên";
        }
    }

    private void SetupCalendar()
    {
        currentCalendarDate = DateTime.Now;

        if (selectedDeadlineDate == null && deadlineInput != null && !string.IsNullOrWhiteSpace(deadlineInput.text))
        {
            DateTime parsedDate;
            if (DateTime.TryParse(deadlineInput.text, out parsedDate))
            {
                selectedDeadlineDate = parsedDate;
                currentCalendarDate = parsedDate;
            }
        }

        GenerateCalendar();
    }

    private void ToggleCalendar()
    {
        if (calendarPanel == null)
            return;

        bool nextState = !calendarPanel.activeSelf;
        calendarPanel.SetActive(nextState);

        if (nextState)
        {
            if (selectedDeadlineDate.HasValue)
                currentCalendarDate = selectedDeadlineDate.Value;
            else
                currentCalendarDate = DateTime.Now;

            GenerateCalendar();
        }
    }

    private void PreviousMonth()
    {
        currentCalendarDate = currentCalendarDate.AddMonths(-1);
        GenerateCalendar();
    }

    private void NextMonth()
    {
        currentCalendarDate = currentCalendarDate.AddMonths(1);
        GenerateCalendar();
    }

    private void GenerateCalendar()
    {
        if (dayButtonContent == null || dayButtonPrefab == null)
            return;

        for (int i = dayButtonContent.childCount - 1; i >= 0; i--)
        {
            Destroy(dayButtonContent.GetChild(i).gameObject);
        }

        if (monthYearText != null)
            monthYearText.text = currentCalendarDate.ToString("MM/yyyy");

        int daysInMonth = DateTime.DaysInMonth(currentCalendarDate.Year, currentCalendarDate.Month);

        DateTime today = DateTime.Today;

        for (int day = 1; day <= daysInMonth; day++)
        {
            DateTime date = new DateTime(currentCalendarDate.Year, currentCalendarDate.Month, day);

            GameObject obj = Instantiate(dayButtonPrefab, dayButtonContent);

            TMP_Text txt = obj.GetComponentInChildren<TMP_Text>();
            Image img = obj.GetComponent<Image>();
            Button btn = obj.GetComponent<Button>();

            if (txt != null)
                txt.text = day.ToString();

            bool isSelected = selectedDeadlineDate.HasValue &&
                            selectedDeadlineDate.Value.Date == date.Date;

            bool isTodayDefault = !selectedDeadlineDate.HasValue &&
                                today.Date == date.Date;

            if (img != null)
            {
                if (isSelected || isTodayDefault)
                    img.color = selectedDayColor;
                else
                    img.color = normalDayColor;
            }

            if (txt != null)
            {
                if (isSelected || isTodayDefault)
                    txt.color = selectedTextColor;
                else
                    txt.color = normalTextColor;
            }

            if (btn != null)
            {
                DateTime selectedDate = date;
                btn.onClick.AddListener(() => SelectDate(selectedDate));
            }
        }
    }

    private void SelectDate(DateTime date)
    {
        selectedDeadlineDate = date;
        currentCalendarDate = date;

        if (deadlineInput != null)
            deadlineInput.text = date.ToString("yyyy-MM-dd");

        GenerateCalendar();

        if (calendarPanel != null)
            calendarPanel.SetActive(false);
    }

    private void OnBackButtonClicked()
    {
        ClearEditPrefs();
        SceneManager.LoadScene(lessonInClassSceneName);
    }

    private void ClearEditPrefs()
    {
        PlayerPrefs.DeleteKey("is_edit_lesson");
        PlayerPrefs.DeleteKey("edit_lesson_id");
        PlayerPrefs.DeleteKey("edit_class_id");
        PlayerPrefs.DeleteKey("edit_lesson_title");
        PlayerPrefs.DeleteKey("edit_lesson_description");
        PlayerPrefs.DeleteKey("edit_lesson_img_url");
        PlayerPrefs.DeleteKey("edit_lesson_pdf_url");
        PlayerPrefs.DeleteKey("edit_exercise_pdf_url");
        PlayerPrefs.DeleteKey("edit_video_url");
        PlayerPrefs.Save();
    }

    private void ShowMessage(string msg)
    {
        Debug.LogWarning(msg);

        if (messageText != null)
            messageText.text = msg;
    }

    private void Update()
    {
        if (calendarPanel == null || !calendarPanel.activeSelf)
            return;

        bool pressed = false;
        Vector2 screenPosition = Vector2.zero;

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            pressed = true;
            screenPosition = Mouse.current.position.ReadValue();
        }

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            pressed = true;
            screenPosition = Touchscreen.current.primaryTouch.position.ReadValue();
        }

        if (!pressed)
            return;

        CheckClickOutsideCalendar(screenPosition);
    }

    private void CheckClickOutsideCalendar(Vector2 screenPosition)
    {
        RectTransform calendarRect = calendarPanel.GetComponent<RectTransform>();

        if (calendarRect == null)
            return;

        bool clickInsideCalendar = RectTransformUtility.RectangleContainsScreenPoint(
            calendarRect,
            screenPosition,
            null
        );

        RectTransform iconRect = openCalendarButton.GetComponent<RectTransform>();

        bool clickOnIcon = RectTransformUtility.RectangleContainsScreenPoint(
            iconRect,
            screenPosition,
            null
        );

        if (!clickInsideCalendar && !clickOnIcon)
        {
            calendarPanel.SetActive(false);
        }
    }

    private IEnumerator LoadEditLessonDataFromApi()
    {
        if (string.IsNullOrEmpty(editLessonId))
        {
            ShowMessage("Không tìm thấy edit_lesson_id.");
            yield break;
        }

        string url = baseUrl.Trim() + "/lessons/" + editLessonId;

        UnityWebRequest request = UnityWebRequest.Get(url);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            ShowMessage("Không load được dữ liệu bài học.");
            Debug.LogError(request.downloadHandler.text);
            yield break;
        }

        LessonEditResponse response =
            JsonUtility.FromJson<LessonEditResponse>(request.downloadHandler.text);

        if (response == null || !response.success || response.lesson == null)
        {
            ShowMessage("Dữ liệu bài học không hợp lệ.");
            Debug.LogError(request.downloadHandler.text);
            yield break;
        }

        LessonEditData lesson = response.lesson;

        if (lessonNameInput != null)
            lessonNameInput.text = !string.IsNullOrEmpty(lesson.lesson_title)
                ? lesson.lesson_title
                : lesson.lesson_name;

        if (lessonInfoInput != null)
            lessonInfoInput.text = lesson.lesson_info;

        if (timeExerciseInput != null)
            timeExerciseInput.text = lesson.time_exercise;

        if (deadlineInput != null)
            deadlineInput.text = lesson.deadline_date;

        if (deadlineTimeInput != null)
            deadlineTimeInput.text = lesson.deadline_time;

        backgroundImageUrl = !string.IsNullOrEmpty(lesson.lesson_img_url)
            ? lesson.lesson_img_url
            : lesson.lesson_img;

        lessonPdfUrl = lesson.lesson_pdf_url;
        exercisePdfUrl = lesson.exercise_pdf_url;
        videoUrl = lesson.video_url;

        if (!string.IsNullOrEmpty(backgroundImageUrl))
        {
            if (backgroundPreview != null)
                backgroundPreview.gameObject.SetActive(true);

            StartCoroutine(LoadImageFromUrl(backgroundImageUrl, backgroundPreview));
        }

        if (!string.IsNullOrEmpty(lessonPdfUrl))
        {
            AddFileItem(
                lessonFileListContent,
                uploadLessonButton,
                GetFileNameFromUrl(lessonPdfUrl),
                () =>
                {
                    lessonPdfPath = "";
                    lessonPdfUrl = "";
                    lessonPdfRemoved = true;
                }
            );
        }

        if (!string.IsNullOrEmpty(exercisePdfUrl))
        {
            AddFileItem(
                exerciseFileListContent,
                uploadExerciseButton,
                GetFileNameFromUrl(exercisePdfUrl),
                () =>
                {
                    exercisePdfPath = "";
                    exercisePdfUrl = "";
                    exercisePdfRemoved = true;
                }
            );
        }

        if (!string.IsNullOrEmpty(videoUrl))
        {
            AddFileItem(
                videoFileListContent,
                uploadVideoButton,
                GetFileNameFromUrl(videoUrl),
                () =>
                {
                    videoPath = "";
                    videoUrl = "";
                    videoRemoved = true;
                }
            );
        }

        existingModels.Clear();

        if (lesson.models != null && lesson.models.Count > 0)
        {
            for (int i = 0; i < lesson.models.Count; i++)
            {
                ModelData model = lesson.models[i];

                if (model == null || string.IsNullOrEmpty(model.model_url))
                    continue;

                existingModels.Add(model);

                AddFileItem(
                    modelFileListContent,
                    uploadModelButton,
                    !string.IsNullOrEmpty(model.model_name)
                        ? model.model_name
                        : GetFileNameFromUrl(model.model_url),
                    () =>
                    {
                        existingModels.Remove(model);
                        UpdateModelUploadButtonState();
                    },
                    modelItemPrefab,
                    true
                );
            }
        }

        UpdateModelUploadButtonState();
    }

    private void UpdateModelUploadButtonState()
    {
        if (uploadModelButton == null)
            return;

        uploadModelButton.gameObject.SetActive(modelFilePaths.Count == 0);
    }

    private IEnumerator UpdateModelUploadButtonAfterDestroy()
    {
        yield return new WaitForEndOfFrame();
        UpdateModelUploadButtonState();
    }
}

[System.Serializable]
public class ModelData
{
    public string model_name;
    public string model_url;
}

[System.Serializable]
public class LessonRequest
{
    public string class_id;
    public string teacher_id;
    public string lesson_title;
    public string lesson_info;
    public string time_exercise;
    public string deadline_date;
    public string deadline_time;
    public string lesson_img;
    public string lesson_img_url;
    public string lesson_pdf_url;
    public string exercise_pdf_url;
    public System.Collections.Generic.List<ModelData> models;
    public string video_url;

    
}

[System.Serializable]
public class UploadResponse
{
    public bool success;
    public string image_url;
    public string pdf_url;
    public string file_name;
    public string pdf_name;
    public string video_url;
}

[System.Serializable]
public class LessonCreateResponse
{
    public bool success;
    public string message;
    public string lesson_id;
    public string quiz_id;
}

[System.Serializable]
public class LessonCreateData
{
    public string lesson_id;
    public string class_id;
    public string teacher_id;
    public string lesson_title;
}

[System.Serializable]
public class LessonEditResponse
{
    public bool success;
    public LessonEditData lesson;
}

[System.Serializable]
public class LessonEditData
{
    public string lesson_id;
    public string class_id;
    public string teacher_id;
    public string lesson_title;
    public string lesson_name;
    public string lesson_info;
    public string lesson_img;
    public string lesson_img_url;
    public string lesson_pdf_url;
    public string exercise_pdf_url;
    public string time_exercise;
    public string deadline_date;
    public string deadline_time;
    public string quiz_id;
    public string video_url;
    public System.Collections.Generic.List<ModelData> models;
}

// [System.Serializable]
// public class UploadQuizResponse
// {
//     public bool success;
//     public QuizUploadData quiz;
// }

[System.Serializable]
public class QuizUploadData
{
    public string quiz_id;
    public string quiz_pdf_url;
}
