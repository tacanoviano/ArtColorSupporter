using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArtColorSupporter
{
    /// <summary>左の画像の色数（フルカラー / 256色 / 16色 / 8色 / モノクロ）。</summary>
    public enum ColorMode
    {
        Full,
        Colors256,
        Colors16,
        Colors8,
        Mono,
    }

    /// <summary>
    /// 画像の色を減らす。256/16/8 色はメディアンカット法で画像に合った色（パレット）を選び、
    /// 各ピクセルをいちばん近いパレット色に置き換える。モノクロはグレースケールにする。
    /// Unity API を使わないので別スレッドから呼んでよい。
    /// </summary>
    public static class ColorReducer
    {
        /// <summary>パレットを作るときに使うピクセル数の上限（速度のため間引く）。</summary>
        const int MaxSamples = 65536;

        public static int ColorCount(ColorMode mode)
        {
            switch (mode)
            {
                case ColorMode.Colors256: return 256;
                case ColorMode.Colors16: return 16;
                case ColorMode.Colors8: return 8;
                default: return 0;
            }
        }

        public static Color32[] Reduce(Color32[] source, ColorMode mode)
        {
            if (mode == ColorMode.Full) return source;
            if (mode == ColorMode.Mono) return ToGrayscale(source);
            return Quantize(source, ColorCount(mode));
        }

        static Color32[] ToGrayscale(Color32[] source)
        {
            var result = new Color32[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                var c = source[i];
                byte y = (byte)Math.Min(255, (int)(0.299f * c.r + 0.587f * c.g + 0.114f * c.b + 0.5f));
                result[i] = new Color32(y, y, y, 255);
            }
            return result;
        }

        static Color32[] Quantize(Color32[] source, int colorCount)
        {
            var palette = BuildPalette(source, colorCount);

            // RGB 各 5bit (32×32×32) ごとに最も近いパレット番号を先に求めておく
            var lookup = new byte[32 * 32 * 32];
            for (int r = 0; r < 32; r++)
            for (int g = 0; g < 32; g++)
            for (int b = 0; b < 32; b++)
                lookup[(r << 10) | (g << 5) | b] = (byte)Nearest(palette, (r << 3) + 4, (g << 3) + 4, (b << 3) + 4);

            var result = new Color32[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                var c = source[i];
                result[i] = palette[lookup[((c.r >> 3) << 10) | ((c.g >> 3) << 5) | (c.b >> 3)]];
            }
            return result;
        }

        static int Nearest(Color32[] palette, int r, int g, int b)
        {
            int best = 0;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < palette.Length; i++)
            {
                int dr = palette[i].r - r, dg = palette[i].g - g, db = palette[i].b - b;
                // 人の目は緑の差に敏感なので重みを付ける
                int distance = 3 * dr * dr + 4 * dg * dg + 2 * db * db;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }
            return best;
        }

        struct Box
        {
            public int Start;
            public int Length;
        }

        static Color32[] BuildPalette(Color32[] source, int colorCount)
        {
            int stride = Math.Max(1, source.Length / MaxSamples);
            var samples = new int[(source.Length + stride - 1) / stride];
            for (int i = 0, n = 0; i < source.Length && n < samples.Length; i += stride, n++)
            {
                var c = source[i];
                samples[n] = (c.r << 16) | (c.g << 8) | c.b;
            }

            var boxes = new List<Box> { new Box { Start = 0, Length = samples.Length } };
            while (boxes.Count < colorCount)
            {
                // 色の広がりが大きく、ピクセルの多い箱を半分に分ける
                int target = -1, targetShift = 0;
                float bestScore = 0;
                for (int i = 0; i < boxes.Count; i++)
                {
                    if (boxes[i].Length < 2) continue;
                    int shift = WidestChannel(samples, boxes[i], out int range);
                    float score = range * (float)Math.Sqrt(boxes[i].Length);
                    if (range > 0 && score > bestScore)
                    {
                        bestScore = score;
                        target = i;
                        targetShift = shift;
                    }
                }
                if (target < 0) break; // これ以上分けられない（色数が少ない画像）

                var box = boxes[target];
                Array.Sort(samples, box.Start, box.Length, new ChannelComparer(targetShift));
                int half = box.Length / 2;
                boxes[target] = new Box { Start = box.Start, Length = half };
                boxes.Add(new Box { Start = box.Start + half, Length = box.Length - half });
            }

            var palette = new Color32[boxes.Count];
            for (int i = 0; i < boxes.Count; i++)
            {
                long r = 0, g = 0, b = 0;
                var box = boxes[i];
                for (int j = box.Start; j < box.Start + box.Length; j++)
                {
                    r += (samples[j] >> 16) & 0xFF;
                    g += (samples[j] >> 8) & 0xFF;
                    b += samples[j] & 0xFF;
                }
                int count = Math.Max(1, box.Length);
                palette[i] = new Color32((byte)(r / count), (byte)(g / count), (byte)(b / count), 255);
            }
            return palette;
        }

        /// <summary>箱の中で最も値の幅が広いチャンネルのビット位置（R=16, G=8, B=0）を返す。</summary>
        static int WidestChannel(int[] samples, Box box, out int range)
        {
            int minR = 255, minG = 255, minB = 255, maxR = 0, maxG = 0, maxB = 0;
            for (int j = box.Start; j < box.Start + box.Length; j++)
            {
                int r = (samples[j] >> 16) & 0xFF, g = (samples[j] >> 8) & 0xFF, b = samples[j] & 0xFF;
                if (r < minR) minR = r;
                if (r > maxR) maxR = r;
                if (g < minG) minG = g;
                if (g > maxG) maxG = g;
                if (b < minB) minB = b;
                if (b > maxB) maxB = b;
            }
            int rangeR = maxR - minR, rangeG = maxG - minG, rangeB = maxB - minB;
            if (rangeG >= rangeR && rangeG >= rangeB)
            {
                range = rangeG;
                return 8;
            }
            if (rangeR >= rangeB)
            {
                range = rangeR;
                return 16;
            }
            range = rangeB;
            return 0;
        }

        sealed class ChannelComparer : IComparer<int>
        {
            readonly int shift;

            public ChannelComparer(int shift)
            {
                this.shift = shift;
            }

            public int Compare(int a, int b)
            {
                return ((a >> shift) & 0xFF) - ((b >> shift) & 0xFF);
            }
        }
    }
}
