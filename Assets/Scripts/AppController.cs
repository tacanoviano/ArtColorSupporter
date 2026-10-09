using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ArtColorSupporter
{
    /// <summary>
    /// 最初の画面。UI をコードで組み立て、画像表示・減色・色の比較・カメラ・言語切り替えを担当する。
    /// 左半分: 画像（ピンチで拡大・ドラッグで移動・タップでターゲット色）と色数ボタン。
    /// 右上: カラーホイール（ターゲット ✕ / カメラ ○）と色のアドバイス。右下: カメラ映像と白い円。
    /// </summary>
    public class AppController : MonoBehaviour
    {
        static readonly Color BackgroundColor = new Color32(0x22, 0x24, 0x28, 0xFF);
        static readonly Color PanelColor = new Color32(0x2F, 0x32, 0x38, 0xFF);
        static readonly Color PrimaryColor = new Color32(0x4C, 0x8B, 0xF5, 0xFF);
        static readonly Color SecondaryColor = new Color32(0x3E, 0xB4, 0x89, 0xFF);
        static readonly Color NeutralColor = new Color32(0x55, 0x5A, 0x64, 0xFF);
        static readonly Color StopColor = new Color32(0xD9, 0x5C, 0x5C, 0xFF);
        static readonly Color ShutterColor = new Color32(0xF2, 0xF2, 0xF2, 0xFF);
        static readonly Color TextColor = new Color32(0xF5, 0xF5, 0xF5, 0xFF);
        static readonly Color SubTextColor = new Color32(0xA0, 0xA6, 0xB0, 0xFF);
        static readonly Color DarkTextColor = new Color32(0x20, 0x20, 0x20, 0xFF);

        static readonly ColorMode[] Modes =
            { ColorMode.Full, ColorMode.Colors256, ColorMode.Colors16, ColorMode.Colors8, ColorMode.Mono };
        static readonly string[] ModeKeys = { "mode_full", "mode_256", "mode_16", "mode_8", "mode_mono" };

        /// <summary>カメラ映像の円の直径（映像エリアの短い辺に対する割合）。</summary>
        const float CircleRatio = 0.24f;
        /// <summary>円の画像のうち、平均を取る内側の半径の割合（白い縁を除く）。</summary>
        const float CircleInnerRatio = 0.88f;
        /// <summary>
        /// ターゲット色はタップした点の周り (2×半径+1) 四方の平均。
        /// 画素を粗くした画像では 1 画素がそのまま 1 色なので、タップした画素の色だけを使う。
        /// </summary>
        const int TargetSampleRadius = 2;

        // 左半分: 画像
        RectTransform imageArea;
        RawImage displayImage;
        ImageViewer imageViewer;
        GameObject placeholder;
        Image[] modeButtonImages;

        Texture2D originalTexture;
        Color32[] originalPixels;
        Texture2D reducedTexture;
        Color32[] displayedPixels;
        int imageWidth, imageHeight;
        int displayedWidth, displayedHeight;
        ColorMode colorMode = ColorMode.Full;
        int reduceVersion;

        Vector2? targetUv;
        Color? targetColor;
        Color? cameraColor;

        // 右上: ホイールと情報
        RectTransform wheelSection;
        RectTransform wheelRoot;
        RectTransform infoColumn;
        ColorWheel colorWheel;
        Image targetSwatch, cameraSwatch;
        LocalizedText adviceText;
        LocalizedText diffText;
        LocalizedText messageText;

        // 右下: カメラ
        RectTransform previewArea;
        RawImage previewImage;
        RectTransform circle;
        LocalizedText cameraPlaceholder;
        Button shutterButton;
        Image cameraButtonImage;
        LocalizedText cameraButtonLabel;
        CameraController cameraController;
        Coroutine cameraStartRoutine;
        bool cameraActive;
        int sampleFrame;

        bool picking;

        /// <summary>どのシーンから起動しても画面が作られるようにする。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureExists()
        {
            if (FindAnyObjectByType<AppController>() == null)
                new GameObject("App").AddComponent<AppController>();
        }

        void Awake()
        {
            ApplyLandscapeOrientation();
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            cameraController = gameObject.AddComponent<CameraController>();
            BuildUI();
            ShowMessage("msg_welcome");
            UpdateModeButtons();
            UpdateComparison();
        }

        void Update()
        {
            // ホイールの右側の残りを情報欄にする
            infoColumn.offsetMin = new Vector2(wheelRoot.rect.width + 24, infoColumn.offsetMin.y);

            if (!cameraActive) return;

            float circleSize = Mathf.Min(previewArea.rect.width, previewArea.rect.height) * CircleRatio;
            circle.sizeDelta = new Vector2(circleSize, circleSize);

            if (cameraController.IsReady)
            {
                var webcam = cameraController.Texture;
                previewImage.enabled = true;
                circle.gameObject.SetActive(true);
                previewImage.uvRect = cameraController.VerticallyMirrored ? new Rect(0, 1, 1, -1) : new Rect(0, 0, 1, 1);
                UIFactory.FitTexture(previewImage.rectTransform, previewArea, webcam.width, webcam.height,
                    cameraController.RotationAngle);

                // 数フレームごとに円の中の平均色を求める
                if (++sampleFrame % 3 == 0)
                {
                    float scale = previewImage.rectTransform.sizeDelta.x / webcam.width;
                    float radius = circleSize / 2f * CircleInnerRatio / Mathf.Max(scale, 0.0001f);
                    if (cameraController.TryGetCenterAverage(radius, out var average))
                    {
                        // ちらつかないように少しずつ追従させる
                        cameraColor = cameraColor.HasValue ? Color.Lerp(cameraColor.Value, average, 0.35f) : average;
                        UpdateComparison();
                    }
                }
            }
            shutterButton.interactable = cameraController.IsReady;

#if ENABLE_LEGACY_INPUT_MANAGER
            // Android の戻るボタンでカメラを止める
            if (Input.GetKeyDown(KeyCode.Escape)) StopCamera();
#endif
        }

        static void ApplyLandscapeOrientation()
        {
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
        }

        // ───────────────────────── カメラ ─────────────────────────

        void OnCameraButton()
        {
            if (cameraActive)
            {
                StopCamera();
                ShowMessage("msg_camera_stopped");
                return;
            }

            cameraActive = true;
            sampleFrame = 0;
            previewImage.enabled = false;
            cameraPlaceholder.SetKey("msg_camera_starting");
            cameraPlaceholder.gameObject.SetActive(true);
            shutterButton.gameObject.SetActive(true);
            shutterButton.interactable = false;
            cameraButtonLabel.SetKey("btn_camera_stop");
            cameraButtonImage.color = StopColor;
            cameraStartRoutine = StartCoroutine(cameraController.StartCamera(OnCameraStarted));
        }

        void OnCameraStarted(CameraController.StartResult result)
        {
            cameraStartRoutine = null;
            switch (result)
            {
                case CameraController.StartResult.Started:
                    previewImage.texture = cameraController.Texture;
                    cameraPlaceholder.gameObject.SetActive(false);
                    ShowMessage("msg_camera_hint");
                    break;
                case CameraController.StartResult.PermissionDenied:
                    StopCamera();
                    ShowMessage("msg_camera_denied");
                    break;
                case CameraController.StartResult.NoCamera:
                    StopCamera();
                    ShowMessage("msg_camera_no_device");
                    break;
                default:
                    StopCamera();
                    ShowMessage("msg_camera_failed");
                    break;
            }
        }

        void StopCamera()
        {
            if (cameraStartRoutine != null) StopCoroutine(cameraStartRoutine);
            cameraStartRoutine = null;
            cameraActive = false;
            cameraController.StopCamera();
            previewImage.texture = null;
            previewImage.enabled = false;
            circle.gameObject.SetActive(false);
            shutterButton.gameObject.SetActive(false);
            cameraPlaceholder.SetKey("placeholder_camera");
            cameraPlaceholder.gameObject.SetActive(true);
            cameraButtonLabel.SetKey("btn_camera");
            cameraButtonImage.color = PrimaryColor;
            cameraColor = null;
            UpdateComparison();
        }

        void OnShutterButton()
        {
            var captured = cameraController.Capture();
            if (captured == null)
            {
                ShowMessage("msg_capture_failed");
                return;
            }

            SetDisplayedTexture(captured);
            SaveCapture(captured);
        }

        // ───────────────────────── 画像の選択 ─────────────────────────

        void OnImageButton()
        {
            if (picking) return;
            picking = true;
            ShowMessage("msg_picking");
            GalleryPicker.PickImage(Localization.Get("picker_title"), OnImagePicked);
        }

        void OnImagePicked(GalleryPicker.Result result, Texture2D texture)
        {
            picking = false;
            switch (result)
            {
                case GalleryPicker.Result.Picked:
                    SetDisplayedTexture(texture);
                    ShowMessage("msg_image_loaded");
                    break;
                case GalleryPicker.Result.Canceled:
                    ShowMessage("msg_image_canceled");
                    break;
                case GalleryPicker.Result.NotAvailable:
                    ShowMessage("msg_gallery_unavailable");
                    break;
                default:
                    ShowMessage("msg_image_failed");
                    break;
            }
        }

        void OnLanguageButton()
        {
            Localization.Toggle();
        }

        // ───────────────────────── 画像の表示・減色・ターゲット ─────────────────────────

        void SetDisplayedTexture(Texture2D texture)
        {
            if (originalTexture != null && originalTexture != texture) Destroy(originalTexture);
            originalTexture = texture;
            originalPixels = texture.GetPixels32();
            imageWidth = texture.width;
            imageHeight = texture.height;

            displayImage.gameObject.SetActive(true);
            placeholder.SetActive(false);
            displayedPixels = originalPixels;
            displayedWidth = imageWidth;
            displayedHeight = imageHeight;
            imageViewer.SetTexture(texture, true);

            targetUv = null;
            targetColor = null;
            UpdateComparison();
            ApplyColorMode();
        }

        void OnModeButton(int index)
        {
            colorMode = Modes[index];
            UpdateModeButtons();
            ApplyColorMode();
        }

        void UpdateModeButtons()
        {
            for (int i = 0; i < modeButtonImages.Length; i++)
                modeButtonImages[i].color = Modes[i] == colorMode ? PrimaryColor : NeutralColor;
        }

        void ApplyColorMode()
        {
            if (originalPixels == null) return;
            reduceVersion++;
            if (colorMode == ColorMode.Full)
            {
                ShowReduced(originalPixels, imageWidth, imageHeight, originalTexture);
                return;
            }
            StartCoroutine(ReduceColors(reduceVersion, colorMode, originalPixels));
        }

        IEnumerator ReduceColors(int version, ColorMode mode, Color32[] source)
        {
            ShowMessage("msg_reducing");
            // 大きな画像でも画面が止まらないよう別スレッドで計算する
            int sourceWidth = imageWidth, sourceHeight = imageHeight;
            int width = 0, height = 0;
            var task = Task.Run(() => ColorReducer.Reduce(source, sourceWidth, sourceHeight, mode, out width, out height));
            while (!task.IsCompleted) yield return null;
            if (version != reduceVersion || source != originalPixels) yield break; // 途中で別の操作があった

            if (task.IsFaulted)
            {
                Debug.LogException(task.Exception);
                ShowMessage("msg_reduce_failed");
                yield break;
            }

            if (reducedTexture == null || reducedTexture.width != width || reducedTexture.height != height)
            {
                if (reducedTexture != null) Destroy(reducedTexture);
                // 粗くした画素がぼやけず、四角いまま拡大されるようにする
                reducedTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    name = "Reduced",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                };
            }
            reducedTexture.SetPixels32(task.Result);
            reducedTexture.Apply();
            ShowReduced(task.Result, width, height, reducedTexture);
            ShowMessage(targetUv.HasValue ? "msg_mode_changed" : "msg_image_loaded");
        }

        void ShowReduced(Color32[] pixels, int width, int height, Texture2D texture)
        {
            displayedPixels = pixels;
            displayedWidth = width;
            displayedHeight = height;
            imageViewer.SetTexture(texture, false);
            // ターゲットは同じ場所のまま、新しい色で取り直す
            if (targetUv.HasValue) SetTarget(targetUv.Value);
        }

        void OnImageTapped(Vector2 uv)
        {
            SetTarget(uv);
            ShowMessage("msg_target_set");
        }

        void SetTarget(Vector2 uv)
        {
            targetUv = uv;
            int w = displayedWidth, h = displayedHeight;
            int cx = Mathf.Clamp((int)(uv.x * w), 0, w - 1);
            int cy = Mathf.Clamp((int)(uv.y * h), 0, h - 1);
            int radius = w < imageWidth ? 0 : TargetSampleRadius;

            float r = 0, g = 0, b = 0;
            int count = 0;
            for (int y = Mathf.Max(0, cy - radius); y <= Mathf.Min(h - 1, cy + radius); y++)
            {
                for (int x = Mathf.Max(0, cx - radius); x <= Mathf.Min(w - 1, cx + radius); x++)
                {
                    var c = displayedPixels[y * w + x];
                    r += c.r;
                    g += c.g;
                    b += c.b;
                    count++;
                }
            }
            var color = new Color(r / count / 255f, g / count / 255f, b / count / 255f, 1f);
            targetColor = color;
            imageViewer.ShowMarker(uv, ColorWheel.Invert(color));
            UpdateComparison();
        }

        // ───────────────────────── 色の比較 ─────────────────────────

        void UpdateComparison()
        {
            colorWheel.SetTarget(targetColor);
            colorWheel.SetCamera(cameraColor);
            targetSwatch.color = targetColor ?? PanelColor;
            cameraSwatch.color = cameraColor ?? PanelColor;

            if (!targetColor.HasValue)
            {
                SetAdvice("advice_need_target");
                diffText.gameObject.SetActive(false);
                return;
            }
            if (!cameraColor.HasValue)
            {
                SetAdvice("advice_need_camera");
                diffText.gameObject.SetActive(false);
                return;
            }

            var diff = ColorAdvice.Difference(targetColor.Value, cameraColor.Value);
            SetAdvice(ColorAdvice.AdviceKey(diff));
            diffText.gameObject.SetActive(true);
            diffText.SetKey("cmyk_diff", Percent(diff.C), Percent(diff.M), Percent(diff.Y), Percent(diff.K));
        }

        void SetAdvice(string key)
        {
            if (adviceText.Key != key) adviceText.SetKey(key);
        }

        static int Percent(float value)
        {
            return Mathf.RoundToInt(value * 100f);
        }

        // ───────────────────────── 保存とメッセージ ─────────────────────────

        void SaveCapture(Texture2D texture)
        {
            try
            {
                byte[] png = texture.EncodeToPNG();
                string fileName = $"ArtColorSupporter_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                string directory = Path.Combine(Application.persistentDataPath, "Captures");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, fileName);
                File.WriteAllBytes(path, png);

                bool savingToGallery = GalleryPicker.TrySaveToGallery(png, fileName,
                    success => ShowMessage(success ? "msg_saved_gallery" : "msg_save_gallery_failed"));
                if (!savingToGallery) ShowMessage("msg_saved_file", path);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                ShowMessage("msg_save_failed");
            }
        }

        void ShowMessage(string key, params object[] args)
        {
            messageText.SetKey(key, args);
        }

        // ───────────────────────── UI の組み立て ─────────────────────────

        void BuildUI()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f; // 横持ちなので高さ基準

            var background = UIFactory.CreateImage(canvas.transform, "Background", BackgroundColor, false);
            UIFactory.StretchFull(background.rectTransform);

            var safeArea = UIFactory.CreateRect("SafeArea", canvas.transform);
            safeArea.gameObject.AddComponent<SafeArea>();

            BuildImageArea(safeArea);
            BuildRightPanel(safeArea);
        }

        void BuildImageArea(Transform parent)
        {
            var frame = UIFactory.CreateImage(parent, "ImageFrame", PanelColor, true);
            UIFactory.Stretch(frame.rectTransform, new Vector2(0, 0), new Vector2(0.5f, 1), 32, 16, 32, 32);

            imageArea = UIFactory.CreateRect("ImageArea", frame.transform);
            UIFactory.Stretch(imageArea, Vector2.zero, Vector2.one, 16, 16, 16, 16 + 96 + 16);
            imageArea.gameObject.AddComponent<RectMask2D>();
            // タッチを受け取るための透明な面
            var touchSurface = imageArea.gameObject.AddComponent<Image>();
            touchSurface.color = new Color(0, 0, 0, 0);

            var placeholderText = UIFactory.CreateText(imageArea, "Placeholder", "placeholder_image", 40, SubTextColor,
                TextAnchor.MiddleCenter);
            UIFactory.StretchFull((RectTransform)placeholderText.transform, 24);
            placeholder = placeholderText.gameObject;

            var imageRect = UIFactory.CreateRect("DisplayImage", imageArea);
            displayImage = imageRect.gameObject.AddComponent<RawImage>();
            displayImage.raycastTarget = false;
            imageRect.gameObject.SetActive(false);

            var marker = UIFactory.CreateRect("TargetMarker", imageArea);
            var markerImage = marker.gameObject.AddComponent<Image>();
            markerImage.sprite = UIFactory.CrossSprite;
            markerImage.raycastTarget = false;
            marker.sizeDelta = new Vector2(48, 48);

            imageViewer = imageArea.gameObject.AddComponent<ImageViewer>();
            imageViewer.Init(displayImage, marker);
            imageViewer.Tapped += OnImageTapped;

            // 色数の切り替えボタン
            var modeRow = UIFactory.CreateRect("ColorModeButtons", frame.transform);
            UIFactory.Stretch(modeRow, new Vector2(0, 0), new Vector2(1, 0), 16, 16, -(16 + 96), 16);
            var layout = modeRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            modeButtonImages = new Image[Modes.Length];
            for (int i = 0; i < Modes.Length; i++)
            {
                int index = i;
                var button = UIFactory.CreateButton(modeRow, "Mode_" + Modes[i], ModeKeys[i], NeutralColor, TextColor, 34,
                    () => OnModeButton(index));
                modeButtonImages[i] = (Image)button.targetGraphic;
            }
        }

        void BuildRightPanel(Transform parent)
        {
            var panel = UIFactory.CreateRect("RightPanel", parent);
            UIFactory.Stretch(panel, new Vector2(0.5f, 0), new Vector2(1, 1), 16, 32, 32, 32);

            // 上の操作ボタン
            var topBar = UIFactory.CreateRect("TopBar", panel);
            UIFactory.Stretch(topBar, new Vector2(0, 1), new Vector2(1, 1), 0, 0, 0, -100);
            var layout = topBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 16;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            var cameraButton = UIFactory.CreateButton(topBar, "CameraButton", "btn_camera", PrimaryColor, TextColor, 44,
                OnCameraButton);
            cameraButtonImage = (Image)cameraButton.targetGraphic;
            cameraButtonLabel = cameraButton.GetComponentInChildren<LocalizedText>();
            UIFactory.CreateButton(topBar, "ImageButton", "btn_image", SecondaryColor, TextColor, 44, OnImageButton);
            UIFactory.CreateButton(topBar, "LanguageButton", "btn_language", NeutralColor, TextColor, 40,
                OnLanguageButton);

            BuildWheelSection(panel);
            BuildCameraSection(panel);
        }

        void BuildWheelSection(Transform panel)
        {
            wheelSection = UIFactory.CreateRect("WheelSection", panel);
            UIFactory.Stretch(wheelSection, new Vector2(0, 0.5f), new Vector2(1, 1), 0, 0, 100 + 20, 8);

            colorWheel = ColorWheel.Create(wheelSection);
            wheelRoot = (RectTransform)colorWheel.transform;
            wheelRoot.anchorMin = new Vector2(0, 0);
            wheelRoot.anchorMax = new Vector2(0, 1);
            wheelRoot.pivot = new Vector2(0, 0.5f);
            wheelRoot.offsetMin = wheelRoot.offsetMax = Vector2.zero;
            var fitter = wheelRoot.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
            fitter.aspectRatio = ColorWheel.AspectRatio;

            // ホイールの右: 色見本とメッセージ（左端は Update でホイールの幅に合わせる）
            infoColumn = UIFactory.CreateRect("InfoColumn", wheelSection);
            UIFactory.StretchFull(infoColumn);
            var layout = infoColumn.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            targetSwatch = CreateSwatchRow(infoColumn, "TargetRow", "label_target", UIFactory.CrossSprite);
            cameraSwatch = CreateSwatchRow(infoColumn, "CameraRow", "label_camera", UIFactory.RingSprite);

            adviceText = CreateInfoText(infoColumn, "Advice", 32, TextColor, 80, 0);
            diffText = CreateInfoText(infoColumn, "CmykDiff", 24, SubTextColor, 34, 0);
            messageText = CreateInfoText(infoColumn, "Message", 26, SubTextColor, 60, 1);
        }

        Image CreateSwatchRow(Transform parent, string name, string labelKey, Sprite markSprite)
        {
            var row = UIFactory.CreateRect(name, parent);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 56;

            var swatch = UIFactory.CreateImage(row, "Swatch", PanelColor, true);
            UIFactory.Place(swatch.rectTransform, new Vector2(0, 0.5f), new Vector2(96, 56), Vector2.zero);

            var mark = UIFactory.CreateRect("Mark", row).gameObject.AddComponent<Image>();
            mark.sprite = markSprite;
            mark.color = SubTextColor;
            mark.raycastTarget = false;
            UIFactory.Place(mark.rectTransform, new Vector2(0, 0.5f), new Vector2(40, 40), new Vector2(110, 0));

            var label = UIFactory.CreateText(row, "Label", labelKey, 30, TextColor, TextAnchor.MiddleLeft);
            UIFactory.Stretch((RectTransform)label.transform, Vector2.zero, Vector2.one, 160, 0, 0, 0);
            return swatch;
        }

        static LocalizedText CreateInfoText(Transform parent, string name, int fontSize, Color color, float height,
            float flexibleHeight)
        {
            var text = UIFactory.CreateText(parent, name, "", fontSize, color, TextAnchor.UpperLeft);
            var element = text.gameObject.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            element.flexibleHeight = flexibleHeight;
            return text;
        }

        void BuildCameraSection(Transform panel)
        {
            var section = UIFactory.CreateImage(panel, "CameraSection", Color.black, true);
            UIFactory.Stretch(section.rectTransform, new Vector2(0, 0), new Vector2(1, 0.5f), 0, 0, 8, 0);

            previewArea = UIFactory.CreateRect("PreviewArea", section.transform);
            UIFactory.StretchFull(previewArea, 8);
            previewArea.gameObject.AddComponent<RectMask2D>();

            var previewRect = UIFactory.CreateRect("Preview", previewArea);
            previewImage = previewRect.gameObject.AddComponent<RawImage>();
            previewImage.raycastTarget = false;
            previewImage.enabled = false;

            var circleImage = UIFactory.CreateRect("Circle", previewArea).gameObject.AddComponent<Image>();
            circleImage.sprite = UIFactory.OutlinedRingSprite;
            circleImage.raycastTarget = false;
            circle = circleImage.rectTransform;
            UIFactory.Place(circle, new Vector2(0.5f, 0.5f), new Vector2(200, 200), Vector2.zero);
            circle.gameObject.SetActive(false);

            cameraPlaceholder = UIFactory.CreateText(previewArea, "Placeholder", "placeholder_camera", 34, SubTextColor,
                TextAnchor.MiddleCenter);
            UIFactory.StretchFull((RectTransform)cameraPlaceholder.transform, 24);

            shutterButton = UIFactory.CreateButton(section.transform, "ShutterButton", "btn_shutter", ShutterColor,
                DarkTextColor, 34, OnShutterButton);
            UIFactory.Place((RectTransform)shutterButton.transform, new Vector2(1, 0), new Vector2(170, 80),
                new Vector2(-20, 20));
            shutterButton.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (originalTexture != null) Destroy(originalTexture);
            if (reducedTexture != null) Destroy(reducedTexture);
        }
    }
}
