using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class ClassCardUI : MonoBehaviour
{
    [Header("Images")]
    public Image classImage;
    public Image teacherAvatarImage;

    [Header("Texts")]
    public TMP_Text classNameText;
    public TMP_Text summaryText;
    public TMP_Text teacherNameText;

    [Header("Student Action")]
    public Button studentActionButton;
    public TMP_Text studentActionButtonText;

    [Header("Buttons")]
    public GameObject teacherButtonBox;
    public GameObject studentButtonBox;

    [Header("Scene Navigation")]
    public string lessonSceneName = "LessonInClass";

    private ManageClassManager manager;
    private string classId = "";
    private ClassData currentData;

    private bool isRegisterMode = false;
    private bool isRegistered = false;

    public void SetManager(ManageClassManager manageClassManager)
    {
        manager = manageClassManager;
    }

    public void SetData(ClassData data)
    {
        currentData = data;

        if (data == null)
        {
            Debug.LogError("ClassData bị null.");
            return;
        }

        classId = data.class_id;

        if (classNameText != null)
            classNameText.text = data.class_name;

        if (summaryText != null)
        {
            summaryText.text = string.IsNullOrEmpty(data.summary_info)
                ? "Chưa có mô tả lớp học"
                : data.summary_info;
        }

        if (teacherNameText != null)
        {
            teacherNameText.text = string.IsNullOrEmpty(data.teacher_name)
                ? "Giáo viên"
                : data.teacher_name;
        }

        string classImageUrl = !string.IsNullOrEmpty(data.class_img_url)
            ? data.class_img_url
            : data.class_img;

        if (!string.IsNullOrEmpty(classImageUrl) && classImage != null)
            StartCoroutine(LoadImage(classImageUrl, classImage, true));

        if (!string.IsNullOrEmpty(data.teacher_img) && teacherAvatarImage != null)
            StartCoroutine(LoadImage(data.teacher_img, teacherAvatarImage, false));

        SetEditMode(false, "");
    }

    private IEnumerator LoadImage(string imageUrl, Image targetImage, bool isClassImage)
    {
        UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageUrl);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning("Load image failed: " + request.error);
            Debug.LogWarning("Image URL: " + imageUrl);
            yield break;
        }

        Texture2D texture = DownloadHandlerTexture.GetContent(request);

        if (texture == null)
        {
            Debug.LogWarning("Texture null: " + imageUrl);
            yield break;
        }

        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.Apply(false, false);

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f
        );

        targetImage.sprite = sprite;
        targetImage.color = Color.white;
        targetImage.type = Image.Type.Simple;

        if (isClassImage)
        {
            targetImage.preserveAspect = true;
        }
        else
        {
            targetImage.preserveAspect = true;
        }
    }

    public void SetEditMode(bool isEditMode, string role)
    {
        isRegisterMode = false;
        isRegistered = false;

        if (teacherButtonBox != null)
            teacherButtonBox.SetActive(false);

        if (studentButtonBox != null)
            studentButtonBox.SetActive(false);

        if (role == "teacher")
        {
            if (teacherButtonBox != null)
                teacherButtonBox.SetActive(isEditMode);
        }
        else if (role == "student")
        {
            if (studentButtonBox != null)
                studentButtonBox.SetActive(isEditMode);

            if (studentActionButtonText != null)
                studentActionButtonText.text = "Hủy đăng kí";

            if (studentActionButton != null)
            {
                studentActionButton.interactable = true;

                Image img = studentActionButton.GetComponent<Image>();
                if (img != null)
                    img.color = new Color32(230, 80, 80, 255);
            }
        }
    }

    public void OnDeleteClassButtonClicked()
    {
        if (manager == null)
        {
            Debug.LogError("ManageClassManager chưa được gắn vào ClassCardUI.");
            return;
        }

        if (string.IsNullOrEmpty(classId))
        {
            Debug.LogError("classId rỗng, chưa SetData cho ClassCard.");
            return;
        }

        manager.DeleteClass(classId, gameObject);
    }

    public void OnCancelEnrollmentButtonClicked()
    {
        if (manager == null)
        {
            Debug.LogError("ManageClassManager chưa được gắn vào ClassCardUI.");
            return;
        }

        if (string.IsNullOrEmpty(classId))
        {
            Debug.LogError("classId rỗng, chưa SetData cho ClassCard.");
            return;
        }

        manager.CancelEnrollment(classId, gameObject);
    }

    public void SetStudentRegisterMode(bool value)
    {
        isRegisterMode = value;
        isRegistered = false;

        if (studentButtonBox != null)
            studentButtonBox.SetActive(true);

        if (studentActionButtonText != null)
            studentActionButtonText.text = "Đăng kí";

        if (studentActionButton != null)
        {
            studentActionButton.interactable = true;

            Image img = studentActionButton.GetComponent<Image>();
            if (img != null)
                img.color = new Color32(0, 180, 216, 255);
        }
    }

    public void OnStudentActionButtonClicked()
    {
        if (manager == null)
        {
            Debug.LogError("ManageClassManager chưa được gắn.");
            return;
        }

        if (string.IsNullOrEmpty(classId))
        {
            Debug.LogError("classId rỗng, chưa SetData cho ClassCard.");
            return;
        }

        if (isRegisterMode)
        {
            if (!isRegistered)
            {
                manager.RegisterClass(classId, this);
            }
        }
        else
        {
            manager.CancelEnrollment(classId, gameObject);
        }
    }

    public void SetRegisteredState()
    {
        isRegistered = true;

        if (studentActionButtonText != null)
            studentActionButtonText.text = "Đã đăng kí";

        if (studentActionButton != null)
        {
            studentActionButton.interactable = false;

            Image img = studentActionButton.GetComponent<Image>();
            if (img != null)
                img.color = Color.gray;
        }
    }

    public void OnClassCardClicked()
    {
        if (currentData == null || string.IsNullOrEmpty(currentData.class_id))
        {
            Debug.LogError("currentData/class_id không hợp lệ, chưa SetData cho ClassCard.");
            return;
        }

        PlayerPrefs.SetString("selected_class_id", currentData.class_id);
        PlayerPrefs.SetString("selected_class_name", currentData.class_name);

        PlayerPrefs.Save();

        Debug.Log("Go LessonInClass class_id = " + currentData.class_id);
        Debug.Log("Go LessonInClass class_name = " + currentData.class_name);

        SceneManager.LoadScene(lessonSceneName);
    }

    public void OnUpdateClassButtonClicked()
    {
        if (currentData == null)
        {
            Debug.LogError("currentData null, chưa SetData.");
            return;
        }

        PlayerPrefs.SetString("edit_class_id", currentData.class_id);
        PlayerPrefs.SetString("edit_class_name", currentData.class_name);
        PlayerPrefs.SetString("edit_summary_info", currentData.summary_info);
        PlayerPrefs.SetString("edit_class_img_url", currentData.class_img_url);
        PlayerPrefs.SetInt("is_edit_class", 1);
        PlayerPrefs.Save();

        SceneManager.LoadScene("CreateClassScene");
    }
}