using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ArtColorSupporter
{
    /// <summary>
    /// 左の画像表示。1本指ドラッグで移動、2本指ピンチ（エディタではマウスホイール）で拡大縮小、
    /// タップで画像上の位置（UV 0〜1）を通知する。表示エリア自身に付けて使う。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class ImageViewer : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IInitializePotentialDragHandler, IDragHandler, IScrollHandler
    {
        const float MaxZoom = 12f;

        RectTransform area;
        RawImage image;
        RectTransform marker;
        int textureWidth, textureHeight;
        float zoom = 1f;
        Vector2 pan;

        readonly Dictionary<int, Vector2> pointers = new Dictionary<int, Vector2>();
        bool tapCandidate;
        int tapPointerId;
        Vector2 tapStart;

        /// <summary>画像がタップされたときに UV 座標（左下 0,0 / 右上 1,1）で呼ばれる。</summary>
        public event Action<Vector2> Tapped;

        float TapMoveThreshold => Mathf.Max(20f, Screen.dpi * 0.12f);

        public void Init(RawImage displayImage, RectTransform targetMarker)
        {
            area = (RectTransform)transform;
            image = displayImage;
            marker = targetMarker;
            marker.SetParent(image.rectTransform, false);
            marker.gameObject.SetActive(false);
        }

        public void SetTexture(Texture texture, bool resetView)
        {
            image.texture = texture;
            textureWidth = texture.width;
            textureHeight = texture.height;
            if (resetView)
            {
                zoom = 1f;
                pan = Vector2.zero;
                HideMarker();
            }
            Layout();
        }

        public void ShowMarker(Vector2 uv, Color color)
        {
            marker.anchorMin = marker.anchorMax = uv;
            marker.anchoredPosition = Vector2.zero;
            marker.GetComponent<Graphic>().color = color;
            marker.gameObject.SetActive(true);
        }

        public void HideMarker()
        {
            marker.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            Layout();
        }

        void Layout()
        {
            if (image == null || textureWidth <= 0 || textureHeight <= 0) return;
            var areaSize = area.rect.size;
            float fit = Mathf.Min(areaSize.x / textureWidth, areaSize.y / textureHeight);
            var size = new Vector2(textureWidth, textureHeight) * (fit * zoom);

            // 画像がエリアより大きい方向だけ動かせる（はみ出しすぎない）
            float maxX = Mathf.Max(0, (size.x - areaSize.x) / 2);
            float maxY = Mathf.Max(0, (size.y - areaSize.y) / 2);
            pan = new Vector2(Mathf.Clamp(pan.x, -maxX, maxX), Mathf.Clamp(pan.y, -maxY, maxY));

            var rt = image.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pan;
        }

        Vector2 ToLocal(Vector2 screenPosition)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screenPosition, null, out var local);
            return local;
        }

        void ZoomAround(Vector2 localPoint, float factor)
        {
            float newZoom = Mathf.Clamp(zoom * factor, 1f, MaxZoom);
            pan = localPoint - (localPoint - pan) * (newZoom / zoom);
            zoom = newZoom;
            Layout();
        }

        static bool IsPrimary(PointerEventData eventData)
        {
            // タッチは左ボタン扱いで届くので、マウスの右・中ボタンだけ除く
            return eventData.button == PointerEventData.InputButton.Left;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!IsPrimary(eventData)) return;
            pointers[eventData.pointerId] = eventData.position;
            if (pointers.Count == 1)
            {
                tapCandidate = true;
                tapPointerId = eventData.pointerId;
                tapStart = eventData.position;
            }
            else
            {
                tapCandidate = false;
            }
        }

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            // 指の動きにすぐ追従させる
            eventData.useDragThreshold = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            int id = eventData.pointerId;
            if (!pointers.ContainsKey(id)) return;

            if (pointers.Count >= 2)
            {
                int a = -999, b = -999;
                foreach (var key in pointers.Keys)
                {
                    if (a == -999) a = key;
                    else if (b == -999) b = key;
                }
                Vector2 a0 = ToLocal(pointers[a]), b0 = ToLocal(pointers[b]);
                pointers[id] = eventData.position;
                Vector2 a1 = ToLocal(pointers[a]), b1 = ToLocal(pointers[b]);

                float before = (a0 - b0).magnitude;
                Vector2 center0 = (a0 + b0) / 2, center1 = (a1 + b1) / 2;
                if (before > 1f) ZoomAround(center0, (a1 - b1).magnitude / before);
                pan += center1 - center0;
            }
            else
            {
                Vector2 before = ToLocal(pointers[id]);
                pointers[id] = eventData.position;
                pan += ToLocal(eventData.position) - before;
            }
            Layout();

            if (tapCandidate && (eventData.position - tapStart).magnitude > TapMoveThreshold) tapCandidate = false;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!pointers.Remove(eventData.pointerId)) return;
            bool isTap = tapCandidate && eventData.pointerId == tapPointerId &&
                         (eventData.position - tapStart).magnitude <= TapMoveThreshold;
            tapCandidate = false;
            if (!isTap || image.texture == null) return;

            var rt = image.rectTransform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, eventData.position, null, out var local))
                return;
            var rect = rt.rect;
            var uv = new Vector2((local.x - rect.xMin) / rect.width, (local.y - rect.yMin) / rect.height);
            if (uv.x < 0 || uv.x > 1 || uv.y < 0 || uv.y > 1) return;
            Tapped?.Invoke(uv);
        }

        public void OnScroll(PointerEventData eventData)
        {
            // ホイール量の単位は入力方式によって違うので、向きだけを使う
            float direction = Mathf.Sign(eventData.scrollDelta.y);
            if (eventData.scrollDelta.y != 0) ZoomAround(ToLocal(eventData.position), Mathf.Pow(1.15f, direction));
        }

        void OnDisable()
        {
            pointers.Clear();
            tapCandidate = false;
        }
    }
}
