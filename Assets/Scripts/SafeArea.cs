using UnityEngine;

namespace ArtColorSupporter
{
    /// <summary>ノッチやパンチホールを避けるため、RectTransform を Screen.safeArea に合わせる。</summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        RectTransform rectTransform;
        Rect lastSafeArea;
        Vector2Int lastScreenSize;

        void Awake()
        {
            rectTransform = (RectTransform)transform;
            Apply();
        }

        void Update()
        {
            if (Screen.safeArea != lastSafeArea || lastScreenSize.x != Screen.width || lastScreenSize.y != Screen.height)
                Apply();
        }

        void Apply()
        {
            lastSafeArea = Screen.safeArea;
            lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;

            rectTransform.anchorMin = new Vector2(lastSafeArea.xMin / Screen.width, lastSafeArea.yMin / Screen.height);
            rectTransform.anchorMax = new Vector2(lastSafeArea.xMax / Screen.width, lastSafeArea.yMax / Screen.height);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
    }
}
