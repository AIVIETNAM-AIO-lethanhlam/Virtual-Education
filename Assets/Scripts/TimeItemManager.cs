using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class TimePickerManager : MonoBehaviour
{
    [Header("Input")]
    public TMP_InputField dueTimeInput;

    [Header("Panel")]
    public GameObject timePickerPanel;

    [Header("Content")]
    public Transform hourContent;
    public Transform minuteContent;
    public Transform secondContent;

    [Header("Prefab")]
    public GameObject timeItemPrefab;

    [Header("Colors")]
    public Color normalBackgroundColor = Color.white;
    public Color selectedBackgroundColor = new Color(0.1f, 0.6f, 1f, 1f);
    public Color normalTextColor = Color.black;
    public Color selectedTextColor = Color.white;

    private int selectedHour = 0;
    private int selectedMinute = 0;
    private int selectedSecond = 0;

    private Button selectedHourBtn;
    private Button selectedMinuteBtn;
    private Button selectedSecondBtn;

    private TMP_Text selectedHourText;
    private TMP_Text selectedMinuteText;
    private TMP_Text selectedSecondText;

    private void Start()
    {
        if (timePickerPanel != null)
            timePickerPanel.SetActive(false);

        if (dueTimeInput != null)
        {
            dueTimeInput.onSelect.RemoveAllListeners();
            dueTimeInput.onSelect.AddListener(delegate
            {
                OpenTimePicker();
            });
        }

        GenerateTimeItems();
        UpdateInputText();
    }

    private void Update()
    {
        if (timePickerPanel == null || !timePickerPanel.activeSelf)
            return;

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector2 mousePosition = Mouse.current.position.ReadValue();

            if (!IsPointerOverObject(timePickerPanel, mousePosition) &&
                dueTimeInput != null &&
                !IsPointerOverObject(dueTimeInput.gameObject, mousePosition))
            {
                CloseTimePicker();
            }
        }

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            Vector2 touchPosition = Touchscreen.current.primaryTouch.position.ReadValue();

            if (!IsPointerOverObject(timePickerPanel, touchPosition) &&
                dueTimeInput != null &&
                !IsPointerOverObject(dueTimeInput.gameObject, touchPosition))
            {
                CloseTimePicker();
            }
        }
    }


    public void ToggleTimePicker()
    {
        if (timePickerPanel == null)
            return;

        timePickerPanel.SetActive(!timePickerPanel.activeSelf);
    }

    public void OpenTimePicker()
    {
        if (timePickerPanel != null)
            timePickerPanel.SetActive(true);
    }

    public void CloseTimePicker()
    {
        if (timePickerPanel != null)
            timePickerPanel.SetActive(false);
    }

    private void GenerateTimeItems()
    {
        ClearContent(hourContent);
        ClearContent(minuteContent);
        ClearContent(secondContent);

        for (int i = 0; i < 24; i++)
            CreateTimeButton(hourContent, i, "hour");

        for (int i = 0; i < 60; i++)
            CreateTimeButton(minuteContent, i, "minute");

        for (int i = 0; i < 60; i++)
            CreateTimeButton(secondContent, i, "second");
    }

    private void CreateTimeButton(Transform parent, int value, string type)
    {
        if (parent == null || timeItemPrefab == null)
            return;

        GameObject item = Instantiate(timeItemPrefab, parent);

        TMP_Text text = item.GetComponentInChildren<TMP_Text>();
        if (text != null)
            text.text = value.ToString("00");

        Button button = item.GetComponent<Button>();
        if (button == null)
            return;

        int tempValue = value;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            SelectTime(tempValue, type, button, text);
        });

        SetTimeItemColor(button, text, false);

        if (value == 0)
        {
            if (type == "hour")
            {
                selectedHourBtn = button;
                selectedHourText = text;
                SetTimeItemColor(selectedHourBtn, selectedHourText, true);
            }
            else if (type == "minute")
            {
                selectedMinuteBtn = button;
                selectedMinuteText = text;
                SetTimeItemColor(selectedMinuteBtn, selectedMinuteText, true);
            }
            else if (type == "second")
            {
                selectedSecondBtn = button;
                selectedSecondText = text;
                SetTimeItemColor(selectedSecondBtn, selectedSecondText, true);
            }
        }
    }

    private void SelectTime(int value, string type, Button button, TMP_Text text)
    {
        if (type == "hour")
        {
            selectedHour = value;

            SetTimeItemColor(selectedHourBtn, selectedHourText, false);

            selectedHourBtn = button;
            selectedHourText = text;

            SetTimeItemColor(selectedHourBtn, selectedHourText, true);
        }
        else if (type == "minute")
        {
            selectedMinute = value;

            SetTimeItemColor(selectedMinuteBtn, selectedMinuteText, false);

            selectedMinuteBtn = button;
            selectedMinuteText = text;

            SetTimeItemColor(selectedMinuteBtn, selectedMinuteText, true);
        }
        else if (type == "second")
        {
            selectedSecond = value;

            SetTimeItemColor(selectedSecondBtn, selectedSecondText, false);

            selectedSecondBtn = button;
            selectedSecondText = text;

            SetTimeItemColor(selectedSecondBtn, selectedSecondText, true);
        }

        UpdateInputText();
    }

    private void UpdateInputText()
    {
        if (dueTimeInput == null)
            return;

        dueTimeInput.text =
            selectedHour.ToString("00") + ":" +
            selectedMinute.ToString("00") + ":" +
            selectedSecond.ToString("00");
    }

    private void ClearContent(Transform content)
    {
        if (content == null)
            return;

        foreach (Transform child in content)
        {
            Destroy(child.gameObject);
        }
    }

    private void SetTimeItemColor(Button btn, TMP_Text text, bool isSelected)
    {
        if (btn != null)
        {
            Image img = btn.GetComponent<Image>();

            if (img != null)
            {
                img.color = isSelected
                    ? selectedBackgroundColor
                    : normalBackgroundColor;
            }
        }

        if (text != null)
        {
            text.color = isSelected
                ? selectedTextColor
                : normalTextColor;
        }
    }

    private bool IsPointerOverObject(GameObject target, Vector2 screenPosition)
    {
        if (target == null)
            return false;

        RectTransform rect = target.GetComponent<RectTransform>();

        if (rect == null)
            return false;

        return RectTransformUtility.RectangleContainsScreenPoint(
            rect,
            screenPosition,
            null
        );
    }
}