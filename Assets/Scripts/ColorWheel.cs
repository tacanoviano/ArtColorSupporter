using UnityEngine;
using UnityEngine.UI;

namespace ArtColorSupporter
{
    /// <summary>
    /// 色相（角度）と彩度（中心からの距離）のカラーホイールと、明るさのバー。
    /// ターゲットの色を ✕ 印、カメラの色を ○ 印で表示する。
    /// 印の色は下にあるホイールの色を反転させたもの（どこにあっても見えるように）。
    /// </summary>
    public class ColorWheel : MonoBehaviour
    {
        /// <summary>ホイール + 明るさバー全体の横:縦。ホイール部分が正方形になる値。</summary>
        public const float AspectRatio = 1f / WheelWidth;

        const float WheelWidth = 0.85f;
        const int WheelTextureSize = 512;
        const float MarkerSize = 46f;
        const float BarMarkerSize = 30f;

        RectTransform wheel;
        RectTransform targetMarker, cameraMarker;
        RectTransform targetBarMarker, cameraBarMarker;
        Texture2D wheelTexture, barTexture;

        public static ColorWheel Create(Transform parent)
        {
            var root = UIFactory.CreateRect("ColorWheel", parent);
            var view = root.gameObject.AddComponent<ColorWheel>();
            view.Build(root);
            return view;
        }

        void Build(RectTransform root)
        {
            wheelTexture = CreateWheelTexture(WheelTextureSize);
            var wheelImage = UIFactory.CreateRect("Wheel", root).gameObject.AddComponent<RawImage>();
            wheelImage.texture = wheelTexture;
            wheelImage.raycastTarget = false;
            wheel = wheelImage.rectTransform;
            UIFactory.Stretch(wheel, Vector2.zero, new Vector2(WheelWidth, 1));

            barTexture = CreateValueBarTexture(256);
            var barImage = UIFactory.CreateRect("ValueBar", root).gameObject.AddComponent<RawImage>();
            barImage.texture = barTexture;
            barImage.raycastTarget = false;
            UIFactory.Stretch(barImage.rectTransform, new Vector2(0.91f, 0.04f), new Vector2(0.97f, 0.96f));

            targetMarker = CreateMarker(wheel, "TargetMarker", UIFactory.CrossSprite, MarkerSize);
            cameraMarker = CreateMarker(wheel, "CameraMarker", UIFactory.RingSprite, MarkerSize);
            targetBarMarker = CreateMarker(barImage.rectTransform, "TargetValueMarker", UIFactory.CrossSprite, BarMarkerSize);
            cameraBarMarker = CreateMarker(barImage.rectTransform, "CameraValueMarker", UIFactory.RingSprite, BarMarkerSize);
        }

        static RectTransform CreateMarker(Transform parent, string name, Sprite sprite, float size)
        {
            var image = UIFactory.CreateRect(name, parent).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            image.rectTransform.sizeDelta = new Vector2(size, size);
            image.gameObject.SetActive(false);
            return image.rectTransform;
        }

        public void SetTarget(Color? color)
        {
            Place(targetMarker, targetBarMarker, color);
        }

        public void SetCamera(Color? color)
        {
            Place(cameraMarker, cameraBarMarker, color);
        }

        static void Place(RectTransform marker, RectTransform barMarker, Color? color)
        {
            marker.gameObject.SetActive(color.HasValue);
            barMarker.gameObject.SetActive(color.HasValue);
            if (!color.HasValue) return;

            Color.RGBToHSV(color.Value, out float h, out float s, out float v);
            float angle = h * Mathf.PI * 2f;
            var position = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (s * 0.5f) + new Vector2(0.5f, 0.5f);
            marker.anchorMin = marker.anchorMax = position;
            marker.anchoredPosition = Vector2.zero;
            marker.GetComponent<Image>().color = Invert(Color.HSVToRGB(h, s, 1f));

            barMarker.anchorMin = barMarker.anchorMax = new Vector2(0.5f, v);
            barMarker.anchoredPosition = Vector2.zero;
            barMarker.GetComponent<Image>().color = Invert(new Color(v, v, v));
        }

        public static Color Invert(Color color)
        {
            return new Color(1f - color.r, 1f - color.g, 1f - color.b, 1f);
        }

        static Texture2D CreateWheelTexture(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                name = "ColorWheel",
            };
            var pixels = new Color32[size * size];
            float radius = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - radius) / radius;
                    float dy = (y + 0.5f - radius) / radius;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float hue = Mathf.Repeat(Mathf.Atan2(dy, dx) / (Mathf.PI * 2f), 1f);
                    Color color = Color.HSVToRGB(hue, Mathf.Min(distance, 1f), 1f);
                    color.a = Mathf.Clamp01((1f - distance) * radius + 0.5f);
                    pixels[y * size + x] = color;
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        static Texture2D CreateValueBarTexture(int height)
        {
            var texture = new Texture2D(1, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                name = "ValueBar",
            };
            var pixels = new Color32[height];
            for (int y = 0; y < height; y++)
            {
                byte v = (byte)(y * 255 / (height - 1));
                pixels[y] = new Color32(v, v, v, 255);
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        void OnDestroy()
        {
            if (wheelTexture != null) Destroy(wheelTexture);
            if (barTexture != null) Destroy(barTexture);
        }
    }
}
