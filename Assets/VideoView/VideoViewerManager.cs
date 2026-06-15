using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

public class VideoViewerManager : MonoBehaviour
{
    [Header("UI Elements")]
    public TMP_Text titleText;
    public TMP_Text statusText;
    public Button backButton;

    [Header("Video Setup")]
    public RawImage videoScreen;
    public VideoPlayer videoPlayer;
    public AudioSource audioSource;

    [Header("API")]
    //public string baseUrl = "http://localhost:4000/api";
    public string baseUrl = " https://plus-pork-dodge.ngrok-free.dev/api";

    private RenderTexture renderTexture;
    private bool videoPrepared = false;

    void Start()
    {
        if (videoScreen == null) { Debug.LogError("[VideoViewer] videoScreen chưa assign!"); return; }
        if (videoPlayer == null) { Debug.LogError("[VideoViewer] videoPlayer chưa assign!"); return; }
        if (audioSource == null) { Debug.LogError("[VideoViewer] audioSource chưa assign!"); return; } // Kiểm tra thêm

        if (backButton != null)
            backButton.onClick.AddListener(OnBackClicked);

        videoPlayer.playOnAwake     = false;
        videoPlayer.renderMode      = VideoRenderMode.RenderTexture;
        
        // --- CẤU HÌNH LẠI ÂM THANH TẠI ĐÂY ---
        videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource; // Chuyển sang dùng AudioSource
        videoPlayer.EnableAudioTrack(0, true);                          // Bật track âm thanh số 0 (track mặc định)
        videoPlayer.SetTargetAudioSource(0, audioSource);               // Link VideoPlayer với AudioSource
        // -------------------------------------

        videoPlayer.skipOnDrop      = true;
        videoPlayer.source          = VideoSource.Url;
        if (videoScreen == null) { Debug.LogError("[VideoViewer] videoScreen chưa assign!"); return; }
        if (videoPlayer == null) { Debug.LogError("[VideoViewer] videoPlayer chưa assign!"); return; }

        if (backButton != null)
            backButton.onClick.AddListener(OnBackClicked);

        videoPlayer.playOnAwake     = false;
        videoPlayer.renderMode      = VideoRenderMode.RenderTexture;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
        videoPlayer.skipOnDrop      = true;
        videoPlayer.source          = VideoSource.Url;

        videoPlayer.prepareCompleted += (vp) => { videoPrepared = true; };
        videoPlayer.errorReceived    += OnVideoError;

        SetStatus("Đang tải thông tin bài học...");
        StartCoroutine(FetchLessonVideo());
    }

    // ─── B1: Lấy video_url từ server ─────────────────────────────────────────
    private IEnumerator FetchLessonVideo()
    {
        string lessonId = PlayerPrefs.GetString("selected_lesson_id", "");
        if (string.IsNullOrEmpty(lessonId))
        {
            SetStatus("Không tìm thấy bài học."); yield break;
        }

        UnityWebRequest req = UnityWebRequest.Get(baseUrl + "/lessons/" + lessonId);
        req.timeout = 15;
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            SetStatus("Lỗi kết nối server: " + req.error); yield break;
        }

        LessonResponse res = JsonUtility.FromJson<LessonResponse>(req.downloadHandler.text);
        if (res == null || !res.success || res.lesson == null)
        {
            SetStatus("Dữ liệu bài học không hợp lệ."); yield break;
        }

        if (titleText != null) titleText.text = res.lesson.lesson_title.ToUpper();

        string videoUrl = res.lesson.video_url;
        if (string.IsNullOrEmpty(videoUrl))
        {
            SetStatus("Giáo viên chưa đính kèm video."); yield break;
        }

        // Chuyển URL Firebase sang direct stream URL
        string streamUrl = ConvertToDirectStreamUrl(videoUrl);
        Debug.Log("[VideoViewer] Stream URL: " + streamUrl);

        yield return StartCoroutine(PrepareAndPlay(streamUrl));
    }

    // ─── Convert Firebase Storage URL → direct stream URL ────────────────────
    // Firebase public URL:
    //   https://storage.googleapis.com/BUCKET/path/file.mp4
    // → Direct stream (không redirect, WindowsMediaFoundation chịu được):
    //   https://firebasestorage.googleapis.com/v0/b/BUCKET/o/path%2Ffile.mp4?alt=media
    private string ConvertToDirectStreamUrl(string firebaseUrl)
    {
        // Nếu đã là firebasestorage.googleapis.com → chỉ thêm ?alt=media
        if (firebaseUrl.Contains("firebasestorage.googleapis.com"))
        {
            if (!firebaseUrl.Contains("alt=media"))
                return firebaseUrl + (firebaseUrl.Contains("?") ? "&" : "?") + "alt=media";
            return firebaseUrl;
        }

        // Nếu là storage.googleapis.com/BUCKET/path → convert sang REST API URL
        // Ví dụ: https://storage.googleapis.com/my-app.appspot.com/lesson_videos/abc.mp4
        //      → https://firebasestorage.googleapis.com/v0/b/my-app.appspot.com/o/lesson_videos%2Fabc.mp4?alt=media
        try
        {
            // Lấy phần sau "storage.googleapis.com/"
            string marker = "storage.googleapis.com/";
            int idx = firebaseUrl.IndexOf(marker);
            if (idx < 0) return firebaseUrl; // không phải Firebase URL, giữ nguyên

            string afterMarker = firebaseUrl.Substring(idx + marker.Length);

            // Tách bucket và object path
            int slashIdx = afterMarker.IndexOf('/');
            if (slashIdx < 0) return firebaseUrl;

            string bucket     = afterMarker.Substring(0, slashIdx);
            string objectPath = afterMarker.Substring(slashIdx + 1);

            // Encode object path (thay '/' bằng '%2F')
            string encodedPath = objectPath.Replace("/", "%2F");

            string directUrl = $"https://firebasestorage.googleapis.com/v0/b/{bucket}/o/{encodedPath}?alt=media";
            Debug.Log($"[VideoViewer] Converted: {firebaseUrl}\n→ {directUrl}");
            return directUrl;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[VideoViewer] ConvertUrl failed: " + e.Message + " → dùng URL gốc");
            return firebaseUrl;
        }
    }

    // ─── B2: Prepare stream URL và play ──────────────────────────────────────
    private IEnumerator PrepareAndPlay(string streamUrl)
    {
        SetStatus("Đang kết nối video...");

        videoPrepared        = false;
        videoPlayer.url      = streamUrl;
        videoPlayer.Prepare();

        // Chờ prepared trên main thread (timeout 30s)
        float timeout = 30f;
        while (!videoPrepared && timeout > 0f)
        {
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (!videoPrepared)
        {
            SetStatus("Không thể tải video. Kiểm tra kết nối mạng.");
            Debug.LogError("[VideoViewer] Prepare timeout!");
            yield break;
        }

        // ── Assign RenderTexture trên main thread ──
        CleanupRenderTexture();

        int w = videoPlayer.width  > 0 ? (int)videoPlayer.width  : 1280;
        int h = videoPlayer.height > 0 ? (int)videoPlayer.height : 720;

        Debug.Log($"[VideoViewer] Prepared! {w}x{h}");

        renderTexture = new RenderTexture(w, h, 24);
        renderTexture.Create();

        videoPlayer.targetTexture = renderTexture;
        videoScreen.texture       = renderTexture;
        videoScreen.color         = Color.white;
        videoScreen.enabled       = false;
        videoScreen.enabled       = true; // force UI rebuild

        yield return null; // chờ 1 frame

        SetStatus("");
        videoPlayer.Play();
        Debug.Log("[VideoViewer] Đang phát video!");
    }

    // ─── Callbacks ────────────────────────────────────────────────────────────
    private void OnVideoError(VideoPlayer vp, string message)
    {
        SetStatus("Lỗi phát video: " + message);
        Debug.LogError("[VideoViewer] Error: " + message);
    }

    private void OnBackClicked()
    {
        videoPlayer.Stop();
        CleanupRenderTexture();
        SceneManager.LoadScene("LessonInClassScene");
    }

    void OnDestroy()
    {
        CleanupRenderTexture();
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────
    private void SetStatus(string msg)
    {
        if (statusText != null) statusText.text = msg;
    }

    private void CleanupRenderTexture()
    {
        if (renderTexture != null)
        {
            videoPlayer.targetTexture = null;
            renderTexture.Release();
            Destroy(renderTexture);
            renderTexture = null;
        }
    }

    // ─── Data classes ─────────────────────────────────────────────────────────
    [System.Serializable]
    public class LessonResponse
    {
        public bool success;
        public LessonData lesson;
    }

    [System.Serializable]
    public class LessonData
    {
        public string lesson_title;
        public string video_url;
    }
}