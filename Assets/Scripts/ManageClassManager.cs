using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using TMPro;

public class ManageClassManager : MonoBehaviour
{
    [Header("Teacher Panels")]
    public GameObject emptyStateTeacher;
    public GameObject scrollClassListTeacher;

    [Header("Student Panels")]
    public GameObject emptyStateStudent;
    public GameObject scrollClassListStudent;

    [Header("Student Title")]
    public TMP_Text studentTitleText;

    [Header("Teacher Class List UI")]
    public Transform teacherClassListContent;
    public GameObject teacherClassCardPrefab;

    [Header("Student Class List UI")]
    public Transform studentClassListContent;
    public GameObject studentClassCardPrefab;

    [Header("Buttons In Teacher Scroll")]
    public GameObject teacherUpdateButtonObject;
    public GameObject teacherFinishButtonObject;
    public TMP_Text teacherUpdateButtonText;
    public TMP_Text teacherFinishButtonText;

    [Header("Buttons In Student Scroll")]
    public GameObject studentUpdateButtonObject;
    public GameObject studentFinishButtonObject;
    public TMP_Text studentUpdateButtonText;
    public TMP_Text studentFinishButtonText;

    [Header("Scene Names")]
    public string homeUserSceneName = "new_home_scene_user";
    public string createClassSceneName = "CreateClassScene";

    [Header("API")]
    public string baseUrl = "http://localhost:4000/api";
    // public string baseUrl = " https://e571-14-241-225-251.ngrok-free.app/api";

    private bool hasClass = false;
    private bool isEditMode = false;
    private bool isStudentRegisterMode = false;
    private string currentRole = "";

    private void Start()
    {
        string userId = PlayerPrefs.GetString("user_id", "");
        currentRole = PlayerPrefs.GetString("current_role", "");

        Debug.Log("ManageClassScene user_id = " + userId);
        Debug.Log("ManageClassScene current_role = " + currentRole);

        HideAllPanels();

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(currentRole))
        {
            ShowEmptyPanel();
            return;
        }

        StartCoroutine(LoadUserClasses(userId, currentRole));
    }

    public void OnReturnButtonClicked()
    {
        SceneManager.LoadScene(homeUserSceneName);
    }

    private IEnumerator LoadUserClasses(string userId, string role)
    {
        string url = "";

        if (role == "teacher")
            url = baseUrl + "/teachers/" + userId + "/classes";
        else if (role == "student")
            url = baseUrl + "/students/" + userId + "/classes";
        else
        {
            ShowEmptyPanel();
            yield break;
        }

        Debug.Log("Load classes URL: " + url);

        UnityWebRequest request = UnityWebRequest.Get(url);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Load classes failed: " + request.error);
            Debug.LogError("Response: " + request.downloadHandler.text);
            ShowEmptyPanel();
            yield break;
        }

        string json = request.downloadHandler.text;
        Debug.Log("Classes response: " + json);

        ClassListResponse response = JsonUtility.FromJson<ClassListResponse>(json);

        if (response != null && response.classes != null && response.classes.Length > 0)
        {
            hasClass = true;
            ShowClassList();
            GenerateClassCards(response.classes);
        }
        else
        {
            hasClass = false;
            ShowEmptyPanel();
        }
    }

    private void GenerateClassCards(ClassData[] classes)
    {
        Transform content = GetCurrentContent();
        GameObject prefab = GetCurrentPrefab();

        if (content == null || prefab == null)
        {
            Debug.LogError("Chưa gắn Content hoặc Prefab cho role: " + currentRole);
            return;
        }

        ClearOldClassCards(content);

        int insertIndex = GetInsertIndexBeforeButtons(content);

        for (int i = 0; i < classes.Length; i++)
        {
            GameObject card = Instantiate(prefab, content);
            card.transform.SetSiblingIndex(insertIndex + i);

            ClassCardUI cardUI = card.GetComponent<ClassCardUI>();

            if (cardUI != null)
            {
                cardUI.SetManager(this);
                cardUI.SetData(classes[i]);
                cardUI.SetEditMode(false, currentRole);
            }
        }
    }

    public void OnEmptyStudentJoinClassClicked()
    {
        if (currentRole != "student")
            return;

        StartCoroutine(LoadAvailableClassesForStudent());
    }

    // public void OnUpdateClassButtonClicked()
    // {
    //     if (currentRole == "student" && isEditMode)
    //     {
    //         StartCoroutine(LoadAvailableClassesForStudent());
    //         return;
    //     }

    //     if (!hasClass)
    //     {
    //         if (currentRole == "teacher")
    //             SceneManager.LoadScene(createClassSceneName);
    //         else if (currentRole == "student")
    //             StartCoroutine(LoadAvailableClassesForStudent());

    //         return;
    //     }

    //     if (!isEditMode)
    //     {
    //         SetEditMode(true);
    //     }
    //     else
    //     {
    //         if (currentRole == "teacher")
    //             SceneManager.LoadScene(createClassSceneName);
    //     }
    // }

    public void OnUpdateClassButtonClicked()
    {
        if (currentRole == "student" && isEditMode)
        {
            StartCoroutine(LoadAvailableClassesForStudent());
            return;
        }

        if (!hasClass)
        {
            if (currentRole == "teacher")
            {
                ClearEditClassPrefs();
                SceneManager.LoadScene(createClassSceneName);
            }
            else if (currentRole == "student")
            {
                StartCoroutine(LoadAvailableClassesForStudent());
            }

            return;
        }

        if (!isEditMode)
        {
            SetEditMode(true);
        }
        else
        {
            if (currentRole == "teacher")
            {
                ClearEditClassPrefs();
                SceneManager.LoadScene(createClassSceneName);
            }
        }
    }

    public void OnFinishButtonClicked()
    {
        if (currentRole == "student" && isStudentRegisterMode)
        {
            isStudentRegisterMode = false;

            if (studentTitleText != null)
                studentTitleText.text = "Lớp bạn đang học";

            if (studentUpdateButtonObject != null)
                studentUpdateButtonObject.SetActive(true);

            string userId = PlayerPrefs.GetString("user_id", "");
            StartCoroutine(LoadUserClasses(userId, currentRole));

            return;
        }

        if (isEditMode)
        {
            SetEditMode(false);
        }
        else
        {
            SceneManager.LoadScene(homeUserSceneName);
        }
    }

    public void DeleteClass(string classId, GameObject cardObject)
    {
        StartCoroutine(DeleteClassCoroutine(classId, cardObject));
    }

    private IEnumerator DeleteClassCoroutine(string classId, GameObject cardObject)
    {
        string url = baseUrl + "/classes/" + classId;

        Debug.Log("Delete class URL: " + url);

        UnityWebRequest request = UnityWebRequest.Delete(url);
        request.downloadHandler = new DownloadHandlerBuffer();

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Delete class failed: " + request.error);
            Debug.LogError("Response: " + request.downloadHandler.text);
            yield break;
        }

        Debug.Log("Delete class response: " + request.downloadHandler.text);

        if (cardObject != null)
            Destroy(cardObject);

        yield return null;

        CheckClassListAfterChange();
    }

    public void CancelEnrollment(string classId, GameObject cardObject)
    {
        string studentId = PlayerPrefs.GetString("user_id", "");

        if (string.IsNullOrEmpty(studentId))
        {
            Debug.LogError("Không tìm thấy student_id trong PlayerPrefs.");
            return;
        }

        StartCoroutine(CancelEnrollmentCoroutine(studentId, classId, cardObject));
    }

    

    private IEnumerator CancelEnrollmentCoroutine(string studentId, string classId, GameObject cardObject)
    {
        string url = baseUrl + "/students/" + studentId + "/classes/" + classId;

        Debug.Log("Cancel enrollment URL: " + url);

        UnityWebRequest request = UnityWebRequest.Delete(url);
        request.downloadHandler = new DownloadHandlerBuffer();

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Cancel enrollment failed: " + request.error);
            Debug.LogError("Response: " + request.downloadHandler.text);
            yield break;
        }

        Debug.Log("Cancel enrollment response: " + request.downloadHandler.text);

        if (cardObject != null)
            Destroy(cardObject);

        yield return null;

        CheckClassListAfterChange();
    }

    private void CheckClassListAfterChange()
    {
        Transform content = GetCurrentContent();

        if (content == null)
            return;

        ClassCardUI[] cards = content.GetComponentsInChildren<ClassCardUI>(true);

        if (cards.Length == 0)
        {
            hasClass = false;
            ShowEmptyPanel();
        }
    }

    private void SetEditMode(bool value)
    {
        isEditMode = value;

        Transform content = GetCurrentContent();

        if (content != null)
        {
            ClassCardUI[] cards = content.GetComponentsInChildren<ClassCardUI>(true);

            for (int i = 0; i < cards.Length; i++)
            {
                cards[i].SetEditMode(isEditMode, currentRole);
            }
        }

        if (currentRole == "teacher")
        {
            if (teacherUpdateButtonText != null)
                teacherUpdateButtonText.text = isEditMode ? "Thêm lớp học" : "Cập nhật lớp học";

            if (teacherFinishButtonText != null)
                teacherFinishButtonText.text = isEditMode ? "Hoàn tất" : "Quay lại";
        }
        else if (currentRole == "student")
        {
            if (studentUpdateButtonText != null)
                studentUpdateButtonText.text = isEditMode ? "Đăng kí lớp học mới" : "Cập nhật";

            if (studentFinishButtonText != null)
                studentFinishButtonText.text = isEditMode ? "Hoàn tất" : "Quay lại";
        }
    }

    private IEnumerator LoadAvailableClassesForStudent()
    {
        string studentId = PlayerPrefs.GetString("user_id", "");

        if (string.IsNullOrEmpty(studentId))
        {
            Debug.LogError("Không tìm thấy user_id.");
            yield break;
        }

        isStudentRegisterMode = true;
        isEditMode = false;

        string url = baseUrl + "/students/" + studentId + "/available-classes";

        Debug.Log("Available classes URL: " + url);

        UnityWebRequest request = UnityWebRequest.Get(url);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Load available classes failed: " + request.error);
            Debug.LogError("Response: " + request.downloadHandler.text);
            yield break;
        }

        ClassListResponse response =
            JsonUtility.FromJson<ClassListResponse>(request.downloadHandler.text);

        HideAllPanels();

        if (scrollClassListStudent != null)
            scrollClassListStudent.SetActive(true);

        if (studentTitleText != null)
            studentTitleText.text = "Các lớp hiện có";

        if (studentUpdateButtonObject != null)
            studentUpdateButtonObject.SetActive(false);

        if (studentFinishButtonObject != null)
            studentFinishButtonObject.SetActive(true);

        if (studentFinishButtonText != null)
            studentFinishButtonText.text = "Quay lại";

        ClearOldClassCards(studentClassListContent);

        if (response == null || response.classes == null || response.classes.Length == 0)
        {
            Debug.Log("Không có lớp khả dụng.");
            yield break;
        }

        for (int i = 0; i < response.classes.Length; i++)
        {
            GameObject card = Instantiate(studentClassCardPrefab, studentClassListContent);

            ClassCardUI cardUI = card.GetComponent<ClassCardUI>();

            if (cardUI != null)
            {
                cardUI.SetManager(this);
                cardUI.SetData(response.classes[i]);
                cardUI.SetStudentRegisterMode(true);
            }
        }
        if (studentFinishButtonObject != null)
        {
            studentFinishButtonObject.transform.SetAsLastSibling();
        }
    }

    public void RegisterClass(string classId, ClassCardUI cardUI)
    {
        StartCoroutine(RegisterClassCoroutine(classId, cardUI));
    }

    private IEnumerator RegisterClassCoroutine(string classId, ClassCardUI cardUI)
    {
        string studentId = PlayerPrefs.GetString("user_id", "");

        if (string.IsNullOrEmpty(studentId))
            yield break;

        string url = baseUrl + "/students/" + studentId + "/classes/" + classId;

        Debug.Log("Register class URL: " + url);

        UnityWebRequest request = new UnityWebRequest(url, "POST");
        request.uploadHandler = new UploadHandlerRaw(new byte[0]);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Register class failed: " + request.error);
            Debug.LogError("Response: " + request.downloadHandler.text);
            yield break;
        }

        Debug.Log("Register class response: " + request.downloadHandler.text);

        if (cardUI != null)
            cardUI.SetRegisteredState();
    }

    private void ClearOldClassCards(Transform content)
    {
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Transform child = content.GetChild(i);

            if (child.GetComponent<ClassCardUI>() != null)
                Destroy(child.gameObject);
        }
    }

    private int GetInsertIndexBeforeButtons(Transform content)
    {
        GameObject updateButton = GetCurrentUpdateButtonObject();

        if (updateButton != null)
            return updateButton.transform.GetSiblingIndex();

        return content.childCount;
    }

    private void HideAllPanels()
    {
        if (emptyStateTeacher != null) emptyStateTeacher.SetActive(false);
        if (scrollClassListTeacher != null) scrollClassListTeacher.SetActive(false);

        if (emptyStateStudent != null) emptyStateStudent.SetActive(false);
        if (scrollClassListStudent != null) scrollClassListStudent.SetActive(false);
    }

    private void ShowEmptyPanel()
    {
        hasClass = false;
        isEditMode = false;
        isStudentRegisterMode = false;

        HideAllPanels();

        if (currentRole == "teacher")
        {
            if (emptyStateTeacher != null)
                emptyStateTeacher.SetActive(true);
        }
        else if (currentRole == "student")
        {
            if (emptyStateStudent != null)
                emptyStateStudent.SetActive(true);
        }
    }

    private void ShowClassList()
    {
        HideAllPanels();

        if (currentRole == "teacher")
        {
            if (scrollClassListTeacher != null)
                scrollClassListTeacher.SetActive(true);
        }
        else if (currentRole == "student")
        {
            if (scrollClassListStudent != null)
                scrollClassListStudent.SetActive(true);
        }

        SetEditMode(false);
    }

    private Transform GetCurrentContent()
    {
        if (currentRole == "teacher")
            return teacherClassListContent;

        if (currentRole == "student")
            return studentClassListContent;

        return null;
    }

    private GameObject GetCurrentPrefab()
    {
        if (currentRole == "teacher")
            return teacherClassCardPrefab;

        if (currentRole == "student")
            return studentClassCardPrefab;

        return null;
    }

    private GameObject GetCurrentUpdateButtonObject()
    {
        if (currentRole == "teacher")
            return teacherUpdateButtonObject;

        if (currentRole == "student")
            return studentUpdateButtonObject;

        return null;
    }

    private void ClearEditClassPrefs()
    {
        PlayerPrefs.DeleteKey("is_edit_class");
        PlayerPrefs.DeleteKey("edit_class_id");
        PlayerPrefs.DeleteKey("edit_class_name");
        PlayerPrefs.DeleteKey("edit_summary_info");
        PlayerPrefs.DeleteKey("edit_class_img_url");
        PlayerPrefs.Save();
    }
}

[System.Serializable]
public class ClassListResponse
{
    public bool success;
    public ClassData[] classes;
}

[System.Serializable]
public class ClassData
{
    public string class_id;
    public string class_name;
    public string summary_info;
    public string class_img;
    public string class_img_url;
    public string teacher_id;
    public string teacher_name;
    public string teacher_img;
    public string created_at;
    public string joined_at;
}