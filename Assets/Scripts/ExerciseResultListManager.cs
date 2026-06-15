using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

[System.Serializable]
public class QuizAttempt
{
    public string attempt_id;
    public int attempt_number;
    public string student_id;
    public string lesson_id;
    public string quiz_id;
    public string started_at;
    public string submitted_at;
    public int duration_seconds;
    public int total_questions;
    public int correct_count;
    public string score;
    public string status;
    public string created_at;
}

[System.Serializable]
public class QuizAttemptResponse
{
    public bool success;
    public List<QuizAttempt> attempts;
}

public class ExerciseResultListManager : MonoBehaviour
{
    [Header("Panels")]
    public GameObject exerciseIntroPanelStudent;
    public GameObject exerciseIntroPanelTeacher;
    public GameObject quizDoingPanel;
    public GameObject exerciseResultScrollView;

    [Header("API")]
    public string baseUrl = "http://localhost:4000/api";
    // public string baseUrl = "https://e571-14-241-225-251.ngrok-free.app/api";

    [Header("Attempt UI")]
    public Transform attemptListContent;
    public GameObject attemptItemPrefab;

    private string studentId = "";
    private string lessonId = "";
    private Coroutine loadAttemptsRoutine;

    private void OnEnable()
    {
        string role = PlayerPrefs.GetString("exercise_role",
                    PlayerPrefs.GetString("current_role", ""))
                    .Trim()
                    .ToLower();

        Debug.Log("ExerciseResultListManager OnEnable role = " + role);

        if (role == "teacher")
        {
            Debug.Log("Teacher role => không load attempts, không hiện ExerciseResultScrollView.");

            if (exerciseResultScrollView != null)
                exerciseResultScrollView.SetActive(false);

            if (exerciseIntroPanelStudent != null)
                exerciseIntroPanelStudent.SetActive(false);

            if (quizDoingPanel != null)
                quizDoingPanel.SetActive(false);

            if (exerciseIntroPanelTeacher != null)
                exerciseIntroPanelTeacher.SetActive(true);

            return;
        }

        LoadAttempts();
    }

    public void LoadAttempts()
    {
        string role = PlayerPrefs.GetString("exercise_role",
                    PlayerPrefs.GetString("current_role", ""))
                    .Trim()
                    .ToLower();

        if (role == "teacher")
        {
            Debug.Log("Teacher role => chặn LoadAttempts.");

            if (exerciseResultScrollView != null)
                exerciseResultScrollView.SetActive(false);

            if (exerciseIntroPanelTeacher != null)
                exerciseIntroPanelTeacher.SetActive(true);

            return;
        }

        studentId = PlayerPrefs.GetString("user_id", "");
        lessonId = PlayerPrefs.GetString("selected_lesson_id", "");

        if (string.IsNullOrEmpty(studentId))
        {
            Debug.LogError("Không tìm thấy user_id trong PlayerPrefs.");
            return;
        }

        if (string.IsNullOrEmpty(lessonId))
        {
            Debug.LogError("Không tìm thấy selected_lesson_id trong PlayerPrefs.");
            return;
        }

        if (loadAttemptsRoutine != null)
            StopCoroutine(loadAttemptsRoutine);

        loadAttemptsRoutine = StartCoroutine(LoadAttemptsCoroutine());
    }

    private IEnumerator LoadAttemptsCoroutine()
    {
        string url = baseUrl + "/students/" + studentId + "/lessons/" + lessonId + "/attempts";

        Debug.Log("Load attempts URL: " + url);

        UnityWebRequest request = UnityWebRequest.Get(url);
        request.SetRequestHeader("ngrok-skip-browser-warning", "true");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Load attempts failed: " + request.error);
            Debug.LogError("Response: " + request.downloadHandler.text);
            yield break;
        }

        string json = request.downloadHandler.text;
        Debug.Log("Attempts JSON: " + json);

        QuizAttemptResponse response = JsonUtility.FromJson<QuizAttemptResponse>(json);

        if (response == null || response.success == false || response.attempts == null)
        {
            Debug.LogError("Load attempts response invalid");
            yield break;
        }

        ClearOldItems();

        yield return null;

        if (response.attempts.Count > 0)
            ShowResultPanel();
        else
            ShowIntroPanelStudent();

        for (int i = 0; i < response.attempts.Count; i++)
        {
            QuizAttempt attempt = response.attempts[i];

            GameObject item = Instantiate(attemptItemPrefab, attemptListContent);
            item.SetActive(true);

            AttemptItemUI itemUI = item.GetComponent<AttemptItemUI>();

            if (itemUI == null)
            {
                Debug.LogError("AttemptItem prefab chưa gắn AttemptItemUI.cs");
                continue;
            }

            int attemptNumber = i + 1;
            string deadlineTime = PlayerPrefs.GetString("quiz_close_time", "Chưa có hạn");

            itemUI.SetData(
                attemptNumber,
                attempt.status,
                attempt.score,
                FormatDateTime(attempt.submitted_at),
                deadlineTime
            );
        }

        yield return null;
        ForceRebuildResultLayout();
    }

    private void ClearOldItems()
    {
        if (attemptListContent == null)
            return;

        for (int i = attemptListContent.childCount - 1; i >= 0; i--)
        {
            Destroy(attemptListContent.GetChild(i).gameObject);
        }
    }

    private void ForceRebuildResultLayout()
    {
        Canvas.ForceUpdateCanvases();

        RectTransform attemptListRect = attemptListContent as RectTransform;
        if (attemptListRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(attemptListRect);

        if (attemptListRect != null && attemptListRect.parent != null)
        {
            RectTransform contentRect = attemptListRect.parent as RectTransform;
            if (contentRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        }

        if (exerciseResultScrollView != null)
        {
            RectTransform scrollRect = exerciseResultScrollView.GetComponent<RectTransform>();
            if (scrollRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect);
        }

        Canvas.ForceUpdateCanvases();
    }

    private string FormatDateTime(string dateTime)
    {
        if (string.IsNullOrEmpty(dateTime))
            return "";

        DateTime dt;

        if (DateTime.TryParse(dateTime, out dt))
            return dt.ToString("dd/MM/yyyy HH:mm:ss");

        if (dateTime.Length >= 10)
        {
            string yyyy = dateTime.Substring(0, 4);
            string mm = dateTime.Substring(5, 2);
            string dd = dateTime.Substring(8, 2);

            return dd + "/" + mm + "/" + yyyy;
        }

        return dateTime;
    }

    private void ShowResultPanel()
    {
        string role = PlayerPrefs.GetString("exercise_role",
                    PlayerPrefs.GetString("current_role", ""))
                    .Trim()
                    .ToLower();

        if (role == "teacher")
        {
            Debug.Log("Teacher role => không cho hiện ExerciseResultScrollView.");

            if (exerciseResultScrollView != null)
                exerciseResultScrollView.SetActive(false);

            if (exerciseIntroPanelTeacher != null)
                exerciseIntroPanelTeacher.SetActive(true);

            if (exerciseIntroPanelStudent != null)
                exerciseIntroPanelStudent.SetActive(false);

            if (quizDoingPanel != null)
                quizDoingPanel.SetActive(false);

            return;
        }

        if (quizDoingPanel != null && quizDoingPanel.activeSelf)
        {
            Debug.Log("Học sinh đang làm bài, không tự chuyển ResultPanel.");
            return;
        }

        if (exerciseIntroPanelStudent != null)
            exerciseIntroPanelStudent.SetActive(false);

        if (exerciseIntroPanelTeacher != null)
            exerciseIntroPanelTeacher.SetActive(false);

        if (quizDoingPanel != null)
            quizDoingPanel.SetActive(false);

        if (exerciseResultScrollView != null)
            exerciseResultScrollView.SetActive(true);
    }

    private void ShowIntroPanelStudent()
    {
        if (exerciseIntroPanelStudent != null)
            exerciseIntroPanelStudent.SetActive(true);

        if (exerciseIntroPanelTeacher != null)
            exerciseIntroPanelTeacher.SetActive(false);

        if (quizDoingPanel != null)
            quizDoingPanel.SetActive(false);

        if (exerciseResultScrollView != null)
            exerciseResultScrollView.SetActive(false);
    }
}