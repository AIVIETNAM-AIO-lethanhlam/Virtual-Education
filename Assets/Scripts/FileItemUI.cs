using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class FileItemUI : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text fileNameText;
    public Button deleteButton;

    private Action onDelete;

    private void Awake()
    {
        if (deleteButton != null)
        {
            deleteButton.onClick.RemoveAllListeners();
            deleteButton.onClick.AddListener(DeleteFileItem);
        }
    }

    public void Setup(string fileName, Action deleteAction)
    {
        if (fileNameText != null)
            fileNameText.text = fileName;

        onDelete = deleteAction;

        if (deleteButton != null)
        {
            deleteButton.onClick.RemoveAllListeners();
            deleteButton.onClick.AddListener(DeleteFileItem);
        }
    }

    private void DeleteFileItem()
    {
        Debug.Log("Clicked delete file item: " + gameObject.name);

        if (onDelete != null)
            onDelete.Invoke();

        Destroy(gameObject);
    }
}