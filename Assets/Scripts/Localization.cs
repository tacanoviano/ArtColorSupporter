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
            { "btn_camera_stop", new[] { "カメラ停止", "Stop camera" } },
            { "btn_image", new[] { "画像", "Image" } },
            { "btn_language", new[] { "English", "日本語" } }, // 切り替え先の言語を表示する
            { "btn_shutter", new[] { "撮影", "Capture" } },

            // 色数の切り替え
            { "mode_full", new[] { "フルカラー", "Full color" } },
            { "mode_256", new[] { "256色", "256 colors" } },
            { "mode_16", new[] { "16色", "16 colors" } },
            { "mode_8", new[] { "8色", "8 colors" } },
            { "mode_mono", new[] { "モノクロ", "Mono" } },

            // 画像・カメラのエリア
            { "placeholder_image", new[] { "ここに画像が表示されます", "Your image will appear here" } },
            { "placeholder_camera", new[] {
                "「カメラ」を押すとここに映像が表示されます",
                "Tap \"Camera\" to show the camera view here" } },
            { "picker_title", new[] { "画像を選択", "Select an image" } },

            // 色の比較
            { "label_target", new[] { "ターゲット", "Target" } },
            { "label_camera", new[] { "カメラ", "Camera" } },
            { "advice_need_target", new[] {
                "左の画像をタップして、ターゲットの色を選んでください。",
                "Tap the left image to pick a target color." } },
            { "advice_need_camera", new[] {
                "「カメラ」を押して、塗った色を白い円の中に映してください。",
                "Tap \"Camera\" and put your painted color inside the white circle." } },
            { "advice_more_blue", new[] { "いちばん足りないのは 青（シアン）です。", "Most needed: blue (cyan)." } },
            { "advice_more_red", new[] { "いちばん足りないのは 赤（マゼンタ）です。", "Most needed: red (magenta)." } },
            { "advice_more_yellow", new[] { "いちばん足りないのは 黄 です。", "Most needed: yellow." } },
            { "advice_more_black", new[] { "いちばん足りないのは 黒 です。", "Most needed: black." } },
            { "advice_too_much", new[] {
                "足りない色はありません。白を混ぜて明るくしてみてください。",
                "Nothing is missing. Try adding white to lighten it." } },
            { "advice_match", new[] { "ターゲットとほぼ同じ色です！", "Almost the same as the target!" } },
            { "cmyk_diff", new[] {
                "不足量 C {0:+0;-0;0}%  M {1:+0;-0;0}%  Y {2:+0;-0;0}%  K {3:+0;-0;0}%",
                "Needed C {0:+0;-0;0}%  M {1:+0;-0;0}%  Y {2:+0;-0;0}%  K {3:+0;-0;0}%" } },

            // メッセージ
            { "msg_welcome", new[] {
                "「画像」で写真を選ぶか、「カメラ」の「撮影」で左に画像を表示してください。",
                "Choose a photo with \"Image\", or use \"Capture\" in camera mode to show one on the left." } },
            { "msg_camera_starting", new[] { "カメラを起動しています…", "Starting the camera..." } },
            { "msg_camera_hint", new[] {
                "塗った色を白い円の中に映してください。「撮影」で左に画像を表示します。",
                "Put your painted color inside the white circle. \"Capture\" shows the photo on the left." } },
            { "msg_camera_stopped", new[] { "カメラを止めました。", "Camera stopped." } },
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
            { "msg_image_loaded", new[] {
                "色を調べたい場所をタップしてください。2本指で拡大・移動できます。",
                "Tap a spot to pick its color. Use two fingers to zoom and move." } },
            { "msg_image_canceled", new[] { "画像の選択をキャンセルしました。", "Image selection was canceled." } },
            { "msg_image_failed", new[] { "画像を読み込めませんでした。", "Could not load the image." } },
            { "msg_gallery_unavailable", new[] {
                "画像選択プラグイン（NativeGallery）が導入されていません。",
                "The image picker plugin (NativeGallery) is not installed." } },
            { "msg_reducing", new[] { "色を減らしています…", "Reducing colors..." } },
            { "msg_reduce_failed", new[] { "色を減らせませんでした。", "Could not reduce the colors." } },
            { "msg_mode_changed", new[] {
                "表示する色の数を変えました。ターゲットの色も取り直しました。",
                "Changed the number of colors. The target color was picked again." } },
            { "msg_target_set", new[] {
                "ターゲットの色を選びました。別の場所をタップすると選び直せます。",
                "Target color picked. Tap another spot to change it." } },
        };
    }
}
