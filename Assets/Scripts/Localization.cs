using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArtColorSupporter
{
    public enum Language
    {
        Japanese = 0,
        English = 1,
    }

    /// <summary>
    /// アプリ内の全テキストを日本語/英語で管理する。
    /// 新しいボタンやメッセージを追加するときは Table にキーを追加し、
    /// LocalizedText (または Localization.Get) 経由で表示すること。
    /// </summary>
    public static class Localization
    {
        const string PrefKey = "ArtColorSupporter.Language";

        static bool loaded;
        static Language current;

        /// <summary>言語が切り替わったときに呼ばれる。</summary>
        public static event Action LanguageChanged;

        public static Language Current
        {
            get
            {
                EnsureLoaded();
                return current;
            }
            set
            {
                EnsureLoaded();
                if (current == value) return;
                current = value;
                PlayerPrefs.SetInt(PrefKey, (int)value);
                PlayerPrefs.Save();
                LanguageChanged?.Invoke();
            }
        }

        public static void Toggle()
        {
            Current = Current == Language.Japanese ? Language.English : Language.Japanese;
        }

        public static string Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            if (Table.TryGetValue(key, out var texts)) return texts[(int)Current];
            Debug.LogWarning($"[Localization] Missing key: {key}");
            return key;
        }

        public static string Get(string key, params object[] args)
        {
            var format = Get(key);
            return args == null || args.Length == 0 ? format : string.Format(format, args);
        }

        static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            current = (Language)PlayerPrefs.GetInt(PrefKey, (int)Language.Japanese);
        }

        // key -> { 日本語, English }
        static readonly Dictionary<string, string[]> Table = new Dictionary<string, string[]>
        {
            // タイトル
            { "app_title", new[] { "ArtColorSupporter", "ArtColorSupporter" } },

            // ボタン
            { "btn_camera", new[] { "カメラ", "Camera" } },
            { "btn_image", new[] { "画像", "Image" } },
            { "btn_language", new[] { "English", "日本語" } }, // 切り替え先の言語を表示する
            { "btn_shutter", new[] { "撮影", "Capture" } },
            { "btn_close", new[] { "閉じる", "Close" } },

            // 画像エリア
            { "placeholder_image", new[] { "ここに画像が表示されます", "Your image will appear here" } },
            { "picker_title", new[] { "画像を選択", "Select an image" } },

            // メッセージ
            { "msg_welcome", new[] {
                "「カメラ」で撮影するか、「画像」で端末の写真を選んでください。",
                "Tap \"Camera\" to take a photo, or \"Image\" to choose one from your device." } },
            { "msg_camera_starting", new[] { "カメラを起動しています…", "Starting the camera..." } },
            { "msg_camera_hint", new[] {
                "描きたいものにカメラを向けて「撮影」を押してください。",
                "Point the camera at your subject and tap \"Capture\"." } },
            { "msg_camera_no_device", new[] { "使用できるカメラが見つかりません。", "No camera is available on this device." } },
            { "msg_camera_denied", new[] {
                "カメラの使用が許可されていません。端末の設定から許可してください。",
                "Camera access was denied. Please allow it in your device settings." } },
            { "msg_camera_failed", new[] { "カメラを起動できませんでした。", "Could not start the camera." } },
            { "msg_capture_failed", new[] { "撮影に失敗しました。", "Failed to capture the image." } },
            { "msg_saved_gallery", new[] {
                "撮影した画像を保存しました（アルバム: ArtColorSupporter）。",
                "Photo saved (album: ArtColorSupporter)." } },
            { "msg_saved_file", new[] { "撮影した画像を保存しました:\n{0}", "Photo saved:\n{0}" } },
            { "msg_save_gallery_failed", new[] {
                "アプリ内には保存しましたが、アルバムへの保存に失敗しました。",
                "Saved inside the app, but could not save to the photo album." } },
            { "msg_save_failed", new[] { "画像の保存に失敗しました。", "Failed to save the photo." } },
            { "msg_picking", new[] { "画像を選んでください。", "Please choose an image." } },
            { "msg_image_loaded", new[] { "画像を読み込みました。", "Image loaded." } },
            { "msg_image_canceled", new[] { "画像の選択をキャンセルしました。", "Image selection was canceled." } },
            { "msg_image_failed", new[] { "画像を読み込めませんでした。", "Could not load the image." } },
            { "msg_gallery_unavailable", new[] {
                "画像選択プラグイン（NativeGallery）が導入されていません。",
                "The image picker plugin (NativeGallery) is not installed." } },
        };
    }
}
