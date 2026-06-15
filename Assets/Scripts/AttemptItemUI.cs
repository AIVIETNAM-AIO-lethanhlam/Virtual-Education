using TMPro;
using UnityEngine;

public class AttemptItemUI : MonoBehaviour
{
    [Header("Texts")]
    public TMP_Text attemptTitleText;
    public TMP_Text attemptNumberText;
    public TMP_Text statusText;
    public TMP_Text scoreText;
    public TMP_Text submitDateText;
    public TMP_Text deadlineText;

    private void Awake()
    {
        AutoFindTexts();
    }

    private void AutoFindTexts()
    {
        if (attemptTitleText == null) attemptTitleText = FindTMPText("HeaderText");
        if (attemptNumberText == null) attemptNumberText = FindTMPText("AttemptNumberText");
        if (statusText == null) statusText = FindTMPText("StatusText");
        if (scoreText == null) scoreText = FindTMPText("ScoreText");
        if (submitDateText == null) submitDateText = FindTMPText("SubmitDateText");
        if (deadlineText == null) deadlineText = FindTMPText("DeadlineText");
    }

    private TMP_Text FindTMPText(string objectName)
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);

        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i].gameObject.name == objectName)
                return texts[i];
        }

        Debug.LogWarning("Cannot find TMP_Text: " + objectName + " inside " + gameObject.name);
        return null;
    }

    public void SetData(
        int attemptNumber,
        string status,
        string score,
        string submittedAt,
        string deadline
    )
    {
        AutoFindTexts();

        string attemptLabel = "Lần " + attemptNumber;

        if (attemptTitleText != null)
            attemptTitleText.text = attemptLabel;

        if (attemptNumberText != null)
            attemptNumberText.text = attemptLabel;

        if (statusText != null)
            statusText.text = ConvertStatus(status);

        if (scoreText != null)
            scoreText.text = score;

        if (submitDateText != null)
            submitDateText.text = submittedAt;

        if (deadlineText != null)
            deadlineText.text = deadline;
    }

    private string ConvertStatus(string status)
    {
        if (status == "submitted")
            return "Đã hoàn thành";

        if (status == "doing")
            return "Đang làm";

        return status;
    }
}