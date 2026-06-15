using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class AddFileLessonManager : MonoBehaviour
{
    [Header("Lesson Info")]
    public int lessonId = 1;

    [Header("Upload UI")]
    public GameObject fileItemPrefab;
    public Transform fileListContentLesson;
    public TMP_Text messageText;

    private string selectedPdfPath = "";
    private string selectedPdfName = "";
    public string baseUrl = "http://localhost:4000";
    // private string baseUrl = "https://ce53-2405-4802-913f-8e40-f4ef-a523-d1a8-b433.ngrok-free.app";

    private GameObject currentFileItem;

    private void Start()
    {
        messageText.text = "";

        if (PlayerPrefs.HasKey("lesson_id"))
        {
            lessonId = PlayerPrefs.GetInt("lesson_id");
        }
    }

    public void ChoosePdfFile()
    {
        string[] allowedTypes = new string[] { "application/pdf" };

        NativeFilePicker.PickFile((path) =>
        {
            if (path == null)
            {
                messageText.text = "Bạn chưa chọn file PDF.";
                return;
            }

            selectedPdfPath = path;
            selectedPdfName = Path.GetFileName(path);

            ShowSelectedFile(selectedPdfName);

            messageText.text = "Đã chọn file PDF.";

        }, allowedTypes);
    }

    private void ShowSelectedFile(string fileName)
    {
        if (currentFileItem != null)
        {
            Destroy(currentFileItem);
        }

        currentFileItem = Instantiate(fileItemPrefab, fileListContentLesson);

        FileItemUI fileItemUI = currentFileItem.GetComponent<FileItemUI>();

        if (fileItemUI != null)
        {
            fileItemUI.Setup(fileName, () =>
            {
                selectedPdfPath = "";
                selectedPdfName = "";
                currentFileItem = null;
                messageText.text = "Đã xóa file đã chọn.";
            });
        }
    }

    public void UploadPdfFile()
    {
        if (string.IsNullOrEmpty(selectedPdfPath))
        {
            messageText.text = "Vui lòng chọn file PDF trước.";
            return;
        }

        StartCoroutine(UploadPdfCoroutine());
    }

    private IEnumerator UploadPdfCoroutine()
    {
        messageText.text = "Đang upload file...";

        byte[] fileData = File.ReadAllBytes(selectedPdfPath);

        WWWForm form = new WWWForm();
        form.AddBinaryData("pdf_file", fileData, selectedPdfName, "application/pdf");

        string url = baseUrl + "/api/lessons/" + lessonId + "/upload-pdf";

        UnityWebRequest request = UnityWebRequest.Post(url, form);

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            messageText.text = "Upload file bài giảng thành công.";
            Debug.Log("Upload success: " + request.downloadHandler.text);

            SceneManager.LoadScene("ShowLessonScene");
        }
        else
        {
            messageText.text = "Upload thất bại.";
            Debug.LogError("Upload error: " + request.error);
            Debug.LogError("Server response: " + request.downloadHandler.text);
        }
    }

    public void BackToLessonScene()
    {
        SceneManager.LoadScene("LessonInClass");
    }
}