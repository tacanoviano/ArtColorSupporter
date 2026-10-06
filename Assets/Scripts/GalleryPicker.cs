using System;
using System.IO;
using UnityEngine;

namespace ArtColorSupporter
{
    /// <summary>
    /// 端末のフォルダ（写真アプリ/ギャラリー）から画像を選ばせる。
    /// 実機では NativeGallery プラグインを使い、Unity エディタではファイル選択ダイアログを使う。
    /// </summary>
    public static class GalleryPicker
    {
        public enum Result
        {
            Picked,
            Canceled,
            LoadFailed,
            NotAvailable,
        }

        /// <summary>読み込む画像の最大サイズ（長辺 px）。メモリ節約と減色の速さのため。</summary>
        const int MaxImageSize = 2048;

        public static void PickImage(string title, Action<Result, Texture2D> onDone)
        {
#if UNITY_EDITOR
            string path = UnityEditor.EditorUtility.OpenFilePanel(title, "", "png,jpg,jpeg");
            if (string.IsNullOrEmpty(path))
            {
                onDone(Result.Canceled, null);
                return;
            }
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (texture.LoadImage(File.ReadAllBytes(path)))
            {
                onDone(Result.Picked, texture);
            }
            else
            {
                UnityEngine.Object.Destroy(texture);
                onDone(Result.LoadFailed, null);
            }
#elif NATIVE_GALLERY
            NativeGallery.GetImageFromGallery(path =>
            {
                if (string.IsNullOrEmpty(path))
                {
                    onDone(Result.Canceled, null);
                    return;
                }
                // 後で色の解析に使えるよう読み取り可能なテクスチャとして読み込む
                var texture = NativeGallery.LoadImageAtPath(path, MaxImageSize, false, false);
                onDone(texture != null ? Result.Picked : Result.LoadFailed, texture);
            }, title, "image/*");
#else
            Debug.LogWarning("NativeGallery is not installed. See README.md.");
            onDone(Result.NotAvailable, null);
#endif
        }

        /// <summary>撮影画像を端末のアルバムに保存する（実機 + NativeGallery 導入時のみ）。</summary>
        public static bool TrySaveToGallery(byte[] pngBytes, string fileName, Action<bool> onDone)
        {
#if NATIVE_GALLERY && !UNITY_EDITOR
            NativeGallery.SaveImageToGallery(pngBytes, "ArtColorSupporter", fileName, (success, _) => onDone(success));
            return true;
#else
            return false;
#endif
        }
    }
}
