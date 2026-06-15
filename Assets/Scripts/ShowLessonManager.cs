using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ShowLessonManager : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text lessonTitleText;
    public Button openPdfButton;
    public Button updateBtn;
    public Button startExerciseBtn;
    public TMP_Text messageText;

    [Header("API")]
    public string baseUrl = "http://localhost:4000/api";
    // public string baseUrl = " https://e571-14-241-225-251.ngrok-free.app/api";

    [Header("Scene")]
    public string createLessonSceneName = "CreateLessonScene";
    public string lessonInClassSceneName = "LessonInClass";
    public string pdfViewerSceneName = "PdfViewerScene";
    public string doExerciseSceneName = "DoExerciseScene";

    private string lessonId = "";
    private string pdfUrl = "";
    private LessonData currentLesson;

    [System.Serializable]
    public class LessonData
    {
        public string lesson_id;
        public string class_id;
        public string teacher_id;
        public string quiz_id;

        public string lesson_title;
        public string lesson_name;
        public string lesson_info;
        public string lesson_img_url;
        public string lesson_pdf_url;
        public string exercise_pdf_url;
        public string time_exercise;
        public string deadline_date;
        public string deadline_time;
    }

    [System.Serializable]
    public class LessonResponse
    {
        public bool success;
        public LessonData lesson;
    }

    private void Start()
    {
        lessonId = PlayerPrefs.GetString("selected_lesson_id", "");

        if (messageText != null)
            messageText.text = "";

        if (openPdfButton != null)
        {
            openPdfButton.interactable = false;
            openPdfButton.onClick.RemoveAllListeners();
            openPdfButton.onClick.AddListener(OpenPdf);
        }

        if (updateBtn != null)
        {
            updateBtn.gameObject.SetActive(false);
            updateBtn.onClick.RemoveAllListeners();
            updateBtn.onClick.AddListener(OnUpdateLessonClicked);
        }

        if (startExerciseBtn != null)
        {
            startExerciseBtn.onClick.RemoveAllListeners();
            startExerciseBtn.onClick.AddListener(OpenDoExerciseScene);
        }

        if (string.IsNullOrEmpty(lessonId))
        {
            ShowMessage("Không tìm thấy selected_lesson_id.");
            return;
        }

        StartCoroutine(LoadLessonData());
    }

    private IEnumerator LoadLessonData()
    {
        string url = baseUrl + "/lessons/" + lessonId;

        Debug.Log("Load lesson detail URL: " + url);

        UnityWebRequest request = UnityWebRequest.Get(url);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            ShowMessage("Không lấy được thông tin bài học.");
            Debug.LogError("Load lesson error: " + request.error);
            Debug.LogError("Server response: " + request.downloadHandler.text);
            yield break;
        }

        LessonResponse response =
            JsonUtility.FromJson<LessonResponse>(request.downloadHandler.text);

        if (response == null || !response.success || response.lesson == null)
        {
            ShowMessage("Dữ liệu bài học không hợp lệ.");
            yield break;
        }

        currentLesson = response.lesson;

        if (lessonTitleText != null)
        {
            lessonTitleText.text = !string.IsNullOrEmpty(currentLesson.lesson_title)
                ? currentLesson.lesson_title
                : currentLesson.lesson_name;
        }

        pdfUrl = currentLesson.lesson_pdf_url;

        if (!string.IsNullOrEmpty(currentLesson.quiz_id))
        {
            PlayerPrefs.SetString("selected_quiz_id", currentLesson.quiz_id);
            PlayerPrefs.Save();

            Debug.Log("Saved selected_quiz_id = " + currentLesson.quiz_id);
        }
        else
        {
            PlayerPrefs.DeleteKey("selected_quiz_id");
            PlayerPrefs.Save();

            Debug.LogWarning("Bài học này chưa có quiz_id.");
        }

        if (string.IsNullOrEmpty(pdfUrl))
        {
            ShowMessage("Bài học này chưa có file PDF.");

            if (openPdfButton != null)
                openPdfButton.interactable = false;
        }
        else
        {
            if (openPdfButton != null)
                openPdfButton.interactable = true;

            ShowMessage("");
        }

        UpdateTeacherButtonVisibility();

        Debug.Log("Lesson PDF URL: " + pdfUrl);
    }

    private void UpdateTeacherButtonVisibility()
    {
        if (updateBtn == null || currentLesson == null)
            return;

        string currentRole = PlayerPrefs.GetString("current_role", "");
        string userId = PlayerPrefs.GetString("user_id", "");

        bool isTeacherOwner =
            currentRole == "teacher" &&
            !string.IsNullOrEmpty(userId) &&
            userId == currentLesson.teacher_id;

        updateBtn.gameObject.SetActive(isTeacherOwner);
    }

    public void OnUpdateLessonClicked()
    {
        if (currentLesson == null)
        {
            ShowMessage("Không tìm thấy dữ liệu bài học để cập nhật.");
            return;
        }

        PlayerPrefs.SetInt("is_edit_lesson", 1);
        PlayerPrefs.SetString("edit_lesson_id", currentLesson.lesson_id);
        PlayerPrefs.SetString("edit_class_id", currentLesson.class_id);
        PlayerPrefs.SetString("selected_class_id", currentLesson.class_id);

        PlayerPrefs.SetString("edit_lesson_title",
            !string.IsNullOrEmpty(currentLesson.lesson_title)
                ? currentLesson.lesson_title
                : currentLesson.lesson_name
        );

        PlayerPrefs.SetString("edit_lesson_description", currentLesson.lesson_info);
        PlayerPrefs.SetString("edit_lesson_img_url", currentLesson.lesson_img_url);
        PlayerPrefs.SetString("edit_lesson_pdf_url", currentLesson.lesson_pdf_url);
        PlayerPrefs.SetString("edit_exercise_pdf_url", currentLesson.exercise_pdf_url);
        PlayerPrefs.SetString("edit_quiz_id", currentLesson.quiz_id);

        PlayerPrefs.Save();

        LessonTopBarPrefabManager.GoToScene(createLessonSceneName);
    }

    public void OpenPdf()
    {
        if (string.IsNullOrEmpty(pdfUrl))
        {
            ShowMessage("Không tìm thấy file PDF.");
            return;
        }

        PlayerPrefs.SetString("pdf_url", pdfUrl);
        PlayerPrefs.Save();

        LessonTopBarPrefabManager.GoToScene(pdfViewerSceneName);
    }

    public void OpenDoExerciseScene()
    {
        if (currentLesson == null)
        {
            ShowMessage("Chưa tải xong dữ liệu bài học.");
            return;
        }

        if (string.IsNullOrEmpty(currentLesson.quiz_id))
        {
            ShowMessage("Bài học này chưa có bài tập.");
            return;
        }

        string currentRole = PlayerPrefs.GetString("current_role", "").Trim().ToLower();

        string lessonTitle = !string.IsNullOrEmpty(currentLesson.lesson_title)
            ? currentLesson.lesson_title
            : currentLesson.lesson_name;

        PlayerPrefs.SetString("selected_lesson_id", currentLesson.lesson_id);
        PlayerPrefs.SetString("selected_quiz_id", currentLesson.quiz_id);
        PlayerPrefs.SetString("exercise_role", currentRole);

        // thêm dòng này
        PlayerPrefs.SetString("current_lesson_title", lessonTitle);

        PlayerPrefs.Save();

        Debug.Log("Open DoExerciseScene with role = " + currentRole);
        Debug.Log("Saved current_lesson_title = " + lessonTitle);

        LessonTopBarPrefabManager.GoToScene(doExerciseSceneName);
    }

    public void OnBackButtonClicked()
    {
        LessonTopBarPrefabManager.GoToScene(lessonInClassSceneName);
    }

    private void ShowMessage(string msg)
    {
        if (messageText != null)
            messageText.text = msg;
    }
}