using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class ModelListManager : MonoBehaviour
{
    [Header("Top Bar")]
    public TMP_Text titleText;
    [Header("UI References")]
    public Transform contentPanel;       // Kéo object "Content" vào đây
    public GameObject modelItemPrefab;   // Kéo Prefab từ thư mục Project vào đây
    public Button backBtn;

    [Header("API")]
    // public string baseUrl = "http://localhost:4000/api";
    public string baseUrl = "https://plus-pork-dodge.ngrok-free.dev/api";

    private void Start()
    {
        if (backBtn != null)
        {
            backBtn.onClick.AddListener(() =>
            {
                SceneManager.LoadScene("LessonInClass");
            });
        }

        StartCoroutine(FetchLessonModels());
        UpdateTopBarTitleFromPrefs();
    }

    private void UpdateTopBarTitleFromPrefs()
    {
        string lessonTitle = PlayerPrefs.GetString(
            "current_lesson_title",
            PlayerPrefs.GetString("selected_lesson_title", "")
        );

        if (titleText != null && !string.IsNullOrEmpty(lessonTitle))
        {
            titleText.text = lessonTitle;
            titleText.ForceMeshUpdate();
            Debug.Log("ModelList title from PlayerPrefs = " + lessonTitle);
        }
    }

    private IEnumerator FetchLessonModels()
    {
        string lessonId = PlayerPrefs.GetString("selected_lesson_id", "");
        string classId = PlayerPrefs.GetString("selected_class_id", "");

        if (string.IsNullOrEmpty(lessonId))
        {
            Debug.LogError("Không tìm thấy lesson ID!");
            yield break;
        }

        string url = baseUrl + "/classes/" + classId + "/lessons/" + lessonId;

        UnityWebRequest request = UnityWebRequest.Get(url);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Lỗi gọi API: " + request.error);
            yield break;
        }

        ModelListResponse response =
            JsonUtility.FromJson<ModelListResponse>(request.downloadHandler.text);

        // if (response != null && response.success && response.lesson != null)
        // {
        //     PopulateList(response.lesson.models);
        // }
        if (response != null && response.success && response.lesson != null)
        {
            if (!string.IsNullOrEmpty(response.lesson.lesson_title))
            {
                PlayerPrefs.SetString("current_lesson_title", response.lesson.lesson_title);
                PlayerPrefs.SetString("selected_lesson_title", response.lesson.lesson_title);
                PlayerPrefs.Save();

                if (titleText != null)
                {
                    titleText.text = response.lesson.lesson_title;
                    titleText.ForceMeshUpdate();
                }

                Debug.Log("ModelList title from API = " + response.lesson.lesson_title);
            }

            PopulateList(response.lesson.models);
        }
        else
        {
            Debug.LogWarning("Không có dữ liệu model trong lesson này.");
        }
    }

    private void PopulateList(List<ModelData> models)
    {
        foreach (Transform child in contentPanel)
        {
            Destroy(child.gameObject);
        }

        if (models == null || models.Count == 0)
        {
            Debug.LogWarning("Bài học này chưa có model 3D nào!");
            return;
        }

        for (int i = 0; i < models.Count; i++)
        {
            GameObject itemObj = Instantiate(modelItemPrefab, contentPanel);
            ModelListItem itemScript = itemObj.GetComponent<ModelListItem>();

            if (itemScript != null)
            {
                itemScript.Setup(
                    models[i].model_name,
                    models[i].model_url,
                    i
                );
            }
        }
    }
}

[System.Serializable]
public class ModelListResponse
{
    public bool success;
    public ModelListData lesson;
}

[System.Serializable]
public class ModelListData
{
    public string lesson_title;
    public List<ModelData> models;
}