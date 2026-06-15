using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;

public class DoExerciseManager : MonoBehaviour
{
    [Header("Top Bar")]
    public TMP_Text titleText;
    [Header("Panels")]
    public GameObject exerciseIntroPanelTeacher;
    public GameObject exerciseIntroPanelStudent;
    public GameObject quizDoingPanel;
    public GameObject exerciseResultScrollView;

    [Header("Buttons")]
    public Button startButtonIntro;
    public Button startButtonResult;
    public Button submitButton;

    [Header("API")]
    public string baseUrl = "http://localhost:4000/api";

    private string currentRole = "";
    private string studentId = "";
    private string lessonId = "";

    private void Awake()
    {
        HideAllPanels();
    }

    private void Start()
    {
        currentRole = PlayerPrefs.GetString("exercise_role",
                    PlayerPrefs.GetString("current_role", ""))
                    .Trim()
                    .ToLower();

        studentId = PlayerPrefs.GetString("user_id", "");
        lessonId = PlayerPrefs.GetString("selected_lesson_id", "");

        UpdateTopBarTitle();

        InitButtons();
        HideAllPanels();

        if (currentRole == "teacher")
        {
            Debug.Log("ROLE TEACHER => Show ExerciseIntroPanelTeacher");
            ShowTeacherIntroPanel();
            return;
        }

        if (currentRole == "student")
        {
            Debug.Log("ROLE STUDENT => Check attempt");
            StartCoroutine(CheckStudentAttempt());
            return;
        }

        ShowStudentIntroPanel();
    }

    private void UpdateTopBarTitle()
    {
        string lessonTitle = PlayerPrefs.GetString("current_lesson_title", "");

        if (titleText != null && !string.IsNullOrEmpty(lessonTitle))
        {
            titleText.text = lessonTitle;
            Debug.Log("Updated DoExercise title = " + lessonTitle);
        }
        else
        {
            Debug.LogWarning("Không tìm thấy titleText hoặc current_lesson_title rỗng.");
        }
    }

    private void InitButtons()
    {
        if (startButtonIntro != null)
        {
            startButtonIntro.onClick.RemoveAllListeners();
            startButtonIntro.onClick.AddListener(StartQuiz);
        }

        if (startButtonResult != null)
        {
            startButtonResult.onClick.RemoveAllListeners();
            startButtonResult.onClick.AddListener(StartQuiz);
        }

        if (submitButton != null)
        {
            submitButton.onClick.RemoveAllListeners();
            submitButton.onClick.AddListener(SubmitQuiz);
        }
    }

    private void HideAllPanels()
    {
        if (exerciseIntroPanelTeacher != null)
            exerciseIntroPanelTeacher.SetActive(false);

        if (exerciseIntroPanelStudent != null)
            exerciseIntroPanelStudent.SetActive(false);

        if (quizDoingPanel != null)
            quizDoingPanel.SetActive(false);

        if (exerciseResultScrollView != null)
            exerciseResultScrollView.SetActive(false);
    }

    private void ShowTeacherIntroPanel()
    {
        HideAllPanels();

        if (exerciseIntroPanelTeacher != null)
            exerciseIntroPanelTeacher.SetActive(true);

        if (exerciseResultScrollView != null)
            exerciseResultScrollView.SetActive(false);
    }

    private void ShowStudentIntroPanel()
    {
        HideAllPanels();

        if (exerciseIntroPanelStudent != null)
            exerciseIntroPanelStudent.SetActive(true);
    }

    public void ShowQuizDoingPanel()
    {
        HideAllPanels();

        if (quizDoingPanel != null)
            quizDoingPanel.SetActive(true);
    }

    public void ShowResultPanel()
    {
        if (currentRole == "teacher")
        {
            Debug.LogWarning("Teacher không được mở ExerciseResultScrollView.");
            ShowTeacherIntroPanel();
            return;
        }

        HideAllPanels();

        if (exerciseResultScrollView != null)
            exerciseResultScrollView.SetActive(true);
    }

    public void StartQuiz()
    {
        if (currentRole == "teacher")
        {
            Debug.Log("Teacher bấm bắt đầu => vẫn giữ Teacher Intro Panel.");
            ShowTeacherIntroPanel();
            return;
        }

        ShowQuizDoingPanel();
    }

    public void SubmitQuiz()
    {
        if (currentRole != "student")
        {
            ShowTeacherIntroPanel();
            return;
        }

        StartCoroutine(SaveQuizAttemptCoroutine());
    }

    private IEnumerator CheckStudentAttempt()
    {
        if (currentRole != "student")
        {
            ShowTeacherIntroPanel();
            yield break;
        }

        if (string.IsNullOrEmpty(studentId) || string.IsNullOrEmpty(lessonId))
        {
            Debug.LogError("Thiếu studentId hoặc lessonId.");
            ShowStudentIntroPanel();
            yield break;
        }

        string url = baseUrl + "/students/" + studentId + "/lessons/" + lessonId + "/attempts";

        Debug.Log("Check attempts URL: " + url);

        UnityWebRequest request = UnityWebRequest.Get(url);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Check attempts failed: " + request.error);
            Debug.LogError("Response: " + request.downloadHandler.text);

            ShowStudentIntroPanel();
            yield break;
        }

        string json = request.downloadHandler.text;
        Debug.Log("Check attempts JSON: " + json);

        QuizAttemptResponse response =
            JsonUtility.FromJson<QuizAttemptResponse>(json);

        if (response != null && response.success && response.attempts != null && response.attempts.Count > 0)
        {
            ShowResultPanel();
        }
        else
        {
            ShowStudentIntroPanel();
        }
    }

    private IEnumerator SaveQuizAttemptCoroutine()
    {
        if (currentRole != "student")
        {
            ShowTeacherIntroPanel();
            yield break;
        }

        if (string.IsNullOrEmpty(studentId) || string.IsNullOrEmpty(lessonId))
        {
            ShowResultPanel();
            yield break;
        }

        string url = baseUrl + "/quiz-attempts";

        QuizAttemptRequest data = new QuizAttemptRequest();
        data.student_id = studentId;
        data.lesson_id = lessonId;
        data.score = 0f;
        data.duration = "";
        data.status = "completed";

        string json = JsonUtility.ToJson(data);

        UnityWebRequest request = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);

        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Save attempt failed: " + request.error);
            Debug.LogError("Response: " + request.downloadHandler.text);

            ShowResultPanel();
            yield break;
        }

        Debug.Log("Save attempt response: " + request.downloadHandler.text);
        ShowResultPanel();
    }
}

[System.Serializable]
public class AttemptStatusResponse
{
    public bool success;
    public bool has_attempt;
}

[System.Serializable]
public class QuizAttemptRequest
{
    public string student_id;
    public string lesson_id;
    public float score;
    public string duration;
    public string status;
}