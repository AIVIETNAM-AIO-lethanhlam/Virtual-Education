using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using System;
using System.IO;
using System.Collections;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class CreateExerciseSceneManager : MonoBehaviour
{
    [Header("===== API =====")]
    public string apiBaseUrl = "http://localhost:4000/api";
    // public string apiBaseUrl = "https://e571-14-241-225-251.ngrok-free.app/api";

    [Header("===== DATE INPUT =====")]
    public TMP_InputField dueDateInput;
    public TMP_InputField dueTimeInput;
    public GameObject calendarPanel;

    [Header("===== CALENDAR =====")]
    public TMP_Text monthText;
    public Transform daysGrid;
    public GameObject dayButtonPrefab;

    [Header("===== UPLOAD PDF =====")]
    public Button uploadExerciseBtn;
    public Transform fileListContentExercise;
    public GameObject fileItemPrefab;

    [Header("===== BUTTONS =====")]
    public Button completeBtn;
    public Button backBtn;

    [Header("===== MESSAGE =====")]
    public TMP_Text messageText;

    private DateTime today;
    private DateTime currentMonth;
    private DateTime? selectedDate = null;

    private string uploadedPdfPath = "";
    private string classId = "";
    private string lessonId = "";

    private void Start()
    {
        today = DateTime.Today;
        currentMonth = new DateTime(today.Year, today.Month, 1);

        classId = PlayerPrefs.GetString("selected_class_id", "");
        lessonId = PlayerPrefs.GetString("selected_lesson_id", "");

        if (string.IsNullOrEmpty(classId))
        {
            classId = PlayerPrefs.GetInt("selected_class_id", -1).ToString();
        }

        if (string.IsNullOrEmpty(lessonId))
        {
            lessonId = PlayerPrefs.GetInt("selected_lesson_id", -1).ToString();
        }

        Debug.Log("CreateExercise classId = " + classId);
        Debug.Log("CreateExercise lessonId = " + lessonId);

        if (calendarPanel != null)
        {
            calendarPanel.SetActive(false);
        }

        if (uploadExerciseBtn != null)
        {
            uploadExerciseBtn.onClick.RemoveAllListeners();
            uploadExerciseBtn.onClick.AddListener(UploadPDF);
        }

        if (completeBtn != null)
        {
            completeBtn.onClick.RemoveAllListeners();
            completeBtn.onClick.AddListener(OnCompleteClicked);
        }

        if (backBtn != null)
        {
            backBtn.onClick.RemoveAllListeners();
            backBtn.onClick.AddListener(() =>
            {
                SceneManager.LoadScene("LessonInClass");
            });
        }

        GenerateCalendar();
        ShowMessage("");
    }

    // =====================================================
    // CALENDAR
    // =====================================================

    public void ToggleCalendar()
    {
        if (calendarPanel == null) return;

        bool isOpening = !calendarPanel.activeSelf;
        calendarPanel.SetActive(isOpening);

        if (isOpening)
        {
            calendarPanel.transform.SetAsLastSibling();
        }
    }

    public void PreviousMonth()
    {
        currentMonth = currentMonth.AddMonths(-1);
        GenerateCalendar();
    }

    public void NextMonth()
    {
        currentMonth = currentMonth.AddMonths(1);
        GenerateCalendar();
    }

    private void GenerateCalendar()
    {
        foreach (Transform child in daysGrid)
        {
            Destroy(child.gameObject);
        }

        monthText.text = currentMonth.ToString("MMMM yyyy");

        DateTime firstDayOfMonth = new DateTime(currentMonth.Year, currentMonth.Month, 1);
        int startDay = (int)firstDayOfMonth.DayOfWeek;
        DateTime startDate = firstDayOfMonth.AddDays(-startDay);

        for (int i = 0; i < 42; i++)
        {
            DateTime date = startDate.AddDays(i);

            GameObject dayObj = Instantiate(dayButtonPrefab, daysGrid);

            Button btn = dayObj.GetComponent<Button>();
            Image img = dayObj.GetComponent<Image>();
            TMP_Text txt = dayObj.GetComponentInChildren<TMP_Text>();

            txt.text = date.Day.ToString();

            img.color = new Color32(255, 255, 255, 0);
            txt.color = new Color32(40, 40, 40, 255);

            if (date.Month != currentMonth.Month)
            {
                txt.color = new Color32(170, 170, 170, 255);
            }

            bool isToday = !selectedDate.HasValue && date.Date == today.Date;

            if (isToday)
            {
                img.color = new Color32(66, 150, 255, 255);
                txt.color = Color.white;
            }

            if (selectedDate.HasValue && date.Date == selectedDate.Value.Date)
            {
                img.color = new Color32(66, 150, 255, 255);
                txt.color = Color.white;
            }

            DateTime clickedDate = date;

            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                SelectDate(clickedDate);
            });
        }
    }

    private void SelectDate(DateTime date)
    {
        selectedDate = date;

        if (dueDateInput != null)
        {
            dueDateInput.text = date.ToString("dd/MM/yyyy");
        }

        currentMonth = new DateTime(date.Year, date.Month, 1);

        GenerateCalendar();

        if (calendarPanel != null)
        {
            calendarPanel.SetActive(false);
        }
    }

    // =====================================================
    // PICK PDF
    // =====================================================

    public void UploadPDF()
    {
#if UNITY_EDITOR
        string path = EditorUtility.OpenFilePanel(
            "Chọn file PDF bài tập",
            "",
            "pdf"
        );

        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        uploadedPdfPath = path;

        string fileName = Path.GetFileName(path);
        AddPDFFileItem(fileName);
#else
        Debug.LogWarning("Android build cần NativeFilePicker để chọn file PDF.");
        ShowMessage("Android build cần NativeFilePicker để chọn file PDF.");
#endif
    }

    private void AddPDFFileItem(string fileName)
    {
        ClearFileList();

        GameObject item = Instantiate(fileItemPrefab, fileListContentExercise);
        item.name = "FileItem";

        FileItemUI fileItemUI = item.GetComponent<FileItemUI>();

        if (fileItemUI != null)
        {
            fileItemUI.Setup(fileName, () =>
            {
                RemovePDFFile(item);
            });
        }
        else
        {
            Debug.LogError("FileItem prefab thiếu FileItemUI script!");
        }

        if (uploadExerciseBtn != null)
        {
            uploadExerciseBtn.gameObject.SetActive(false);
        }
    }

    private void RemovePDFFile(GameObject item)
    {
        uploadedPdfPath = "";

        Destroy(item);

        if (uploadExerciseBtn != null)
        {
            uploadExerciseBtn.gameObject.SetActive(true);
        }
    }

    private void ClearFileList()
    {
        foreach (Transform child in fileListContentExercise)
        {
            Destroy(child.gameObject);
        }
    }

    // =====================================================
    // COMPLETE - UPLOAD TO FIREBASE BACKEND
    // =====================================================

    private void OnCompleteClicked()
    {
        if (string.IsNullOrEmpty(classId) || classId == "-1")
        {
            ShowMessage("Không tìm thấy selected_class_id.");
            Debug.LogError("Không tìm thấy selected_class_id.");
            return;
        }

        if (string.IsNullOrEmpty(lessonId) || lessonId == "-1")
        {
            ShowMessage("Không tìm thấy selected_lesson_id.");
            Debug.LogError("Không tìm thấy selected_lesson_id.");
            return;
        }

        if (string.IsNullOrEmpty(uploadedPdfPath))
        {
            ShowMessage("Vui lòng chọn file PDF bài tập.");
            return;
        }

        if (!File.Exists(uploadedPdfPath))
        {
            ShowMessage("File PDF không tồn tại.");
            return;
        }

        StartCoroutine(UploadQuizPdfCoroutine());
    }

    private IEnumerator UploadQuizPdfCoroutine()
    {
        ShowMessage("Đang upload bài tập...");

        if (completeBtn != null)
        {
            completeBtn.interactable = false;
        }

        string url = apiBaseUrl + "/classes/" + classId + "/lessons/" + lessonId + "/upload-quiz-pdf";

        byte[] fileBytes = File.ReadAllBytes(uploadedPdfPath);
        string fileName = Path.GetFileName(uploadedPdfPath);

        WWWForm form = new WWWForm();

        form.AddBinaryData(
            "quiz_pdf",
            fileBytes,
            fileName,
            "application/pdf"
        );

        if (dueDateInput != null)
        {
            form.AddField("deadline_date", dueDateInput.text);
        }

        if (dueTimeInput != null)
        {
            form.AddField("deadline_time", dueTimeInput.text);
        }

        using (UnityWebRequest request = UnityWebRequest.Post(url, form))
        {
            yield return request.SendWebRequest();

            if (completeBtn != null)
            {
                completeBtn.interactable = true;
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Upload quiz PDF failed: " + request.error);
                Debug.LogError("Response: " + request.downloadHandler.text);

                ShowMessage("Upload thất bại: " + request.error);
                yield break;
            }

            Debug.Log("Upload quiz PDF success:");
            Debug.Log(request.downloadHandler.text);

            UploadQuizResponse response =
                JsonUtility.FromJson<UploadQuizResponse>(request.downloadHandler.text);

            if (response != null && response.success && response.quiz != null)
            {
                PlayerPrefs.SetString("selected_quiz_id", response.quiz.quiz_id);
                PlayerPrefs.SetString("selected_class_id", response.quiz.class_id);
                PlayerPrefs.SetString("selected_lesson_id", response.quiz.lesson_id);
                PlayerPrefs.Save();

                SceneManager.LoadScene("DoExerciseScene_test");
            }
            else
            {
                ShowMessage(response != null ? response.message : "Upload thất bại.");
            }
        }
    }

    private void ShowMessage(string message)
    {
        if (messageText != null)
        {
            messageText.text = message;
        }
    }
}

// =====================================================
// JSON RESPONSE CLASSES
// =====================================================

[Serializable]
public class UploadQuizResponse
{
    public bool success;
    public string message;
    public QuizData quiz;
}

[Serializable]
public class QuizData
{
    public string quiz_id;
    public string class_id;
    public string lesson_id;
    public string teacher_id;
    public string quiz_pdf_url;
    public string quiz_pdf_name;
}