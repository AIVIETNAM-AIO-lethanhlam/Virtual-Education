using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LessonTopBarPrefabManager : MonoBehaviour
{
    [Header("UI")]
    public Button backBtn;
    public TMP_Text titleText;

    [Header("Fallback")]
    public string fallbackSceneName = "LessonInClass";

    private const string SceneHistoryKey = "scene_history_stack";
    private static string runtimeFallbackSceneName = "LessonInClass";

    private void Awake()
    {
        if (!string.IsNullOrEmpty(fallbackSceneName))
        {
            runtimeFallbackSceneName = fallbackSceneName;
        }
    }

    private void Start()
    {
        if (backBtn != null)
        {
            backBtn.onClick.RemoveAllListeners();
            backBtn.onClick.AddListener(BackToPreviousScene);
        }

        UpdateTitleByCurrentScene();
    }

    private void UpdateTitleByCurrentScene()
    {
        if (titleText == null)
            return;

        string currentScene = SceneManager.GetActiveScene().name;

        if (currentScene == "DoExerciseScene")
        {
            string lessonTitle = PlayerPrefs.GetString(
                "current_lesson_title",
                PlayerPrefs.GetString("selected_lesson_title", "")
            );

            Debug.Log("TopBar DoExerciseScene lessonTitle = [" + lessonTitle + "]");

            if (!string.IsNullOrEmpty(lessonTitle))
            {
                titleText.text = lessonTitle;
            }

            return;
        }

        string normalTitle = PlayerPrefs.GetString("current_lesson_title", "");

        if (!string.IsNullOrEmpty(normalTitle))
        {
            titleText.text = normalTitle;
        }
    }

    public void SetTitle(string title)
    {
        if (titleText != null)
        {
            titleText.text = title;
        }
    }

    public static void GoToScene(string targetSceneName)
    {
        string currentScene = SceneManager.GetActiveScene().name;

        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogError("Target scene name is empty.");
            return;
        }

        if (currentScene == targetSceneName)
        {
            return;
        }

        PushScene(currentScene);

        Debug.Log("Go from " + currentScene + " to " + targetSceneName);
        SceneManager.LoadScene(targetSceneName);
    }

    public static void BackToPreviousScene()
    {
        string previousScene = PopScene();

        if (!string.IsNullOrEmpty(previousScene))
        {
            Debug.Log("Back to previous scene: " + previousScene);
            SceneManager.LoadScene(previousScene);
            return;
        }

        Debug.LogWarning("Scene history empty. Back to fallback scene: " + runtimeFallbackSceneName);
        SceneManager.LoadScene(runtimeFallbackSceneName);
    }

    private static void PushScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            return;
        }

        string history = PlayerPrefs.GetString(SceneHistoryKey, "");

        if (string.IsNullOrEmpty(history))
        {
            history = sceneName;
        }
        else
        {
            string[] scenes = history.Split('|');

            // Tránh push trùng scene liên tiếp
            if (scenes.Length > 0 && scenes[scenes.Length - 1] == sceneName)
            {
                Debug.Log("Scene already on top of history: " + sceneName);
                return;
            }

            history += "|" + sceneName;
        }

        PlayerPrefs.SetString(SceneHistoryKey, history);
        PlayerPrefs.Save();

        Debug.Log("PUSH history = " + history);
    }

    private static string PopScene()
    {
        string history = PlayerPrefs.GetString(SceneHistoryKey, "");

        Debug.Log("POP before history = " + history);

        if (string.IsNullOrEmpty(history))
        {
            return "";
        }

        string[] scenes = history.Split('|');

        if (scenes.Length == 0)
        {
            ClearHistory();
            return "";
        }

        string previousScene = scenes[scenes.Length - 1];

        string newHistory = "";

        for (int i = 0; i < scenes.Length - 1; i++)
        {
            if (string.IsNullOrEmpty(scenes[i]))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(newHistory))
            {
                newHistory += "|";
            }

            newHistory += scenes[i];
        }

        if (string.IsNullOrEmpty(newHistory))
        {
            PlayerPrefs.DeleteKey(SceneHistoryKey);
        }
        else
        {
            PlayerPrefs.SetString(SceneHistoryKey, newHistory);
        }

        PlayerPrefs.Save();

        Debug.Log("POP after history = " + newHistory);

        return previousScene;
    }

    public static void ClearHistory()
    {
        PlayerPrefs.DeleteKey(SceneHistoryKey);
        PlayerPrefs.Save();

        Debug.Log("CLEAR scene history");
    }
}