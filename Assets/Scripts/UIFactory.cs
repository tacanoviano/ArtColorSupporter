using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ArtColorSupporter
{
    /// <summary>コードから uGUI 部品を組み立てるためのヘルパー。</summary>
    public static class UIFactory
    {
        static Font font;
        static Sprite roundedSprite;

        public static Font DefaultFont
        {
            get
            {
                if (font == null) font = LoadBuiltinFont("LegacyRuntime.ttf") ?? LoadBuiltinFont("Arial.ttf");
                return font;
            }
        }

        /// <summary>角丸の 9-slice スプライト（実行時に生成）。</summary>
        public static Sprite RoundedSprite
        {
            get
            {
                if (roundedSprite == null) roundedSprite = CreateRoundedSprite(64, 20);
                return roundedSprite;
            }
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax,
            float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        public static void StretchFull(RectTransform rt, float padding = 0)
        {
            Stretch(rt, Vector2.zero, Vector2.one, padding, padding, padding, padding);
        }

        public static void Place(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 position)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
        }

        public static Image CreateImage(Transform parent, string name, Color color, bool rounded)
        {
            var rt = CreateRect(name, parent);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            if (rounded)
            {
                image.sprite = RoundedSprite;
                image.type = Image.Type.Sliced;
            }
            return image;
        }

        public static LocalizedText CreateText(Transform parent, string name, string key, int fontSize, Color color,
            TextAnchor alignment)
        {
            var rt = CreateRect(name, parent);
            var text = rt.gameObject.AddComponent<Text>();
            text.font = DefaultFont;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(10, fontSize / 2);
            text.resizeTextMaxSize = fontSize;
            text.raycastTarget = false;

            var localized = rt.gameObject.AddComponent<LocalizedText>();
            localized.SetKey(key);
            return localized;
        }

        public static Button CreateButton(Transform parent, string name, string key, Color background, Color textColor,
            int fontSize, UnityAction onClick)
        {
            var image = CreateImage(parent, name, background, true);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(0.92f, 0.92f, 0.92f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(onClick);

            var label = CreateText(image.transform, "Label", key, fontSize, textColor, TextAnchor.MiddleCenter);
            StretchFull((RectTransform)label.transform, 12);
            return button;
        }

        /// <summary>
        /// テクスチャを area 内に縦横比を保って最大表示する。
        /// angle は時計回りの回転角（WebCamTexture.videoRotationAngle と同じ向き）。
        /// </summary>
        public static void FitTexture(RectTransform target, RectTransform area, int texWidth, int texHeight, int angle = 0)
        {
            if (texWidth <= 0 || texHeight <= 0) return;
            var areaSize = area.rect.size;
            bool swap = angle == 90 || angle == 270;
            float visibleWidth = swap ? texHeight : texWidth;
            float visibleHeight = swap ? texWidth : texHeight;
            float scale = Mathf.Min(areaSize.x / visibleWidth, areaSize.y / visibleHeight);

            target.anchorMin = target.anchorMax = target.pivot = new Vector2(0.5f, 0.5f);
            target.anchoredPosition = Vector2.zero;
            target.sizeDelta = new Vector2(texWidth * scale, texHeight * scale);
            target.localEulerAngles = new Vector3(0, 0, -angle);
        }

        static Font LoadBuiltinFont(string fontName)
        {
            try
            {
                return Resources.GetBuiltinResource<Font>(fontName);
            }
            catch
            {
                return null;
            }
        }

        static Sprite CreateRoundedSprite(int size, int radius)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "RoundedRect",
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float dx = Mathf.Max(0, Mathf.Max(radius - px, px - (size - radius)));
                    float dy = Mathf.Max(0, Mathf.Max(radius - py, py - (size - radius)));
                    float alpha = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0,
                SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }
    }
}
