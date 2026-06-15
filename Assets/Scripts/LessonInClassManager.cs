using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LessonInClassManager : MonoBehaviour
{
    // ------ Scroll List Info----------
    [Header("Member Info Title")]
    public TMP_Text memberClassTitleText;
    [Header("Member Info UI")]
    public GameObject scrollListInfo;
    public Transform teacherMemberParent;
    public Transform studentMemberParent;
    public GameObject memberInfoPrefab;

    [Header("List Member Buttons")]
    public Button listMemberBtnTeacher;
    public Button listMemberBtnStudent;

    [Header("Member Info Return")]
    public Button memberInfoReturnBtn;
    // ----------------------------------------------
    [Header("Class Titles")]
    public TMP_Text teacherClassTitleText;
    public TMP_Text studentClassTitleText;
    
    [Header("Teacher UI")]
    public GameObject emptyLessonTeacher;
    public GameObject scrollLessonListTeacher;

    [Header("Student UI")]
    public GameObject emptyLessonStudent;
    public GameObject scrollLessonListStudent;

    [Header("Teacher Lesson List")]
    public Transform teacherLessonGrid;
    public GameObject teacherLessonItemPrefab;

    [Header("Student Lesson List")]
    public Transform studentLessonGrid;
    public GameObject studentLessonItemPrefab;

    [Header("Teacher Buttons")]
    public Button adjustBtn;

    [Header("Empty Teacher")]
    public Button uploadLessonBtn;

    [Header("Return Buttons")]
    public Button returnBtnTeacher;
    public Button returnBtnStudent;
    public Button emptyReturnBtnTeacher;
    public Button emptyReturnBtnStudent;

    [Header("API")]
    public string baseUrl = "http://localhost:4000/api";
    // public string baseUrl = " https://e571-14-241-225-251.ngrok-free.app/api";

    [Header("Scene Names")]
    public string manageClassSceneName = "ManageClassScene";
    public string createLessonSceneName = "CreateLessonScene";

    private string classId = "";
    private bool isTeacherMode = false;
    private bool isAdjusting = false;

    // ---------------- Scroll List Info---------------------------------
    private bool hasLessons = false;

    
    private void OpenMemberList()
    {
        if (emptyLessonTeacher != null) emptyLessonTeacher.SetActive(false);
        if (scrollLessonListTeacher != null) scrollLessonListTeacher.SetActive(false);

        if (emptyLessonStudent != null) emptyLessonStudent.SetActive(false);
        if (scrollLessonListStudent != null) scrollLessonListStudent.SetActive(false);

        if (scrollListInfo != null) scrollListInfo.SetActive(true);

        StartCoroutine(LoadClassMembers());
    }

    private void ReturnFromMemberList()
    {
        if (scrollListInfo != null) scrollListInfo.SetActive(false);

        if (hasLessons)
            ShowLessonList();
        else
            ShowEmptyState();
    }
    // -------------------------------------------

    private void Start()
    {
        classId = PlayerPrefs.GetString("selected_class_id", "");

        string className = PlayerPrefs.GetString("selected_class_name", "Lớp học");
        SetClassTitle(className);

        string currentRole = PlayerPrefs.GetString("current_role", "");
        isTeacherMode = currentRole == "teacher";

        Debug.Log("LessonInClass selected_class_id = " + classId);
        Debug.Log("LessonInClass current_role = " + currentRole);
        Debug.Log("isTeacherMode = " + isTeacherMode);

        SetupButtons();
        SetInitialUI();

        StartCoroutine(LoadLessons());
    }

    private void SetClassTitle(string className)
    {
        string displayName = LimitText(className, 25);

        if (teacherClassTitleText != null)
            teacherClassTitleText.text = displayName;

        if (studentClassTitleText != null)
            studentClassTitleText.text = displayName;

        if (memberClassTitleText != null)
            memberClassTitleText.text = displayName;
    }

    private string LimitText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return "";

        if (text.Length <= maxLength)
            return text;

        return text.Substring(0, maxLength) + "...";
    }

    private void SetupButtons()
    {
        if (adjustBtn != null)
            adjustBtn.onClick.AddListener(ToggleAdjustMode);

        if (returnBtnTeacher != null)
            returnBtnTeacher.onClick.AddListener(OnTeacherReturnClick);

        if (returnBtnStudent != null)
            returnBtnStudent.onClick.AddListener(GoBack);

        if (emptyReturnBtnTeacher != null)
            emptyReturnBtnTeacher.onClick.AddListener(GoBack);

        if (emptyReturnBtnStudent != null)
            emptyReturnBtnStudent.onClick.AddListener(GoBack);

        if (uploadLessonBtn != null)
            uploadLessonBtn.onClick.AddListener(OpenCreateLessonScene);

        // ------------ Scroll List Info------------------------
        if (listMemberBtnTeacher != null)
            listMemberBtnTeacher.onClick.AddListener(OpenMemberList);

        if (listMemberBtnStudent != null)
            listMemberBtnStudent.onClick.AddListener(OpenMemberList);

        if (memberInfoReturnBtn != null)
            memberInfoReturnBtn.onClick.AddListener(ReturnFromMemberList);
    }

    private void SetInitialUI()
    {
        if (emptyLessonTeacher != null) emptyLessonTeacher.SetActive(false);
        if (scrollLessonListTeacher != null) scrollLessonListTeacher.SetActive(false);

        if (emptyLessonStudent != null) emptyLessonStudent.SetActive(false);
        if (scrollLessonListStudent != null) scrollLessonListStudent.SetActive(false);

        if (adjustBtn != null)
            adjustBtn.gameObject.SetActive(isTeacherMode);

        isAdjusting = false;

        SetAdjustButtonText("Chỉnh sửa bài học");
        SetReturnTeacherText("Quay lại");
    }

    private IEnumerator LoadLessons()
    {
        if (string.IsNullOrEmpty(classId))
        {
            Debug.LogError("Không tìm thấy selected_class_id.");
            ShowEmptyState();
            yield break;
        }

        string url = baseUrl + "/lessons/class/" + classId;
        Debug.Log("Load lessons URL: " + url);

        UnityWebRequest request = UnityWebRequest.Get(url);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Load lessons failed: " + request.error);
            Debug.LogError("Response: " + request.downloadHandler.text);
            ShowEmptyState();
            yield break;
        }

        string json = request.downloadHandler.text;
        Debug.Log("Lessons response: " + json);

        LessonListResponse response = JsonUtility.FromJson<LessonListResponse>(json);

        if (response == null || response.lessons == null || response.lessons.Length == 0)
        {
            hasLessons = false;
            ShowEmptyState();
            yield break;
        }

        hasLessons = true;
        ShowLessonList();
        ClearCurrentLessonGrid();

        Transform currentGrid = GetCurrentLessonGrid();
        GameObject currentPrefab = GetCurrentLessonItemPrefab();

        if (currentGrid == null || currentPrefab == null)
        {
            Debug.LogError("Chưa gắn LessonGrid hoặc LessonItemPrefab.");
            yield break;
        }

        for (int i = 0; i < response.lessons.Length; i++)
        {
            GameObject item = Instantiate(currentPrefab, currentGrid);

            LessonItemUI ui = item.GetComponent<LessonItemUI>();

            if (ui != null)
                ui.SetData(response.lessons[i], isTeacherMode);
            else
                Debug.LogError("LessonItem prefab chưa gắn script LessonItemUI.");
        }

        isAdjusting = false;
        SetAdjustButtonText("Chỉnh sửa bài học");
        SetReturnTeacherText("Quay lại");
        UpdateLessonButtons();
    }

    private Transform GetCurrentLessonGrid()
    {
        return isTeacherMode ? teacherLessonGrid : studentLessonGrid;
    }

    private GameObject GetCurrentLessonItemPrefab()
    {
        return isTeacherMode ? teacherLessonItemPrefab : studentLessonItemPrefab;
    }

    private void ClearCurrentLessonGrid()
    {
        Transform currentGrid = GetCurrentLessonGrid();

        if (currentGrid == null) return;

        for (int i = currentGrid.childCount - 1; i >= 0; i--)
        {
            Destroy(currentGrid.GetChild(i).gameObject);
        }
    }

    private void ShowEmptyState()
    {
        if (isTeacherMode)
        {
            if (emptyLessonTeacher != null) emptyLessonTeacher.SetActive(true);
            if (scrollLessonListTeacher != null) scrollLessonListTeacher.SetActive(false);

            if (emptyLessonStudent != null) emptyLessonStudent.SetActive(false);
            if (scrollLessonListStudent != null) scrollLessonListStudent.SetActive(false);
        }
        else
        {
            if (emptyLessonStudent != null) emptyLessonStudent.SetActive(true);
            if (scrollLessonListStudent != null) scrollLessonListStudent.SetActive(false);

            if (emptyLessonTeacher != null) emptyLessonTeacher.SetActive(false);
            if (scrollLessonListTeacher != null) scrollLessonListTeacher.SetActive(false);
        }
    }

    private void ShowLessonList()
    {
        if (isTeacherMode)
        {
            if (emptyLessonTeacher != null) emptyLessonTeacher.SetActive(false);
            if (scrollLessonListTeacher != null) scrollLessonListTeacher.SetActive(true);

            if (emptyLessonStudent != null) emptyLessonStudent.SetActive(false);
            if (scrollLessonListStudent != null) scrollLessonListStudent.SetActive(false);
        }
        else
        {
            if (emptyLessonStudent != null) emptyLessonStudent.SetActive(false);
            if (scrollLessonListStudent != null) scrollLessonListStudent.SetActive(true);

            if (emptyLessonTeacher != null) emptyLessonTeacher.SetActive(false);
            if (scrollLessonListTeacher != null) scrollLessonListTeacher.SetActive(false);
        }
    }

    private void ToggleAdjustMode()
    {
        if (!isTeacherMode) return;

        if (!isAdjusting)
        {
            isAdjusting = true;

            SetAdjustButtonText("Thêm bài học");
            SetReturnTeacherText("Hoàn tất");

            UpdateLessonButtons();
        }
        else
        {
            OpenCreateLessonScene();
        }
    }

    private void UpdateLessonButtons()
    {
        if (!isTeacherMode) return;

        Transform currentGrid = GetCurrentLessonGrid();

        if (currentGrid == null) return;

        LessonItemUI[] lessonItems = currentGrid.GetComponentsInChildren<LessonItemUI>(true);

        for (int i = 0; i < lessonItems.Length; i++)
        {
            lessonItems[i].SetEditMode(isAdjusting);
        }
    }

    private void OnTeacherReturnClick()
    {
        if (isAdjusting)
        {
            isAdjusting = false;

            SetAdjustButtonText("Chỉnh sửa bài học");
            SetReturnTeacherText("Quay lại");

            UpdateLessonButtons();
        }
        else
        {
            GoBack();
        }
    }

    private void OpenCreateLessonScene()
    {
        if (string.IsNullOrEmpty(classId))
        {
            Debug.LogError("Không tìm thấy selected_class_id.");
            return;
        }

        PlayerPrefs.SetString("selected_class_id", classId);

        PlayerPrefs.DeleteKey("is_edit_lesson");
        PlayerPrefs.DeleteKey("edit_lesson_id");
        PlayerPrefs.DeleteKey("edit_class_id");
        PlayerPrefs.DeleteKey("edit_lesson_title");
        PlayerPrefs.DeleteKey("edit_lesson_description");
        PlayerPrefs.DeleteKey("edit_lesson_img_url");
        PlayerPrefs.DeleteKey("edit_lesson_pdf_url");
        PlayerPrefs.DeleteKey("edit_exercise_pdf_url");

        PlayerPrefs.Save();

        Debug.Log("GoToCreateLessonScene selected_class_id = " + classId);

        SceneManager.LoadScene(createLessonSceneName);
    }

    private void GoBack()
    {
        SceneManager.LoadScene(manageClassSceneName);
    }

    private void SetAdjustButtonText(string text)
    {
        if (adjustBtn == null) return;

        TMP_Text buttonText = adjustBtn.GetComponentInChildren<TMP_Text>();

        if (buttonText != null)
            buttonText.text = text;
    }

    private void SetReturnTeacherText(string text)
    {
        if (returnBtnTeacher == null) return;

        TMP_Text buttonText = returnBtnTeacher.GetComponentInChildren<TMP_Text>();

        if (buttonText != null)
            buttonText.text = text;
    }

    // ------------------------- Load Member Info----------------------------   

    private IEnumerator LoadClassMembers()
    {
        if (string.IsNullOrEmpty(classId))
        {
            Debug.LogError("Không tìm thấy selected_class_id để load members.");
            yield break;
        }

        ClearMemberList();

        string url = baseUrl + "/classes/" + classId + "/members";
        Debug.Log("Load members URL: " + url);

        UnityWebRequest request = UnityWebRequest.Get(url);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Load members failed: " + request.error);
            Debug.LogError("Response: " + request.downloadHandler.text);
            yield break;
        }

        string json = request.downloadHandler.text;
        Debug.Log("Members response: " + json);

        ClassMemberResponse response = JsonUtility.FromJson<ClassMemberResponse>(json);

        if (response == null || response.members == null)
        {
            Debug.LogError("Không parse được danh sách members.");
            yield break;
        }

        foreach (ClassMemberData member in response.members)
        {
            Transform parent = member.role == "teacher" ? teacherMemberParent : studentMemberParent;

            if (parent == null || memberInfoPrefab == null)
            {
                Debug.LogError("Chưa gắn teacherMemberParent / studentMemberParent / memberInfoPrefab.");
                continue;
            }

            GameObject item = Instantiate(memberInfoPrefab, parent);

            MemberInfoUI ui = item.GetComponent<MemberInfoUI>();
            if (ui != null)
            {
                ui.SetData(member.full_name, member.role, member.avatar_url);
            }
            else
            {
                Debug.LogError("MemberInfoPrefab chưa gắn script MemberInfoUI.");
            }
        }
    }

    private void ClearMemberList()
    {
        ClearChildren(teacherMemberParent);
        ClearChildren(studentMemberParent);
    }

    private void ClearChildren(Transform parent)
    {
        if (parent == null) return;

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Destroy(parent.GetChild(i).gameObject);
        }
    }
    // ------------------------------------------------------------
}

[System.Serializable]
public class LessonListResponse
{
    public bool success;
    public LessonInClassData[] lessons;
}

[System.Serializable]
public class LessonInClassData
{
    public string lesson_id;
    public string class_id;
    public string lesson_title;
    public string lesson_name;
    public string lesson_img;
    public string lesson_img_url;
    public string teacher_id;
    public string teacher_name;
    public string teacher_img;
    public string created_at;
}

[System.Serializable]
public class ClassMemberResponse
{
    public bool success;
    public ClassMemberData[] members;
}

[System.Serializable]
public class ClassMemberData
{
    public string user_id;
    public string full_name;
    public string role;
    public string avatar_url;
}