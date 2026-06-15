using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TopSearchBarManager : MonoBehaviour
{
    [Header("Search UI")]
    public TMP_InputField searchInput;
    public Button searchButton;

    [Header("Content cần lọc")]
    public Transform teacherContentParent;
    public Transform studentContentParent;
    public Transform lessonContentParent;

    private void Start()
    {
        if (searchInput != null)
        {
            searchInput.onValueChanged.AddListener(FilterItems);
        }

        if (searchButton != null)
        {
            searchButton.onClick.AddListener(OnSearchButtonClicked);
        }
    }

    private void OnDestroy()
    {
        if (searchInput != null)
        {
            searchInput.onValueChanged.RemoveListener(FilterItems);
        }

        if (searchButton != null)
        {
            searchButton.onClick.RemoveListener(OnSearchButtonClicked);
        }
    }

    private void OnSearchButtonClicked()
    {
        if (searchInput == null) return;

        FilterItems(searchInput.text);
    }

    private void FilterItems(string keyword)
    {
        FilterContent(teacherContentParent, keyword);
        FilterContent(studentContentParent, keyword);
        FilterContent(lessonContentParent, keyword);
    }

    private void FilterContent(Transform contentParent, string keyword)
    {
        if (contentParent == null) return;

        string searchKeyword = "";

        if (!string.IsNullOrEmpty(keyword))
        {
            searchKeyword = keyword.Trim().ToLower();
        }

        foreach (Transform item in contentParent)
        {
            if (item == null) continue;

            bool isMatch = string.IsNullOrEmpty(searchKeyword);

            TMP_Text[] texts = item.GetComponentsInChildren<TMP_Text>(true);

            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] == null) continue;

                string textValue = texts[i].text.ToLower();

                if (textValue.Contains(searchKeyword))
                {
                    isMatch = true;
                    break;
                }
            }

            item.gameObject.SetActive(isMatch);
        }
    }

    public void ClearSearch()
    {
        if (searchInput != null)
        {
            searchInput.text = "";
        }

        FilterItems("");
    }
}