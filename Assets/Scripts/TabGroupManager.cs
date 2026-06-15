// using System.Collections;
// using UnityEngine;
// using UnityEngine.Networking;
// using UnityEngine.SceneManagement;
// using UnityEngine.UI;

// public class TabGroupManager : MonoBehaviour
// {
//     [Header("Buttons")]
//     public Button lessonBtn;
//     public Button modelBtn;
//     public Button exerciseBtn;

//     [Header("Scene Names")]
//     public string showLessonSceneName = "ShowLessonScene";
//     public string modelListSceneName = "ModelListScene";
//     public string doExerciseSceneName = "DoExerciseScene";

//     private void Start()
//     {
//         if (lessonBtn != null)
//         {
//             lessonBtn.onClick.RemoveAllListeners();
//             lessonBtn.onClick.AddListener(OpenLessonScene);
//         }

//         if (modelBtn != null)
//         {
//             modelBtn.onClick.RemoveAllListeners();
//             modelBtn.onClick.AddListener(OpenModelScene);
//         }

//         if (exerciseBtn != null)
//         {
//             exerciseBtn.onClick.RemoveAllListeners();
//             exerciseBtn.onClick.AddListener(OpenExerciseScene);
//         }
//     }

//     public void OpenLessonScene()
//     {
//         LessonTopBarPrefabManager.GoToScene(showLessonSceneName);
//     }

//     public void OpenModelScene()
//     {
//         LessonTopBarPrefabManager.GoToScene(modelListSceneName);
//     }

//     public void OpenExerciseScene()
//     {
//         StartCoroutine(GetQuizIdThenOpenExercise());
//     }

//     private IEnumerator GetQuizIdThenOpenExercise()
//     {
//         string classId = PlayerPrefs.GetString("selected_class_id", "");
//         string lessonId = PlayerPrefs.GetString("selected_lesson_id", "");

//         if (string.IsNullOrEmpty(classId))
//         {
//             Debug.LogError("Không tìm thấy selected_class_id trong PlayerPrefs.");
//             yield break;
//         }

//         if (string.IsNullOrEmpty(lessonId))
//         {
//             Debug.LogError("Không tìm thấy selected_lesson_id trong PlayerPrefs.");
//             yield break;
//         }

//         string quizId = "";

//         yield return StartCoroutine(GetQuizIdFromFirestore(classId, lessonId, (id) =>
//         {
//             quizId = id;
//         }));

//         if (string.IsNullOrEmpty(quizId))
//         {
//             Debug.LogError("Không lấy được quizId từ Firestore.");
//             yield break;
//         }

//         string storageBucket = "virtual-education-d056a.firebasestorage.app";

//         string url = "https://storage.googleapis.com/"
//             + storageBucket
//             + "/quiz_questions/"
//             + classId
//             + "/"
//             + lessonId
//             + "/"
//             + quizId;

//         Debug.Log("Quiz metadata Storage URL = " + url);

//         UnityWebRequest request = UnityWebRequest.Get(url);
//         yield return request.SendWebRequest();

//         if (request.result != UnityWebRequest.Result.Success)
//         {
//             Debug.LogError("Không tìm thấy file questions metadata trong Storage.");
//             Debug.LogError("Error: " + request.error);
//             Debug.LogError("Response: " + request.downloadHandler.text);
//             yield break;
//         }

//         Debug.Log("Quiz metadata response: " + request.downloadHandler.text);

//         QuizQuestionsResponse metaResponse =
//             JsonUtility.FromJson<QuizQuestionsResponse>(request.downloadHandler.text);

//         if (metaResponse == null || metaResponse.success == false)
//         {
//             Debug.LogError("Metadata questions không hợp lệ.");
//             yield break;
//         }

//         if (metaResponse.total_questions <= 0)
//         {
//             Debug.LogError("Quiz chưa có câu hỏi nào.");
//             yield break;
//         }

//         PlayerPrefs.SetString("selected_class_id", classId);
//         PlayerPrefs.SetString("selected_lesson_id", lessonId);
//         PlayerPrefs.SetString("selected_quiz_id", quizId);
//         PlayerPrefs.Save();

//         Debug.Log("Saved selected_quiz_id = " + quizId);
//         Debug.Log("Total questions = " + metaResponse.total_questions);

//         LessonTopBarPrefabManager.GoToScene(doExerciseSceneName);
//     }

//     private IEnumerator GetQuizIdFromFirestore(
//         string classId,
//         string lessonId,
//         System.Action<string> onDone
//     )
//     {
//         string savedQuizId = PlayerPrefs.GetString("selected_quiz_id", "");

//         if (!string.IsNullOrEmpty(savedQuizId))
//         {
//             Debug.Log("Lấy quizId từ PlayerPrefs = " + savedQuizId);

//             if (onDone != null)
//                 onDone(savedQuizId);

//             yield break;
//         }

//         Debug.LogError("Không tìm thấy selected_quiz_id trong PlayerPrefs.");

//         if (onDone != null)
//             onDone("");

//         yield break;
//     }
// }

// [System.Serializable]
// public class LessonDetailResponse
// {
//     public bool success;
//     public LessonDetailData lesson;
// }

// [System.Serializable]
// public class LessonDetailData
// {
//     public string lesson_id;
//     public string lesson_title;
//     public string lesson_name;
//     public string lesson_info;
//     public string lesson_pdf_url;
//     public string exercise_pdf_url;
//     public string quiz_id;
// }

// [System.Serializable]
// public class FirestoreQuizListResponse
// {
//     public FirestoreQuizDocument[] documents;
// }

// [System.Serializable]
// public class FirestoreQuizDocument
// {
//     public string name;
//     public FirestoreQuizFields fields;
// }

// [System.Serializable]
// public class FirestoreQuizFields
// {
//     public FirestoreStringValue quiz_id;
//     public FirestoreStringValue class_id;
//     public FirestoreStringValue lesson_id;
//     public FirestoreStringValue teacher_id;
//     public FirestoreStringValue quiz_pdf_url;
//     public FirestoreStringValue quiz_pdf_name;
// }

// [System.Serializable]
// public class FirestoreStringValue
// {
//     public string stringValue;
// }

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TabGroupManager : MonoBehaviour
{
    [Header("Buttons")]
    public Button lessonBtn;
    public Button modelBtn;
    public Button exerciseBtn;

    [Header("Scene Names")]
    public string showLessonSceneName = "ShowLessonScene";
    public string modelListSceneName = "ModelListScene";
    public string doExerciseSceneName = "DoExerciseScene";

    private void Start()
    {
        if (lessonBtn != null)
        {
            lessonBtn.onClick.RemoveAllListeners();
            lessonBtn.onClick.AddListener(OpenLessonScene);
        }

        if (modelBtn != null)
        {
            modelBtn.onClick.RemoveAllListeners();
            modelBtn.onClick.AddListener(OpenModelScene);
        }

        if (exerciseBtn != null)
        {
            exerciseBtn.onClick.RemoveAllListeners();
            exerciseBtn.onClick.AddListener(OpenExerciseScene);
        }
    }

    public void OpenLessonScene()
    {
        if (SceneManager.GetActiveScene().name == showLessonSceneName)
            return;

        LessonTopBarPrefabManager.GoToScene(showLessonSceneName);
    }

    public void OpenModelScene()
    {
        if (SceneManager.GetActiveScene().name == modelListSceneName)
            return;

        LessonTopBarPrefabManager.GoToScene(modelListSceneName);
    }

    public void OpenExerciseScene()
    {
        string classId = PlayerPrefs.GetString("selected_class_id", "");
        string lessonId = PlayerPrefs.GetString("selected_lesson_id", "");
        string quizId = PlayerPrefs.GetString("selected_quiz_id", "");

        if (string.IsNullOrEmpty(classId))
        {
            Debug.LogError("Không tìm thấy selected_class_id trong PlayerPrefs.");
            return;
        }

        if (string.IsNullOrEmpty(lessonId))
        {
            Debug.LogError("Không tìm thấy selected_lesson_id trong PlayerPrefs.");
            return;
        }

        if (string.IsNullOrEmpty(quizId))
        {
            Debug.LogError("Không tìm thấy selected_quiz_id. Bài học này có thể chưa có bài tập.");
            return;
        }

        PlayerPrefs.SetString("selected_class_id", classId);
        PlayerPrefs.SetString("selected_lesson_id", lessonId);
        PlayerPrefs.SetString("selected_quiz_id", quizId);
        PlayerPrefs.Save();

        Debug.Log("Open DoExerciseScene");
        Debug.Log("selected_class_id = " + classId);
        Debug.Log("selected_lesson_id = " + lessonId);
        Debug.Log("selected_quiz_id = " + quizId);

        if (SceneManager.GetActiveScene().name == doExerciseSceneName)
            return;

        LessonTopBarPrefabManager.GoToScene(doExerciseSceneName);
    }
}