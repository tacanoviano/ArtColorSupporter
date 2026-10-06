using System;
using System.Collections;
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace ArtColorSupporter
{
    /// <summary>WebCamTexture で端末カメラを起動・撮影する。</summary>
    public class CameraController : MonoBehaviour
    {
        public enum StartResult
        {
            Started,
            PermissionDenied,
            NoCamera,
            Failed,
        }

        WebCamTexture webcam;
        bool pausedByApp;

        public WebCamTexture Texture => webcam;
        public bool IsRunning => webcam != null && webcam.isPlaying;

        /// <summary>映像の最初のフレームが届いているか（それまで width/height は 16 になる）。</summary>
        public bool IsReady => IsRunning && webcam.width > 16;

        /// <summary>映像を正しい向きにするための時計回りの回転角（0/90/180/270）。</summary>
        public int RotationAngle => webcam == null ? 0 : ((webcam.videoRotationAngle % 360) + 360) % 360;

        public bool VerticallyMirrored => webcam != null && webcam.videoVerticallyMirrored;

        public IEnumerator StartCamera(Action<StartResult> onDone)
        {
            StopCamera();

#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                bool? granted = null;
                var callbacks = new PermissionCallbacks();
                callbacks.PermissionGranted += _ => granted = true;
                callbacks.PermissionDenied += _ => granted = false;
#if !UNITY_6000_0_OR_NEWER
                callbacks.PermissionDeniedAndDontAskAgain += _ => granted = false;
#endif
                Permission.RequestUserPermission(Permission.Camera, callbacks);
                while (granted == null) yield return null;
                if (granted == false)
                {
                    onDone(StartResult.PermissionDenied);
                    yield break;
                }
            }
#elif UNITY_IOS && !UNITY_EDITOR
            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
                if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
                {
                    onDone(StartResult.PermissionDenied);
                    yield break;
                }
            }
#endif

            var devices = WebCamTexture.devices;
            if (devices.Length == 0)
            {
                onDone(StartResult.NoCamera);
                yield break;
            }

            // 背面カメラを優先する
            var device = devices[0];
            foreach (var d in devices)
            {
                if (!d.isFrontFacing)
                {
                    device = d;
                    break;
                }
            }

            webcam = new WebCamTexture(device.name, 1920, 1080, 30);
            webcam.Play();

            float waited = 0f;
            while (webcam != null && webcam.width <= 16 && waited < 5f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            onDone(IsReady ? StartResult.Started : StartResult.Failed);
        }

        public void StopCamera()
        {
            if (webcam == null) return;
            webcam.Stop();
            Destroy(webcam);
            webcam = null;
        }

        /// <summary>現在のフレームを画面と同じ向きの Texture2D として取得する。</summary>
        public Texture2D Capture()
        {
            if (!IsReady) return null;

            int width = webcam.width;
            int height = webcam.height;
            var pixels = webcam.GetPixels32();
            var rotated = RotateClockwise(pixels, width, height, RotationAngle, VerticallyMirrored,
                out int outWidth, out int outHeight);

            var texture = new Texture2D(outWidth, outHeight, TextureFormat.RGBA32, false);
            texture.SetPixels32(rotated);
            texture.Apply();
            return texture;
        }

        static Color32[] RotateClockwise(Color32[] src, int w, int h, int angle, bool flipVertical,
            out int outW, out int outH)
        {
            bool swap = angle == 90 || angle == 270;
            outW = swap ? h : w;
            outH = swap ? w : h;
            var dst = new Color32[src.Length];

            for (int y = 0; y < h; y++)
            {
                int row = (flipVertical ? h - 1 - y : y) * w;
                for (int x = 0; x < w; x++)
                {
                    int nx, ny;
                    switch (angle)
                    {
                        case 90: nx = y; ny = w - 1 - x; break;
                        case 180: nx = w - 1 - x; ny = h - 1 - y; break;
                        case 270: nx = h - 1 - y; ny = x; break;
                        default: nx = x; ny = y; break;
                    }
                    dst[ny * outW + nx] = src[row + x];
                }
            }
            return dst;
        }

        void OnApplicationPause(bool paused)
        {
            if (webcam == null) return;
            if (paused && webcam.isPlaying)
            {
                webcam.Pause();
                pausedByApp = true;
            }
            else if (!paused && pausedByApp)
            {
                webcam.Play();
                pausedByApp = false;
            }
        }

        void OnDestroy()
        {
            StopCamera();
        }
    }
}
