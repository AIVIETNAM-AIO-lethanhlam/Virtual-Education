using UnityEngine;

public class SafeArea : MonoBehaviour
{
    RectTransform rectTransform;
    Rect safeArea;
    Vector2 minAnchor;
    Vector2 maxAnchor;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        Refresh();
    }

    void Update()
    {
        Refresh();
    }

    void Refresh()
    {
        safeArea = Screen.safeArea;

        if (safeArea != Rect.zero)
        {
            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;

            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
        }
    }
}