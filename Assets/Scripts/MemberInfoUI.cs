using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;

public class MemberInfoUI : MonoBehaviour
{
    public TMP_Text memberNameText;
    public TMP_Text memberRoleText;
    public Image avatarImage;

    public void SetData(string fullName, string role, string avatarUrl)
    {
        if (memberNameText != null)
            memberNameText.text = string.IsNullOrEmpty(fullName) ? "Không có tên" : fullName;

        if (memberRoleText != null)
            memberRoleText.text = role == "teacher" ? "Giáo viên" : "Học sinh";

        if (!string.IsNullOrEmpty(avatarUrl) && avatarImage != null)
            StartCoroutine(LoadAvatar(avatarUrl));
    }

    private IEnumerator LoadAvatar(string url)
    {
        UnityWebRequest request = UnityWebRequestTexture.GetTexture(url);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning("Load avatar failed: " + request.error);
            yield break;
        }

        Texture2D texture = DownloadHandlerTexture.GetContent(request);
        avatarImage.sprite = Sprite.Create(
            texture,
            new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0.5f)
        );
    }
}