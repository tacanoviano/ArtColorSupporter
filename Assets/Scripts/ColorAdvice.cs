using UnityEngine;

namespace ArtColorSupporter
{
    /// <summary>CMYK（シアン・マゼンタ・イエロー・黒）での色の比較。</summary>
    public static class ColorAdvice
    {
        /// <summary>これより小さい差（0〜1）は「ほぼ同じ」とみなす。</summary>
        const float MatchThreshold = 0.04f;

        public struct Cmyk
        {
            public float C, M, Y, K;
        }

        public static Cmyk ToCmyk(Color color)
        {
            float k = 1f - Mathf.Max(color.r, Mathf.Max(color.g, color.b));
            if (k >= 0.999f) return new Cmyk { K = 1f };
            float w = 1f - k;
            return new Cmyk
            {
                C = (1f - color.r - k) / w,
                M = (1f - color.g - k) / w,
                Y = (1f - color.b - k) / w,
                K = k,
            };
        }

        /// <summary>ターゲットから今の色を引いた差。プラスはその色が足りないことを表す。</summary>
        public static Cmyk Difference(Color target, Color current)
        {
            var t = ToCmyk(target);
            var c = ToCmyk(current);
            return new Cmyk { C = t.C - c.C, M = t.M - c.M, Y = t.Y - c.Y, K = t.K - c.K };
        }

        /// <summary>
        /// 一番足りない色のメッセージキーを返す。
        /// シアン=青、マゼンタ=赤、イエロー=黄、K=黒として表示する。
        /// </summary>
        public static string AdviceKey(Cmyk diff)
        {
            string key = "advice_more_blue";
            float most = diff.C;
            if (diff.M > most)
            {
                most = diff.M;
                key = "advice_more_red";
            }
            if (diff.Y > most)
            {
                most = diff.Y;
                key = "advice_more_yellow";
            }
            if (diff.K > most)
            {
                most = diff.K;
                key = "advice_more_black";
            }
            if (most >= MatchThreshold) return key;

            float largest = Mathf.Max(Mathf.Max(Mathf.Abs(diff.C), Mathf.Abs(diff.M)),
                Mathf.Max(Mathf.Abs(diff.Y), Mathf.Abs(diff.K)));
            return largest >= MatchThreshold ? "advice_too_much" : "advice_match";
        }
    }
}
