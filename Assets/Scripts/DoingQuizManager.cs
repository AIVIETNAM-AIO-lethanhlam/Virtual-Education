using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

[Serializable]
public class QuizQuestionData
{
    public string question_id;
    public int question_order;
    public string question_image_url;
    public string correct_answer;
    public string created_at;
}

[Serializable]
public class QuizQuestionsResponse
{
    public bool success;
    public string quiz_id;
    public string class_id;
    public string lesson_id;
    public int total_questions;
    public QuizQuestionData[] questions;
}

[System.Serializable]
public class AttemptRequest
{
    public string quiz_id;
    public string score;
    public int correct_count;
    public int total_questions;
    public int duration_seconds;
    public string status;
}

public class DoingQuizManager : MonoBehaviour
{
    [Header("Start Again Button")]
    public Button startAgainBtn;
    [Header("Attempt Result UI")]
    public Transform attemptListContent;
    public GameObject attemptItemPrefab;

    [Header("Teacher Buttons")]
    public Button updateBtn;

    [Header("Edit Scene")]
    public string createLessonSceneName = "CreateLessonScene";

    [Header("Exercise Info Text")]
    public TMP_Text openTimeTextStudent;
    public TMP_Text closeTimeTextStudent;

    public TMP_Text openTimeTextTeacher;
    public TMP_Text closeTimeTextTeacher;

    public TMP_Text openTimeTextResult;
    public TMP_Text closeTimeTextResult;

    [Header("Intro Panel")]
    public GameObject exerciseIntroPanelStudent;
    public GameObject exerciseIntroPanelTeacher;

    [Header("Start / Back Buttons")]
    public Button startBtn;
    // public Button backBtn;
    [Header("API")]
    public string apiBaseUrl = "http://localhost:4000/api";
    // public string apiBaseUrl = "https://e571-14-241-225-251.ngrok-free.app/api";

    [Header("Panels")]
    public GameObject quizDoingPanel;
    public GameObject exerciseResultScrollView;

    [Header("Question UI")]
    public TMP_Text questionProgressText;
    public TMP_Text questionTitleText;
    public Image questionImage;

    [Header("Answer Buttons")]
    public Button answerAButton;
    public Button answerBButton;
    public Button answerCButton;
    public Button answerDButton;

    [Header("Navigation Buttons")]
    public Button prevBtn;
    public Button nextBtn;
    public Button submitButton;

    [Header("Scene")]
    public string showLessonSceneName = "ShowLessonScene";

    private string classId = "";
    private string lessonId = "";
    private string quizId = "";

    private QuizQuestionData[] questions;
    private int currentQuestionIndex = 0;

    private Dictionary<string, string> selectedAnswers = new Dictionary<string, string>();

    private Color normalButtonColor = Color.white;
    private Color selectedAnswerColor = new Color(0.5f, 0.85f, 1f, 1f);

    private void Start()
    {
        classId = PlayerPrefs.GetString("selected_class_id", "");
        lessonId = PlayerPrefs.GetString("selected_lesson_id", "");
        quizId = PlayerPrefs.GetString("selected_quiz_id", "");

        InitButtons();
        UpdateExerciseInfoTexts();

        string currentRole = PlayerPrefs.GetString("current_role", "");

        if (exerciseResultScrollView != null)
            exerciseResultScrollView.SetActive(false);

        if (currentRole == "teacher")
        {
            if (exerciseIntroPanelTeacher != null)
                exerciseIntroPanelTeacher.SetActive(true);

            if (exerciseIntroPanelStudent != null)
                exerciseIntroPanelStudent.SetActive(false);

            if (quizDoingPanel != null)
                quizDoingPanel.SetActive(false);

            return;
        }

        if (exerciseIntroPanelTeacher != null)
            exerciseIntroPanelTeacher.SetActive(false);

        if (exerciseIntroPanelStudent != null)
            exerciseIntroPanelStudent.SetActive(true);

        if (quizDoingPanel != null)
            quizDoingPanel.SetActive(false);
    }

    private void InitButtons()
    {
        if (startBtn != null)
        {
            startBtn.onClick.RemoveAllListeners();
            startBtn.onClick.AddListener(StartQuiz);
        }

        if (startAgainBtn != null)
        {
            startAgainBtn.onClick.RemoveAllListeners();
            startAgainBtn.onClick.AddListener(StartQuiz);
        }

        // if (backBtn != null)
        // {
        //     backBtn.onClick.RemoveAllListeners();
        //     backBtn.onClick.AddListener(OnBackButtonClicked);
        // }

        if (prevBtn != null)
        {
            prevBtn.onClick.RemoveAllListeners();
            prevBtn.onClick.AddListener(PreviousQuestion);
        }

        if (nextBtn != null)
        {
            nextBtn.onClick.RemoveAllListeners();
            nextBtn.onClick.AddListener(NextQuestion);
        }

        if (submitButton != null)
        {
            submitButton.onClick.RemoveAllListeners();
            submitButton.onClick.AddListener(SubmitQuiz);
        }

        if (answerAButton != null)
        {
            answerAButton.onClick.RemoveAllListeners();
            answerAButton.onClick.AddListener(() => SelectAnswer("A"));
        }

        if (answerBButton != null)
        {
            answerBButton.onClick.RemoveAllListeners();
            answerBButton.onClick.AddListener(() => SelectAnswer("B"));
        }

        if (answerCButton != null)
        {
            answerCButton.onClick.RemoveAllListeners();
            answerCButton.onClick.AddListener(() => SelectAnswer("C"));
        }

        if (answerDButton != null)
        {
            answerDButton.onClick.RemoveAllListeners();
            answerDButton.onClick.AddListener(() => SelectAnswer("D"));
        }

        if (updateBtn != null)
        {
            updateBtn.onClick.RemoveAllListeners();
            updateBtn.onClick.AddListener(OnUpdateLessonClicked);
        }
    }

    // private void OnBackButtonClicked()
    // {
    //     LessonTopBarPrefabManager.GoToScene(showLessonSceneName);
    // }

    public  void StartQuiz()
    {
        Debug.Log("========== START QUIZ ==========");

        ResetQuizSession();

        classId = PlayerPrefs.GetString("selected_class_id", "");
        lessonId = PlayerPrefs.GetString("selected_lesson_id", "");
        quizId = PlayerPrefs.GetString("selected_quiz_id", "");

        Debug.Log("classId = " + classId);
        Debug.Log("lessonId = " + lessonId);
        Debug.Log("quizId = " + quizId);

        if (exerciseIntroPanelStudent != null)
            exerciseIntroPanelStudent.SetActive(false);

        if (exerciseIntroPanelTeacher != null)
            exerciseIntroPanelTeacher.SetActive(false);

        if (exerciseResultScrollView != null)
            exerciseResultScrollView.SetActive(false);

        if (quizDoingPanel != null)
            quizDoingPanel.SetActive(true);

        StartCoroutine(LoadQuestionImages());
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

        if (string.IsNullOrEmpty(classId) || string.IsNullOrEmpty(lessonId) || string.IsNullOrEmpty(quizId))
        {
            Debug.LogError("Thiếu classId / lessonId / quizId.");
            yield break;
        }
        Debug.LogError(
            "Missing IDs => " +
            "classId = [" + classId + "] " +
            "lessonId = [" + lessonId + "] " +
            "quizId = [" + quizId + "]"
        );

       QuizQuestionData[] loadedQuestions = null;
       Debug.Log("CALL GetQuestionsFromApi()");

        yield return StartCoroutine(GetQuestionsFromApi((result) =>
        {
            loadedQuestions = result;
        }));

        Debug.Log("RETURN FROM GetQuestionsFromApi()");

        if (loadedQuestions == null)
        {
            Debug.LogError("loadedQuestions == null");
        }
        else
        {
            Debug.Log("loadedQuestions.Length = " + loadedQuestions.Length);
        }


        if (loadedQuestions == null || loadedQuestions.Length == 0)
        {
            Debug.LogError("Không load được questions.");
            yield break;
        }

        questions = loadedQuestions;

        for (int i = 0; i < questions.Length; i++)
        {
            if (string.IsNullOrEmpty(questions[i].question_image_url))
            {
                questions[i].question_image_url = BuildQuestionImageUrl(
                    classId,
                    lessonId,
                    quizId,
                    questions[i].question_id
                );
            }

            Debug.Log(
                questions[i].question_id +
                " correct_answer = " +
                questions[i].correct_answer
            );
        }

        currentQuestionIndex = 0;

        yield return StartCoroutine(ShowCurrentQuestion());
    }

    private IEnumerator GetQuestionsFromApi(System.Action<QuizQuestionData[]> onDone)
    {
        Debug.Log("========== GET QUESTIONS FROM API ==========");

        string url = apiBaseUrl + "/quizzes/" + quizId + "/questions";

        Debug.Log("Get questions API URL = " + url);

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("ngrok-skip-browser-warning", "true");

            Debug.Log("Sending request...");
            yield return request.SendWebRequest();

            Debug.Log("Request completed");
            Debug.Log("HTTP CODE = " + request.responseCode);

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("========== GET QUESTIONS FAILED ==========");
                Debug.LogError("request.error = " + request.error);
                Debug.LogError("responseCode = " + request.responseCode);
                Debug.LogError("Response = " + request.downloadHandler.text);

                onDone(null);
                yield break;
            }

            string json = request.downloadHandler.text;
            Debug.Log("Questions JSON = " + json);

            QuizQuestionsResponse response =
                JsonUtility.FromJson<QuizQuestionsResponse>(json);

            if (response == null)
            {
                Debug.LogError("Questions response null.");
                onDone(null);
                yield break;
            }

            Debug.Log("response.success = " + response.success);
            Debug.Log("response.quiz_id = " + response.quiz_id);
            Debug.Log("response.total_questions = " + response.total_questions);

            if (!response.success)
            {
                Debug.LogError("Questions response success = false.");
                onDone(null);
                yield break;
            }

            if (response.questions == null || response.questions.Length == 0)
            {
                Debug.LogError("response.questions null hoặc rỗng.");
                onDone(null);
                yield break;
            }

            for (int i = 0; i < response.questions.Length; i++)
            {
                Debug.Log(
                    "Question " + i +
                    " | id = " + response.questions[i].question_id +
                    " | image = " + response.questions[i].question_image_url +
                    " | answer = " + response.questions[i].correct_answer
                );
            }

            onDone(response.questions);
        }
    }

    private string GetCorrectAnswerByQuestionId(string questionId)
    {
        string[] answers = { "B", "C", "C", "B", "C" };

        if (string.IsNullOrEmpty(questionId))
            return "";

        if (!questionId.StartsWith("question_"))
            return "";

        string numberText = questionId.Replace("question_", "");

        int index;

        if (!int.TryParse(numberText, out index))
            return "";

        index = index - 1;

        if (index < 0 || index >= answers.Length)
            return "";

        return answers[index];
    }
    private IEnumerator ShowCurrentQuestion()
    {
        Debug.Log("========== SHOW CURRENT QUESTION ==========");

        if (questions == null || questions.Length == 0)
        {
            Debug.LogError("questions null hoặc rỗng.");
            yield break;
        }

        if (currentQuestionIndex < 0 || currentQuestionIndex >= questions.Length)
        {
            Debug.LogError("currentQuestionIndex không hợp lệ: " + currentQuestionIndex);
            yield break;
        }

        QuizQuestionData question = questions[currentQuestionIndex];

        if (question == null)
        {
            Debug.LogError("question hiện tại bị null.");
            yield break;
        }

        Debug.Log("currentQuestionIndex = " + currentQuestionIndex);
        Debug.Log("question_id = " + question.question_id);
        Debug.Log("question_order = " + question.question_order);
        Debug.Log("question_image_url = " + question.question_image_url);
        Debug.Log("correct_answer = " + question.correct_answer);

        if (questionProgressText != null)
            questionProgressText.text = (currentQuestionIndex + 1) + "/" + questions.Length;

        if (questionTitleText != null)
            questionTitleText.text = "Câu " + question.question_order;

        ResetAnswerButtonColors();

        if (questionImage != null)
        {
            questionImage.sprite = null;
            questionImage.color = new Color(1f, 1f, 1f, 0f);
        }
        else
        {
            Debug.LogError("questionImage chưa được gắn trong Inspector.");
            yield break;
        }

        if (string.IsNullOrEmpty(question.question_image_url))
        {
            Debug.LogError("question_image_url rỗng.");
            yield break;
        }

        Debug.Log("START DOWNLOAD IMAGE");
        Debug.Log("Load image URL = " + question.question_image_url);

        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(question.question_image_url))
        {
            yield return request.SendWebRequest();

            Debug.Log("IMAGE DOWNLOAD DONE");
            Debug.Log("IMAGE HTTP CODE = " + request.responseCode);

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("========== LOAD IMAGE FAILED ==========");
                Debug.LogError("request.error = " + request.error);
                Debug.LogError("responseCode = " + request.responseCode);
                Debug.LogError("Image URL = " + question.question_image_url);
                yield break;
            }

            Texture2D texture = DownloadHandlerTexture.GetContent(request);

            if (texture == null)
            {
                Debug.LogError("Texture null sau khi download image.");
                yield break;
            }

            Debug.Log("Texture loaded SUCCESS");
            Debug.Log("Texture width = " + texture.width);
            Debug.Log("Texture height = " + texture.height);

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f)
            );

            questionImage.sprite = sprite;
            questionImage.preserveAspect = true;
            questionImage.color = Color.white;
        }

        UpdateAnswerButtonState();
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

    private void SelectAnswer(string answer)
    {
        if (questions == null || questions.Length == 0)
            return;

        string questionId = questions[currentQuestionIndex].question_id;

        selectedAnswers[questionId] = answer;

        UpdateAnswerButtonState();
    }

    private void UpdateAnswerButtonState()
    {
        ResetAnswerButtonColors();

        if (questions == null || questions.Length == 0)
            return;

        string questionId = questions[currentQuestionIndex].question_id;

        if (!selectedAnswers.ContainsKey(questionId))
            return;

        string answer = selectedAnswers[questionId];

        if (answer == "A") SetButtonColor(answerAButton, selectedAnswerColor);
        if (answer == "B") SetButtonColor(answerBButton, selectedAnswerColor);
        if (answer == "C") SetButtonColor(answerCButton, selectedAnswerColor);
        if (answer == "D") SetButtonColor(answerDButton, selectedAnswerColor);
    }

    private void ResetAnswerButtonColors()
    {
        SetButtonColor(answerAButton, normalButtonColor);
        SetButtonColor(answerBButton, normalButtonColor);
        SetButtonColor(answerCButton, normalButtonColor);
        SetButtonColor(answerDButton, normalButtonColor);
    }

    private void SetButtonColor(Button btn, Color color)
    {
        if (btn == null) return;

        Image img = btn.GetComponent<Image>();

        if (img != null)
            img.color = color;
    }

    // private void SubmitQuiz()
    // {
    //     int correctCount = 0;

    //     for (int i = 0; i < questions.Length; i++)
    //     {
    //         string questionId = questions[i].question_id;

    //         if (selectedAnswers.ContainsKey(questionId))
    //         {
    //             string selected = selectedAnswers[questionId];
    //             string correct = questions[i].correct_answer;

    //             if (selected == correct)
    //                 correctCount++;
    //         }
    //     }

    //     int totalQuestions = questions.Length;
    //     string scoreText = correctCount + "/" + totalQuestions;

    //     string submitTime = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");

    //     string deadlineTime = PlayerPrefs.GetString("quiz_close_time", "Chưa có hạn");

    //     if (quizDoingPanel != null)
    //         quizDoingPanel.SetActive(false);

    //     if (exerciseResultScrollView != null)
    //         exerciseResultScrollView.SetActive(true);

    //     AddAttemptItem(scoreText, submitTime, deadlineTime);
    //     StartCoroutine(SaveAttemptToFirebase(correctCount, totalQuestions, scoreText));
    // }

    private void SubmitQuiz()
    {
        int correctCount = 0;

        Debug.Log("========== SUBMIT QUIZ START ==========");

        if (questions == null)
        {
            Debug.LogError("questions == null");
            return;
        }

        Debug.Log("Total questions = " + questions.Length);
        Debug.Log("Selected answers count = " + selectedAnswers.Count);

        // ===============================
        // DEBUG TOÀN BỘ QUESTIONS
        // ===============================
        Debug.Log("========== DEBUG QUESTIONS ARRAY ==========");

        for (int q = 0; q < questions.Length; q++)
        {
            if (questions[q] == null)
            {
                Debug.LogError("questions[" + q + "] == null");
                continue;
            }

            string questionDocName = "question_" + (q + 1);

            Debug.Log(
                "========== QUESTION DEBUG ==========\n" +
                "Object: " + questions[q] + "\n" +
                "Index: " + q + "\n" +
                "Doc name: " + questionDocName + "\n" +
                "Object JSON:\n" + JsonUtility.ToJson(questions[q], true) + "\n" +
                "created_at" + questions[q].created_at + "\n" +
                "question_id: [" + questions[q].question_id + "]\n" +
                "question_order: [" + questions[q].question_order + "]\n" +
                "question_image_url: [" + questions[q].question_image_url + "]\n" +
                "correct_answer: [" + questions[q].correct_answer + "]\n" +
                questionDocName + ".correct_answer: [" + questions[q].correct_answer + "]\n" +
                "==================================="
            );
        }

        Debug.Log("========== END DEBUG QUESTIONS ARRAY ==========");

        // ===============================
        // DEBUG TOÀN BỘ SELECTED ANSWERS
        // ===============================
        Debug.Log("========== DEBUG SELECTED ANSWERS ==========");

        foreach (KeyValuePair<string, string> pair in selectedAnswers)
        {
            Debug.Log(
                "selectedAnswers key/question_id = [" + pair.Key + "], " +
                "selected answer = [" + pair.Value + "]"
            );
        }

        Debug.Log("========== END DEBUG SELECTED ANSWERS ==========");

        for (int i = 0; i < questions.Length; i++)
        {
            string questionId = questions[i].question_id;
            string correct = questions[i].correct_answer;

            Debug.Log("----------------------------------");
            Debug.Log("Question index = " + i);
            Debug.Log("Question ID = [" + questionId + "]");
            Debug.Log("Correct answer raw = [" + correct + "]");

            if (string.IsNullOrEmpty(correct))
            {
                Debug.LogError("Missing correct_answer for question_id = [" + questionId + "]");
            }

            if (selectedAnswers.ContainsKey(questionId))
            {
                string selected = selectedAnswers[questionId];

                Debug.Log("Selected answer raw = [" + selected + "]");

                string selectedClean = selected.Trim().ToUpper();
                string correctClean = correct.Trim().ToUpper();

                Debug.Log("Selected clean = [" + selectedClean + "]");
                Debug.Log("Correct clean = [" + correctClean + "]");

                if (selectedClean == correctClean)
                {
                    correctCount++;
                    Debug.Log("Result = TRUE / Đúng");
                }
                else
                {
                    Debug.Log("Result = FALSE / Sai");
                }
            }
            else
            {
                Debug.LogWarning("User chưa chọn đáp án cho question_id = [" + questionId + "]");
            }
        }

        int totalQuestions = questions.Length;
        string scoreText = correctCount + "/" + totalQuestions;

        Debug.Log("========== SUBMIT QUIZ RESULT ==========");
        Debug.Log("Correct count = " + correctCount);
        Debug.Log("Total questions = " + totalQuestions);
        Debug.Log("Score text = " + scoreText);
        Debug.Log("========== SUBMIT QUIZ END ==========");

        string submitTime = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");

        string deadlineTime = PlayerPrefs.GetString("quiz_close_time", "Chưa có hạn");

        ShowResultPanel();

        // AddAttemptItem(scoreText, submitTime, deadlineTime);

        StartCoroutine(SaveAttemptToFirebase(correctCount, totalQuestions, scoreText));
    }

    private void AddAttemptItem(string scoreText, string submitTime, string deadlineTime)
    {
        if (attemptListContent == null || attemptItemPrefab == null)
        {
            Debug.LogError("Chưa gắn attemptListContent hoặc attemptItemPrefab.");
            return;
        }

        GameObject item = Instantiate(attemptItemPrefab, attemptListContent);

        AttemptItemUI attemptItemUI = item.GetComponent<AttemptItemUI>();

        if (attemptItemUI == null)
        {
            Debug.LogError("AttemptItem prefab chưa gắn AttemptItemUI.cs");
            return;
        }

        attemptItemUI.SetData(
            1,
            "submitted",
            scoreText,
            submitTime,
            deadlineTime
        );
    }
    private void UpdateExerciseInfoTexts()
    {
        string openTime = PlayerPrefs.GetString("quiz_open_time", "Chưa có dữ liệu");
        string closeTime = PlayerPrefs.GetString("quiz_close_time", "Chưa có dữ liệu");

        if (openTimeTextStudent != null)
            openTimeTextStudent.text = openTime;

        if (closeTimeTextStudent != null)
            closeTimeTextStudent.text = closeTime;

        if (openTimeTextTeacher != null)
            openTimeTextTeacher.text = openTime;

        if (closeTimeTextTeacher != null)
            closeTimeTextTeacher.text = closeTime;

        if (openTimeTextResult != null)
            openTimeTextResult.text = openTime;

        if (closeTimeTextResult != null)
            closeTimeTextResult.text = closeTime;
    }

    private void OnUpdateLessonClicked()
    {
        string selectedLessonId = PlayerPrefs.GetString("selected_lesson_id", "");
        string selectedClassId = PlayerPrefs.GetString("selected_class_id", "");

        if (string.IsNullOrEmpty(selectedLessonId))
        {
            Debug.LogError("Không tìm thấy selected_lesson_id.");
            return;
        }

        PlayerPrefs.SetInt("is_edit_lesson", 1);

        PlayerPrefs.SetString("edit_lesson_id", selectedLessonId);
        PlayerPrefs.SetString("edit_class_id", selectedClassId);
        PlayerPrefs.SetString("selected_class_id", selectedClassId);

        PlayerPrefs.SetString(
            "edit_lesson_title",
            PlayerPrefs.GetString("selected_lesson_title", "")
        );

        PlayerPrefs.SetString(
            "edit_lesson_description",
            PlayerPrefs.GetString("selected_lesson_info", "")
        );

        PlayerPrefs.SetString(
            "edit_lesson_img_url",
            PlayerPrefs.GetString("selected_lesson_img_url", "")
        );

        PlayerPrefs.SetString(
            "edit_lesson_pdf_url",
            PlayerPrefs.GetString("selected_lesson_pdf_url", "")
        );

        PlayerPrefs.SetString(
            "edit_exercise_pdf_url",
            PlayerPrefs.GetString("selected_exercise_pdf_url", "")
        );

        PlayerPrefs.SetString(
            "edit_quiz_id",
            PlayerPrefs.GetString("selected_quiz_id", "")
        );

        PlayerPrefs.Save();

        LessonTopBarPrefabManager.GoToScene(createLessonSceneName);
    }

    private IEnumerator SaveAttemptToFirebase(int correctCount, int totalQuestions, string scoreText)
    {
        string studentId = PlayerPrefs.GetString("user_id", "");
        string lessonId = PlayerPrefs.GetString("selected_lesson_id", "");
        string quizId = PlayerPrefs.GetString("selected_quiz_id", "");

        string url = apiBaseUrl + "/students/" + studentId + "/lessons/" + lessonId + "/attempts";

        AttemptRequest data = new AttemptRequest
        {
            quiz_id = quizId,
            score = scoreText,
            correct_count = correctCount,
            total_questions = totalQuestions,
            duration_seconds = 0,
            status = "submitted"
        };

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
            yield break;
        }

        Debug.Log("Save attempt success: " + request.downloadHandler.text);

        ExerciseResultListManager resultManager = FindObjectOfType<ExerciseResultListManager>();

        if (resultManager != null)
        {
            resultManager.LoadAttempts();
        }
        else
        {
            Debug.LogError("Không tìm thấy ExerciseResultListManager để reload attempts.");
        }
    }

    private void ShowResultPanel()
    {
        if (exerciseIntroPanelStudent != null)
            exerciseIntroPanelStudent.SetActive(false);

        if (exerciseIntroPanelTeacher != null)
            exerciseIntroPanelTeacher.SetActive(false);

        if (quizDoingPanel != null)
            quizDoingPanel.SetActive(false);

        if (exerciseResultScrollView != null)
            exerciseResultScrollView.SetActive(true);
    }

    private void ResetQuizSession()
    {
        Debug.Log("========== RESET QUIZ SESSION ==========");

        currentQuestionIndex = 0;

        selectedAnswers.Clear();

        UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);

        ResetAnswerButtonColors();

        if (questionProgressText != null)
            questionProgressText.text = "";

        if (questionTitleText != null)
            questionTitleText.text = "";

        if (questionImage != null)
        {
            questionImage.sprite = null;
            questionImage.color = new Color(1, 1, 1, 0);
        }

        questions = null;
    }
}