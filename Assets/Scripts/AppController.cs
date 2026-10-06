using System;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ArtColorSupporter
{
    /// <summary>
    /// 最初の画面。UI をコードで組み立て、カメラ・画像選択・言語切り替えを担当する。
    /// 画面の左半分に撮影/選択した画像を表示し、右半分に操作ボタンと説明を並べる。
    /// </summary>
    public class AppController : MonoBehaviour
    {
        static readonly Color BackgroundColor = new Color32(0x22, 0x24, 0x28, 0xFF);
        static readonly Color PanelColor = new Color32(0x2F, 0x32, 0x38, 0xFF);
        static readonly Color PrimaryColor = new Color32(0x4C, 0x8B, 0xF5, 0xFF);
        static readonly Color SecondaryColor = new Color32(0x3E, 0xB4, 0x89, 0xFF);
        static readonly Color NeutralColor = new Color32(0x55, 0x5A, 0x64, 0xFF);
        static readonly Color ShutterColor = new Color32(0xF2, 0xF2, 0xF2, 0xFF);
        static readonly Color TextColor = new Color32(0xF5, 0xF5, 0xF5, 0xFF);
        static readonly Color SubTextColor = new Color32(0xA0, 0xA6, 0xB0, 0xFF);
        static readonly Color DarkTextColor = new Color32(0x20, 0x20, 0x20, 0xFF);

        // 左半分の画像表示
        RectTransform imageArea;
        RawImage displayImage;
        GameObject placeholder;
        Texture2D currentTexture;

        // 右半分の操作パネル
        LocalizedText messageText;

        // カメラ画面
        GameObject cameraOverlay;
        RectTransform previewArea;
        RawImage previewImage;
        Button shutterButton;
        LocalizedText cameraMessage;
        CameraController cameraController;

        bool busy;

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
        }

        void Update()
        {
            if (currentTexture != null && displayImage.gameObject.activeSelf)
                UIFactory.FitTexture(displayImage.rectTransform, imageArea, currentTexture.width, currentTexture.height);

            if (cameraOverlay.activeSelf)
            {
                if (cameraController.IsReady)
                {
                    var webcam = cameraController.Texture;
                    previewImage.enabled = true;
                    previewImage.uvRect = cameraController.VerticallyMirrored ? new Rect(0, 1, 1, -1) : new Rect(0, 0, 1, 1);
                    UIFactory.FitTexture(previewImage.rectTransform, previewArea, webcam.width, webcam.height,
                        cameraController.RotationAngle);
                }
                shutterButton.interactable = cameraController.IsReady;

#if ENABLE_LEGACY_INPUT_MANAGER
                // Android の戻るボタンでカメラを閉じる
                if (Input.GetKeyDown(KeyCode.Escape)) CloseCamera();
#endif
            }
        }

        static void ApplyLandscapeOrientation()
        {
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
        }

        // ───────────────────────── ボタン操作 ─────────────────────────

        void OnCameraButton()
        {
            if (busy) return;
            busy = true;
            cameraOverlay.SetActive(true);
            previewImage.enabled = false;
            cameraMessage.SetKey("msg_camera_starting");
            StartCoroutine(cameraController.StartCamera(OnCameraStarted));
        }

        void OnCameraStarted(CameraController.StartResult result)
        {
            busy = false;
            switch (result)
            {
                case CameraController.StartResult.Started:
                    previewImage.texture = cameraController.Texture;
                    cameraMessage.SetKey("msg_camera_hint");
                    break;
                case CameraController.StartResult.PermissionDenied:
                    CloseCamera();
                    ShowMessage("msg_camera_denied");
                    break;
                case CameraController.StartResult.NoCamera:
                    CloseCamera();
                    ShowMessage("msg_camera_no_device");
                    break;
                default:
                    CloseCamera();
                    ShowMessage("msg_camera_failed");
                    break;
            }
        }

        void OnShutterButton()
        {
            var captured = cameraController.Capture();
            if (captured == null)
            {
                cameraMessage.SetKey("msg_capture_failed");
                return;
            }

            CloseCamera();
            SetDisplayedTexture(captured);
            SaveCapture(captured);
        }

        void CloseCamera()
        {
            StopAllCoroutines();
            busy = false;
            cameraController.StopCamera();
            previewImage.texture = null;
            cameraOverlay.SetActive(false);
        }

        void OnImageButton()
        {
            if (busy) return;
            busy = true;
            ShowMessage("msg_picking");
            GalleryPicker.PickImage(Localization.Get("picker_title"), OnImagePicked);
        }

        void OnImagePicked(GalleryPicker.Result result, Texture2D texture)
        {
            busy = false;
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

        // ───────────────────────── 画像の表示と保存 ─────────────────────────

        void SetDisplayedTexture(Texture2D texture)
        {
            if (currentTexture != null && currentTexture != texture) Destroy(currentTexture);
            currentTexture = texture;
            displayImage.texture = texture;
            displayImage.gameObject.SetActive(true);
            placeholder.SetActive(false);
            UIFactory.FitTexture(displayImage.rectTransform, imageArea, texture.width, texture.height);
        }

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
            BuildControlPanel(safeArea);
            BuildCameraOverlay(canvas.transform);
        }

        void BuildImageArea(Transform parent)
        {
            var frame = UIFactory.CreateImage(parent, "ImageFrame", PanelColor, true);
            UIFactory.Stretch(frame.rectTransform, new Vector2(0, 0), new Vector2(0.5f, 1), 32, 16, 32, 32);

            imageArea = UIFactory.CreateRect("ImageArea", frame.transform);
            UIFactory.StretchFull(imageArea, 16);
            imageArea.gameObject.AddComponent<RectMask2D>();

            var placeholderText = UIFactory.CreateText(imageArea, "Placeholder", "placeholder_image", 40, SubTextColor,
                TextAnchor.MiddleCenter);
            UIFactory.StretchFull((RectTransform)placeholderText.transform, 24);
            placeholder = placeholderText.gameObject;

            var imageRect = UIFactory.CreateRect("DisplayImage", imageArea);
            displayImage = imageRect.gameObject.AddComponent<RawImage>();
            displayImage.raycastTarget = false;
            imageRect.gameObject.SetActive(false);
        }

        void BuildControlPanel(Transform parent)
        {
            var panel = UIFactory.CreateRect("ControlPanel", parent);
            UIFactory.Stretch(panel, new Vector2(0.5f, 0), new Vector2(1, 1), 16, 32, 32, 32);

            var title = UIFactory.CreateText(panel, "Title", "app_title", 56, TextColor, TextAnchor.MiddleLeft);
            // 上端に高さ 110 で配置（右側は言語ボタンのために空ける）
            UIFactory.Stretch((RectTransform)title.transform, new Vector2(0, 1), new Vector2(1, 1), 8, 300, 0, -110);

            var languageButton = UIFactory.CreateButton(panel, "LanguageButton", "btn_language", NeutralColor, TextColor,
                40, OnLanguageButton);
            UIFactory.Place((RectTransform)languageButton.transform, new Vector2(1, 1), new Vector2(260, 110), Vector2.zero);

            var buttons = UIFactory.CreateRect("MainButtons", panel);
            UIFactory.Stretch(buttons, new Vector2(0, 0.4f), new Vector2(1, 1), 0, 0, 150, 0);
            var layout = buttons.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 32;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            UIFactory.CreateButton(buttons, "CameraButton", "btn_camera", PrimaryColor, TextColor, 64,
                OnCameraButton);
            UIFactory.CreateButton(buttons, "ImageButton", "btn_image", SecondaryColor, TextColor, 64,
                OnImageButton);

            var messageBox = UIFactory.CreateImage(panel, "MessageBox", PanelColor, true);
            UIFactory.Stretch(messageBox.rectTransform, new Vector2(0, 0), new Vector2(1, 0.4f), 0, 0, 32, 0);
            messageText = UIFactory.CreateText(messageBox.transform, "Message", "", 40, TextColor, TextAnchor.MiddleLeft);
            UIFactory.StretchFull((RectTransform)messageText.transform, 32);
        }

        void BuildCameraOverlay(Transform parent)
        {
            var overlay = UIFactory.CreateImage(parent, "CameraOverlay", Color.black, false);
            UIFactory.StretchFull(overlay.rectTransform);
            cameraOverlay = overlay.gameObject;

            previewArea = UIFactory.CreateRect("PreviewArea", overlay.transform);
            UIFactory.StretchFull(previewArea);
            var previewRect = UIFactory.CreateRect("Preview", previewArea);
            previewImage = previewRect.gameObject.AddComponent<RawImage>();
            previewImage.raycastTarget = false;

            var controls = UIFactory.CreateRect("OverlaySafeArea", overlay.transform);
            controls.gameObject.AddComponent<SafeArea>();

            shutterButton = UIFactory.CreateButton(controls, "ShutterButton", "btn_shutter", ShutterColor, DarkTextColor,
                52, OnShutterButton);
            UIFactory.Place((RectTransform)shutterButton.transform, new Vector2(1, 0.5f), new Vector2(240, 240),
                new Vector2(-40, 0));

            var closeButton = UIFactory.CreateButton(controls, "CloseButton", "btn_close", NeutralColor, TextColor, 40,
                CloseCamera);
            UIFactory.Place((RectTransform)closeButton.transform, new Vector2(0, 1), new Vector2(240, 110),
                new Vector2(40, -40));

            var hintBox = UIFactory.CreateImage(controls, "HintBox", new Color(0, 0, 0, 0.55f), true);
            UIFactory.Place(hintBox.rectTransform, new Vector2(0.5f, 0), new Vector2(1100, 110), new Vector2(0, 40));
            cameraMessage = UIFactory.CreateText(hintBox.transform, "Hint", "", 38, TextColor, TextAnchor.MiddleCenter);
            UIFactory.StretchFull((RectTransform)cameraMessage.transform, 16);

            cameraOverlay.SetActive(false);
        }

        void OnDestroy()
        {
            if (currentTexture != null) Destroy(currentTexture);
        }
    }
}
