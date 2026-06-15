using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class VideoSceneOpener : MonoBehaviour
{
    [Header("Cài đặt Button")]
    public Button watchVideoBtn; // Kéo thả nút Video vào đây

    [Header("Tên Scene Video")]
    public string videoSceneName = "VideoViewScene"; // Đảm bảo tên này khớp chính xác

    private void Start()
    {
        // Kiểm tra xem đã gán nút chưa, nếu rồi thì gán sự kiện click
        if (watchVideoBtn != null)
        {
            watchVideoBtn.onClick.RemoveAllListeners(); // Xóa rác cũ nếu có
            watchVideoBtn.onClick.AddListener(GoToVideoScene);
        }
        else
        {
            Debug.LogWarning("Chưa kéo thả nút Video vào script VideoSceneOpener!");
        }
    }

    // Hàm thực hiện chuyển Scene
    public void GoToVideoScene()
    {
        Debug.Log("Đang chuyển sang cảnh: " + videoSceneName);
        
        // CÁCH 1: Dùng hàm mặc định của Unity
        SceneManager.LoadScene(videoSceneName);

        // CÁCH 2: Nếu dự án của bạn bắt buộc phải dùng TopBarManager để chuyển cảnh (như trong TabGroup), 
        // hãy // (comment) dòng CÁCH 1 lại và bỏ comment dòng CÁCH 2 dưới đây:
        // LessonTopBarPrefabManager.GoToScene(videoSceneName);
    }
}