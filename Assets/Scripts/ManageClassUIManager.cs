using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class ManageClassUIManager : MonoBehaviour
{
    [Header("Edit Mode")]
    [SerializeField] private bool isEditMode = false;

    [Header("Class Card Button Boxes")]
    [SerializeField] private GameObject[] classButtonBoxes;

    [Header("Bottom Buttons")]
    [SerializeField] private TMP_Text updateClassButtonText;

    [Header("Scene Names")]
    [SerializeField] private string homeUserSceneName = "new_home_scene_user";
    [SerializeField] private string addClassSceneName = "CreateClassScene";

    private void Start()
    {
        SetEditMode(false);
    }

    public void OnUpdateClassButtonClicked()
    {
        if (!isEditMode)
        {
            SetEditMode(true);
        }
        else
        {
            SceneManager.LoadScene(addClassSceneName);
        }
    }

    public void OnFinishButtonClicked()
    {
        if (isEditMode)
        {
            SetEditMode(false);
        }
        else
        {
            SceneManager.LoadScene(homeUserSceneName);
        }
    }

    private void SetEditMode(bool value)
    {
        isEditMode = value;

        for (int i = 0; i < classButtonBoxes.Length; i++)
        {
            if (classButtonBoxes[i] != null)
            {
                classButtonBoxes[i].SetActive(isEditMode);
            }
        }

        if (updateClassButtonText != null)
        {
            updateClassButtonText.text = isEditMode ? "Thêm lớp học" : "Cập nhật thông tin";
        }
    }
}