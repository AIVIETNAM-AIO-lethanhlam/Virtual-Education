using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LessonItemUI : MonoBehaviour
{
    [Header("Lesson")]
    public TMP_Text lessonTitleText;
    public Image lessonImage;

    [Header("Teacher")]
    public TMP_Text teacherNameText;
    public Image teacherAvatarImage;

    [Header("Teacher Buttons")]
    public GameObject updateBtn;
    public GameObject deleteBtn;

    [Header("Scene")]
    public string lessonDetailSceneName = "ShowLessonScene";
    public string createLessonSceneName = "CreateLessonScene";

    private LessonInClassData currentData;
    private string lessonId = "";

    public void SetData(LessonInClassData data, bool isTeacher)
    {
        if (data == null)
        {
            Debug.LogError("LessonInClassData bị null.");
            return;
        }

        currentData = data;
        lessonId = data.lesson_id;

        string title = "";

        if (!string.IsNullOrEmpty(data.lesson_title))
            title = data.lesson_title;
        else if (!string.IsNullOrEmpty(data.lesson_name))
            title = data.lesson_name;
        else
            title = "Bài học chưa có tên";

        if (lessonTitleText != null)
        {
            lessonTitleText.gameObject.SetActive(true);
            lessonTitleText.text = LimitText(title, 18);
        }

        if (teacherNameText != null)
        {
            teacherNameText.text = string.IsNullOrEmpty(data.teacher_name)
                ? "Giáo viên"
                : data.teacher_name;
        }

        string lessonImgUrl = !string.IsNullOrEmpty(data.lesson_img_url)
            ? data.lesson_img_url
            : data.lesson_img;

        if (!string.IsNullOrEmpty(lessonImgUrl) && lessonImage != null)
            StartCoroutine(LoadImage(lessonImgUrl, lessonImage));

        if (!string.IsNullOrEmpty(data.teacher_img) && teacherAvatarImage != null)
            StartCoroutine(LoadImage(data.teacher_img, teacherAvatarImage));

        SetEditMode(false);

        if (!isTeacher)
        {
            if (updateBtn != null) updateBtn.SetActive(false);
            if (deleteBtn != null) deleteBtn.SetActive(false);
        }
    }

    private IEnumerator LoadImage(string imageUrl, Image targetImage)
    {
        UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageUrl);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning("Load lesson image failed: " + request.error);
            Debug.LogWarning("Image URL: " + imageUrl);
            yield break;
        }

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

    public void SetEditMode(bool isEditMode)
    {
        if (updateBtn != null)
            updateBtn.SetActive(isEditMode);

        if (deleteBtn != null)
            deleteBtn.SetActive(isEditMode);
    }

    public void OnLessonItemClicked()
    {
        if (string.IsNullOrEmpty(lessonId))
        {
            Debug.LogError("lessonId rỗng, chưa SetData cho LessonItem.");
            return;
        }

        PlayerPrefs.SetString("selected_lesson_id", lessonId);

        if (currentData != null)
        {
            PlayerPrefs.SetString("selected_class_id", currentData.class_id);
            PlayerPrefs.SetString("selected_lesson_title",
                !string.IsNullOrEmpty(currentData.lesson_title)
                    ? currentData.lesson_title
                    : currentData.lesson_name
            );
        }

        PlayerPrefs.Save();

        // SceneManager.LoadScene(lessonDetailSceneName);
        LessonTopBarPrefabManager.GoToScene(lessonDetailSceneName);
    }

    public void OnUpdateLessonButtonClicked()
    {
        if (currentData == null)
        {
            Debug.LogError("currentData null, chưa SetData.");
            return;
        }

        PlayerPrefs.SetInt("is_edit_lesson", 1);
        PlayerPrefs.SetString("edit_lesson_id", currentData.lesson_id);
        PlayerPrefs.SetString("edit_class_id", currentData.class_id);
        PlayerPrefs.SetString("edit_lesson_title",
            !string.IsNullOrEmpty(currentData.lesson_title)
                ? currentData.lesson_title
                : currentData.lesson_name
        );
        PlayerPrefs.SetString("edit_lesson_img_url",
            !string.IsNullOrEmpty(currentData.lesson_img_url)
                ? currentData.lesson_img_url
                : currentData.lesson_img
        );

        PlayerPrefs.Save();

        SceneManager.LoadScene(createLessonSceneName);
    }

    public void OnDeleteLessonButtonClicked()
    {
        if (string.IsNullOrEmpty(lessonId))
        {
            Debug.LogError("lessonId rỗng, không thể xóa.");
            return;
        }

        StartCoroutine(DeleteLessonCoroutine());
    }

    private string LimitText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return "";

        if (text.Length <= maxLength)
            return text;

        return text.Substring(0, maxLength) + "...";
    }

    private IEnumerator DeleteLessonCoroutine()
    {
        string baseUrl = "http://localhost:4000/api";
        // string baseUrl = "https://e571-14-241-225-251.ngrok-free.app/api";

        string url = baseUrl + "/lessons/" + UnityWebRequest.EscapeURL(lessonId);

        Debug.Log("Delete lesson URL: " + url);

        UnityWebRequest request = UnityWebRequest.Delete(url);
        request.downloadHandler = new DownloadHandlerBuffer();

        yield return request.SendWebRequest();

        Debug.Log("Response: " + request.downloadHandler.text);

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Delete lesson failed: " + request.error);
            Debug.LogError("URL: " + url);
            yield break;
        }

        Debug.Log("Delete lesson response: " + request.downloadHandler.text);

        Destroy(gameObject);
    }
}