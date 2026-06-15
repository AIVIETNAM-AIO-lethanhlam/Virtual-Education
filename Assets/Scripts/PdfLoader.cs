using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using PDFtoImage;
using SkiaSharp;

public class PdfLoader : MonoBehaviour
{
    [Header("UI")]
    public RectTransform viewport;
    public Transform content;
    public GameObject pageImagePrefab;

    [Header("Layout")]
    public float horizontalPadding = 0f;
    public float topReservedSpace = 250f;

    private string pdfUrl = "";

    private void Start()
    {
        Debug.Log("========== PDF LOADER START ==========");

        pdfUrl = PlayerPrefs.GetString("pdf_url", "");

        Debug.Log("PlayerPrefs pdf_url = " + pdfUrl);

        SetupScrollViewArea();

        if (string.IsNullOrEmpty(pdfUrl))
        {
            Debug.LogError("[PDF] Không tìm thấy pdf_url trong PlayerPrefs.");
            return;
        }

        StartCoroutine(PrepareAndDownloadPdf());
    }

    private IEnumerator PrepareAndDownloadPdf()
    {
        Debug.Log("========== PREPARE PDF ==========");

        Debug.Log("[PDF] Original URL = " + pdfUrl);

        pdfUrl = CleanPdfUrl(pdfUrl);

        Debug.Log("[PDF] Cleaned URL = " + pdfUrl);

        if (!pdfUrl.StartsWith("http"))
        {
            Debug.LogError("[PDF] URL không hợp lệ, không bắt đầu bằng http/https.");
            yield break;
        }

        if (pdfUrl.Contains("storage.googleapis.com"))
        {
            Debug.Log("[PDF] Đây là Google Cloud Storage public URL.");
        }

        yield return StartCoroutine(DownloadAndLoadPdf());
    }

    private IEnumerator DownloadAndLoadPdf()
    {
        pdfUrl = ConvertToFirebaseMediaUrl(pdfUrl.Trim());

        Debug.Log("[PDF] Final download URL = " + pdfUrl);

        UnityWebRequest request = UnityWebRequest.Get(pdfUrl);
        request.SetRequestHeader("ngrok-skip-browser-warning", "true");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("[PDF] Download failed: " + request.error);
            Debug.LogError("[PDF] HTTP CODE = " + request.responseCode);
            Debug.LogError("[PDF] Response = " + request.downloadHandler.text);
            yield break;
        }

        byte[] pdfBytes = request.downloadHandler.data;

        if (pdfBytes == null || pdfBytes.Length == 0)
        {
            Debug.LogError("[PDF] PDF bytes rỗng.");
            yield break;
        }

        Debug.Log("[PDF] Downloaded. Size = " + pdfBytes.Length);

        yield return StartCoroutine(RenderPdf(pdfBytes));
    }
    private string ConvertToFirebaseMediaUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
            return "";

        url = url.Trim();

        if (url.EndsWith("?="))
            url = url.Substring(0, url.Length - 2);

        if (!url.Contains("storage.googleapis.com"))
            return url;

        Uri uri = new Uri(url);
        string path = uri.AbsolutePath.TrimStart('/');

        string bucketName = "virtual-education-d056a.firebasestorage.app";

        if (path.StartsWith(bucketName + "/"))
            path = path.Substring(bucketName.Length + 1);

        string encodedPath = UnityWebRequest.EscapeURL(path).Replace("+", "%20");

        return "https://firebasestorage.googleapis.com/v0/b/"
            + bucketName
            + "/o/"
            + encodedPath
            + "?alt=media";
    }
    private IEnumerator RenderPdf(byte[] pdfBytes)
    {
        Debug.Log("========== RENDER PDF ==========");

        ClearOldPages();

        yield return null;
        Canvas.ForceUpdateCanvases();

        if (viewport == null)
        {
            Debug.LogError("[PDF] Viewport chưa được gắn trong Inspector.");
            yield break;
        }

        if (content == null)
        {
            Debug.LogError("[PDF] Content chưa được gắn trong Inspector.");
            yield break;
        }

        if (pageImagePrefab == null)
        {
            Debug.LogError("[PDF] Page Image Prefab chưa được gắn trong Inspector.");
            yield break;
        }

        float pageWidth = viewport.rect.width - horizontalPadding * 2f;

        if (pageWidth <= 0)
        {
            pageWidth = Screen.width - horizontalPadding * 2f;
            Debug.LogWarning("[PDF] viewport width <= 0, dùng Screen.width = " + pageWidth);
        }

        int pageCount = 0;

        foreach (SKBitmap bitmap in Conversion.ToImages(pdfBytes))
        {
            pageCount++;

            Debug.Log("[PDF] Rendering page " + pageCount);
            Debug.Log("[PDF] Bitmap size = " + bitmap.Width + "x" + bitmap.Height);

            Texture2D texture = ConvertSKBitmapToTexture2D(bitmap);

            GameObject pageObj = Instantiate(pageImagePrefab, content);
            pageObj.SetActive(true);

            Image img = pageObj.GetComponent<Image>();

            if (img == null)
            {
                img = pageObj.AddComponent<Image>();
                Debug.LogWarning("[PDF] Page prefab không có Image, đã tự AddComponent<Image>().");
            }

            img.sprite = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f)
            );

            img.color = Color.white;
            img.preserveAspect = true;

            float ratio = (float)texture.height / texture.width;
            float pageHeight = pageWidth * ratio;

            RectTransform rect = pageObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(pageWidth, pageHeight);

            LayoutElement layout = pageObj.GetComponent<LayoutElement>();
            if (layout == null)
                layout = pageObj.AddComponent<LayoutElement>();

            layout.minWidth = pageWidth;
            layout.preferredWidth = pageWidth;
            layout.minHeight = pageHeight;
            layout.preferredHeight = pageHeight;
            layout.flexibleWidth = 0;
            layout.flexibleHeight = 0;

            bitmap.Dispose();

            yield return null;
        }

        Canvas.ForceUpdateCanvases();

        ScrollRect scrollRect = viewport.GetComponentInParent<ScrollRect>();

        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 1f;
            scrollRect.enabled = false;
            scrollRect.enabled = true;
        }

        Debug.Log("[PDF] Render completed. Total pages = " + pageCount);
    }

    private void SetupScrollViewArea()
    {
        Debug.Log("========== SETUP SCROLL VIEW ==========");

        if (viewport == null)
        {
            Debug.LogError("[PDF] viewport null.");
            return;
        }

        if (content == null)
        {
            Debug.LogError("[PDF] content null.");
            return;
        }

        RectTransform scrollRect = viewport.parent.GetComponent<RectTransform>();

        if (scrollRect == null)
        {
            Debug.LogError("[PDF] Không tìm thấy ScrollRect RectTransform.");
            return;
        }

        scrollRect.anchorMin = new Vector2(0f, 0f);
        scrollRect.anchorMax = new Vector2(1f, 1f);
        scrollRect.pivot = new Vector2(0.5f, 0.5f);
        scrollRect.offsetMin = new Vector2(0f, 0f);
        scrollRect.offsetMax = new Vector2(0f, -topReservedSpace);

        viewport.anchorMin = new Vector2(0f, 0f);
        viewport.anchorMax = new Vector2(1f, 1f);
        viewport.pivot = new Vector2(0.5f, 0.5f);
        viewport.offsetMin = Vector2.zero;
        viewport.offsetMax = Vector2.zero;

        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);

        Debug.Log("[PDF] Setup ScrollView done.");
    }

    private void ClearOldPages()
    {
        if (content == null) return;

        Debug.Log("[PDF] Clear old pages. Count = " + content.childCount);

        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Destroy(content.GetChild(i).gameObject);
        }
    }

    private Texture2D ConvertSKBitmapToTexture2D(SKBitmap bitmap)
    {
        Texture2D texture = new Texture2D(
            bitmap.Width,
            bitmap.Height,
            TextureFormat.RGBA32,
            false
        );

        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                SKColor color = bitmap.GetPixel(x, y);

                texture.SetPixel(
                    x,
                    bitmap.Height - y - 1,
                    new Color32(color.Red, color.Green, color.Blue, color.Alpha)
                );
            }
        }

        texture.Apply();
        return texture;
    }

    private string CleanPdfUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
            return "";

        url = url.Trim();

        // Không xóa "?=" ở cuối URL
        return url;
    }

    private bool IsPdfFile(byte[] data)
    {
        if (data == null || data.Length < 4)
            return false;

        return data[0] == 0x25 &&
               data[1] == 0x50 &&
               data[2] == 0x44 &&
               data[3] == 0x46;
    }

    private string GetFirstText(byte[] data)
    {
        if (data == null || data.Length == 0)
            return "";

        int len = Mathf.Min(data.Length, 300);

        try
        {
            return System.Text.Encoding.UTF8.GetString(data, 0, len);
        }
        catch
        {
            return "Cannot convert bytes to text.";
        }
    }
}