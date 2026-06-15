using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class DoingQuizManagerTest : MonoBehaviour
{
    [Header("Quiz Doing UI")]
    public Image questionImage;
    public TMP_Text questionProgressText;
    public TMP_Text questionTitleText;
    [Header("API")]
    public string apiBaseUrl = "http://localhost:4000/api";
    // public string apiBaseUrl = "https://e571-14-241-225-251.ngrok-free.app/api";

    [Header("Panels")]
    public GameObject exerciseIntroPanelStudent;
    public GameObject quizDoingPanel;

    [Header("Intro Texts")]
    public TMP_Text openTimeText;
    public TMP_Text closeTimeText;
    public TMP_Text highestScoreText;

    

    [Header("Buttons")]
    public Button startBtn;
    // public Button backBtn;
    public Button nextBtn;
    public Button previousBtn;
    public Button submitBtn;

    private string classId;
    private string lessonId;
    private string quizId;

    private QuizQuestionData[] questions;
    private int currentQuestionIndex = 0;

    private void Start()
    {
        classId = PlayerPrefs.GetString("selected_class_id", "");
        lessonId = PlayerPrefs.GetString("selected_lesson_id", "");
        quizId = PlayerPrefs.GetString("selected_quiz_id", "");

        Debug.Log("DoingQuiz classId = " + classId);
        Debug.Log("DoingQuiz lessonId = " + lessonId);
        Debug.Log("DoingQuiz quizId = " + quizId);

        ShowIntroPanel();

        if (startBtn != null)
        {
            startBtn.onClick.RemoveAllListeners();
            startBtn.onClick.AddListener(StartQuiz);
        }

        // if (backBtn != null)
        // {
        //     backBtn.onClick.RemoveAllListeners();
        //     backBtn.onClick.AddListener(() =>
        //     {
        //         SceneManager.LoadScene("LessonInClass");
        //     });
        // }

        if (nextBtn != null)
        {
            nextBtn.onClick.RemoveAllListeners();
            nextBtn.onClick.AddListener(NextQuestion);
        }

        if (previousBtn != null)
        {
            previousBtn.onClick.RemoveAllListeners();
            previousBtn.onClick.AddListener(PreviousQuestion);
        }

        if (submitBtn != null)
        {
            submitBtn.onClick.RemoveAllListeners();
            submitBtn.onClick.AddListener(() =>
            {
                Debug.Log("Submit quiz clicked.");
            });
        }

        if (!string.IsNullOrEmpty(quizId))
        {
            Debug.Log("Dùng quizId từ PlayerPrefs, không cần load lại quiz theo lesson.");
        }
        else
        {
            StartCoroutine(LoadQuizInfo());
        }
    }

    

    private void ShowIntroPanel()
    {
        if (exerciseIntroPanelStudent != null)
            exerciseIntroPanelStudent.SetActive(true);

        if (quizDoingPanel != null)
            quizDoingPanel.SetActive(false);
    }

    private void StartQuiz()
    {
        if (exerciseIntroPanelStudent != null)
            exerciseIntroPanelStudent.SetActive(false);

        if (quizDoingPanel != null)
            quizDoingPanel.SetActive(true);

        StartCoroutine(LoadQuestionImages());
    }

    private IEnumerator LoadQuizInfo()
    {
        if (string.IsNullOrEmpty(classId) || string.IsNullOrEmpty(lessonId))
        {
            Debug.LogError("Thiếu selected_class_id hoặc selected_lesson_id.");
            yield break;
        }

        string url = apiBaseUrl + "/classes/" + classId + "/lessons/" + lessonId + "/quiz";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Load quiz failed: " + request.error);
                Debug.LogError("Response: " + request.downloadHandler.text);
                yield break;
            }

            QuizResponse response = JsonUtility.FromJson<QuizResponse>(request.downloadHandler.text);

            if (response == null || !response.success || response.quiz == null)
            {
                Debug.LogError("Không đọc được dữ liệu quiz.");
                yield break;
            }

            UpdateIntroUI(response.quiz);
        }
    }

    private void UpdateIntroUI(QuizDataTest quiz)
    {
        quizId = quiz.quiz_id;

        Debug.Log("QuizId lấy từ backend = " + quizId);

        PlayerPrefs.SetString("selected_quiz_id", quizId);
        PlayerPrefs.Save();

        if (openTimeText != null)
            openTimeText.text = FormatOpenTime(quiz.open_time);

        if (closeTimeText != null)
            closeTimeText.text = FormatDeadline(quiz.deadline_date, quiz.deadline_time);

        if (highestScoreText != null)
            highestScoreText.text = "Chưa có điểm";
    }

    private string BuildQuestionImageUrl(string classId, string lessonId, string quizId, string questionId)
    {
        string storageBucket = "virtual-education-d056a.firebasestorage.app";

        return "https://storage.googleapis.com/"
            + storageBucket
            + "/quiz_questions/"
            + classId
            + "/"
            + lessonId
            + "/"
            + quizId
            + "/"
            + questionId
            + ".png";
    }

    private IEnumerator LoadQuestionImages()
    {
        if (string.IsNullOrEmpty(classId))
            classId = PlayerPrefs.GetString("selected_class_id", "");

        if (string.IsNullOrEmpty(lessonId))
            lessonId = PlayerPrefs.GetString("selected_lesson_id", "");

        if (string.IsNullOrEmpty(quizId))
            quizId = PlayerPrefs.GetString("selected_quiz_id", "");

        if (string.IsNullOrEmpty(classId))
        {
            Debug.LogError("Thiếu classId.");
            yield break;
        }

        if (string.IsNullOrEmpty(lessonId))
        {
            Debug.LogError("Thiếu lessonId.");
            yield break;
        }

        if (string.IsNullOrEmpty(quizId))
        {
            Debug.LogError("Thiếu quizId.");
            yield break;
        }

        int totalQuestions = 0;

        yield return StartCoroutine(GetTotalQuestionsFromApi(classId, lessonId, quizId, (total) =>
        {
            totalQuestions = total;
        }));

        if (totalQuestions <= 0)
        {
            Debug.LogError("Không lấy được totalQuestions hoặc totalQuestions = 0.");
            yield break;
        }

        questions = new QuizQuestionData[totalQuestions];

        for (int i = 0; i < totalQuestions; i++)
        {
            int questionOrder = i + 1;
            string questionId = "question_" + questionOrder;

            questions[i] = new QuizQuestionData
            {
                question_id = questionId,
                question_order = questionOrder,
                question_image_url = BuildQuestionImageUrl(classId, lessonId, quizId, questionId),
                correct_answer = ""
            };

            Debug.Log("Question " + questionOrder + " URL = " + questions[i].question_image_url);
        }

        currentQuestionIndex = 0;

        yield return StartCoroutine(ShowCurrentQuestion());
    }

    private IEnumerator GetTotalQuestionsFromApi(
        string classId,
        string lessonId,
        string quizId,
        System.Action<int> onDone
    )
    {
        string storageBucket = "virtual-education-d056a.firebasestorage.app";

        string url = "https://storage.googleapis.com/"
            + storageBucket
            + "/quiz_questions/"
            + classId
            + "/"
            + lessonId
            + "/"
            + quizId
            + "/questions";

        Debug.Log("Get total questions URL = " + url);

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Get total questions failed: " + request.error);
                Debug.LogError("Response: " + request.downloadHandler.text);

                if (onDone != null)
                    onDone(0);

                yield break;
            }

            QuizQuestionsResponse response =
                JsonUtility.FromJson<QuizQuestionsResponse>(request.downloadHandler.text);

            if (response == null || !response.success)
            {
                Debug.LogError("Response null hoặc success = false.");

                if (onDone != null)
                    onDone(0);

                yield break;
            }

            if (onDone != null)
                onDone(response.total_questions);
        }
    }

    private IEnumerator ShowCurrentQuestion()
    {
        if (questions == null || questions.Length == 0)
            yield break;

        QuizQuestionData question = questions[currentQuestionIndex];

        if (questionProgressText != null)
            questionProgressText.text = (currentQuestionIndex + 1) + "/" + questions.Length;

        if (questionTitleText != null)
            questionTitleText.text = "Câu " + question.question_order;

        string imageUrl = question.question_image_url;

        Debug.Log("Question image URL = " + imageUrl);

        if (string.IsNullOrEmpty(imageUrl))
        {
            Debug.LogError("Question image URL rỗng.");
            yield break;
        }

        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageUrl))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Load question image failed: " + request.error);
                Debug.LogError("Image URL = " + imageUrl);
                yield break;
            }

            Texture2D texture = DownloadHandlerTexture.GetContent(request);

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f)
            );

            FitQuestionImageToWidth(sprite);
        }
    }

    private void NextQuestion()
    {
        if (questions == null || questions.Length == 0)
            return;

        if (currentQuestionIndex < questions.Length - 1)
        {
            currentQuestionIndex++;
            StartCoroutine(ShowCurrentQuestion());
        }
    }

    private void PreviousQuestion()
    {
        if (questions == null || questions.Length == 0)
            return;

        if (currentQuestionIndex > 0)
        {
            currentQuestionIndex--;
            StartCoroutine(ShowCurrentQuestion());
        }
    }

    private string FormatOpenTime(string openTime)
    {
        if (string.IsNullOrEmpty(openTime))
            return "Không có dữ liệu";

        DateTime dt;

        string[] formats =
        {
            "HH:mm:ss dd/M/yyyy",
            "H:mm:ss dd/M/yyyy",
            "HH:mm:ss d/M/yyyy",
            "H:mm:ss d/M/yyyy",
            "dd/MM/yyyy HH:mm:ss",
            "d/M/yyyy HH:mm:ss",
            "dd/M/yyyy HH:mm:ss",
            "d/MM/yyyy HH:mm:ss"
        };

        if (DateTime.TryParseExact(
            openTime,
            formats,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out dt))
        {
            return dt.ToString("dd/MM/yyyy HH:mm:ss");
        }

        return openTime;
    }

    private string FormatDeadline(string date, string time)
    {
        if (string.IsNullOrEmpty(date) && string.IsNullOrEmpty(time))
            return "Chưa có hạn";

        string raw = date + " " + time;
        DateTime dt;

        string[] formats =
        {
            "dd/MM/yyyy HH:mm:ss",
            "dd/MM/yyyy H:mm:ss",
            "d/M/yyyy HH:mm:ss",
            "d/M/yyyy H:mm:ss",
            "yyyy-MM-dd HH:mm:ss"
        };

        if (DateTime.TryParseExact(
            raw,
            formats,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out dt))
        {
            return dt.ToString("dd/MM/yyyy HH:mm:ss");
        }

        return raw;
    }

    private void FitQuestionImageToWidth(Sprite sprite)
    {
        if (questionImage == null || sprite == null)
        {
            return;
        }

        questionImage.sprite = sprite;
        questionImage.preserveAspect = true;
        questionImage.color = Color.white;

        RectTransform imageRect = questionImage.GetComponent<RectTransform>();

        if (imageRect == null || imageRect.parent == null)
        {
            return;
        }

        RectTransform parentRect = imageRect.parent.GetComponent<RectTransform>();

        if (parentRect == null)
        {
            return;
        }

        float parentWidth = parentRect.rect.width;

        if (parentWidth <= 0)
        {
            parentWidth = 700f;
        }

        float imageRatio = sprite.rect.height / sprite.rect.width;
        float newHeight = parentWidth * imageRatio;

        imageRect.anchorMin = new Vector2(0.5f, 1f);
        imageRect.anchorMax = new Vector2(0.5f, 1f);
        imageRect.pivot = new Vector2(0.5f, 1f);

        imageRect.sizeDelta = new Vector2(parentWidth, newHeight);
        imageRect.anchoredPosition = Vector2.zero;
        imageRect.localScale = Vector3.one;

        LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
    }
}

[Serializable]
public class QuizResponse
{
    public bool success;
    public string message;
    public QuizDataTest quiz;
}

[Serializable]
public class QuizDataTest
{
    public string id;
    public string quiz_id;
    public string class_id;
    public string lesson_id;
    public string teacher_id;
    public string quiz_pdf_url;
    public string quiz_pdf_name;
    public string open_time;
    public string deadline_date;
    public string deadline_time;
}

// [System.Serializable]
// public class QuizQuestionsResponse
// {
//     public bool success;
//     public string quiz_id;
//     public string class_id;
//     public string lesson_id;
//     public int total_questions;
// }
