using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuneArena.Strokes
{
    public static class StrokePreprocessor
    {
        public const int k_SampleCount = 64;
        public const int k_RasterSize = 28;

        const int k_RasterMargin = 2;
        const int k_RasterSubsteps = 4;

        // Rig-space points into the head plane captured at stroke start (x = right, y = up).
        public static void Project(IReadOnlyList<Vector4> points, Quaternion headRotation, List<Vector2> output)
        {
            output.Clear();
            Quaternion toHead = Quaternion.Inverse(headRotation);
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 p = toHead * (Vector3)points[i];
                output.Add(new Vector2(p.x, p.y));
            }
        }

        public static float PathLength(List<Vector2> points)
        {
            float length = 0f;
            for (int i = 1; i < points.Count; i++)
                length += Vector2.Distance(points[i - 1], points[i]);
            return length;
        }

        // Evenly spaced points along the path, as many as output.Length.
        public static void Resample(List<Vector2> points, Vector2[] output)
        {
            int n = output.Length;
            float interval = PathLength(points) / (n - 1);
            float accumulated = 0f;
            Vector2 prev = points[0];
            output[0] = prev;
            int count = 1;
            int i = 1;

            while (i < points.Count && count < n)
            {
                Vector2 cur = points[i];
                float d = Vector2.Distance(prev, cur);
                if (d > 0f && accumulated + d >= interval)
                {
                    prev = Vector2.LerpUnclamped(prev, cur, (interval - accumulated) / d);
                    output[count++] = prev;
                    accumulated = 0f;
                }
                else
                {
                    accumulated += d;
                    prev = cur;
                    i++;
                }
            }

            while (count < n)
                output[count++] = points[points.Count - 1];
        }

        // Centroid at the origin and uniform scale so the larger bounding box side is 1.
        public static void Normalize(Vector2[] points)
        {
            Vector2 min = points[0];
            Vector2 max = points[0];
            Vector2 sum = Vector2.zero;
            for (int i = 0; i < points.Length; i++)
            {
                min = Vector2.Min(min, points[i]);
                max = Vector2.Max(max, points[i]);
                sum += points[i];
            }

            Vector2 centroid = sum / points.Length;
            float size = Mathf.Max(max.x - min.x, max.y - min.y);
            float scale = size > 1e-6f ? 1f / size : 1f;
            for (int i = 0; i < points.Length; i++)
                points[i] = (points[i] - centroid) * scale;
        }
    
        // Normalized points into a k_RasterSize x k_RasterSize image, row 0 at the top, values in [0, 1].
        // Must match rasterize() in ml/runes/preprocess.py.
        public static void Rasterize(Vector2[] points, float[] output)
        {
            Array.Clear(output, 0, output.Length);

            Vector2 min = points[0];
            Vector2 max = points[0];
            for (int i = 1; i < points.Length; i++)
            {
                min = Vector2.Min(min, points[i]);
                max = Vector2.Max(max, points[i]);
            }

            Vector2 center = (min + max) * 0.5f;
            float scale = k_RasterSize - 1 - 2 * k_RasterMargin;
            float half = (k_RasterSize - 1) * 0.5f;

            for (int i = 0; i < points.Length - 1; i++)
            {
                for (int s = 0; s < k_RasterSubsteps; s++)
                {
                    Vector2 p = Vector2.LerpUnclamped(points[i], points[i + 1], (float)s / k_RasterSubsteps);
                    Splat(p, center, scale, half, output);
                }
            }
            Splat(points[points.Length - 1], center, scale, half, output);

            for (int i = 0; i < output.Length; i++)
            {
                if (output[i] > 1f)
                    output[i] = 1f;
            }
        }

        static void Splat(Vector2 p, Vector2 center, float scale, float half, float[] image)
        {
            float x = (p.x - center.x) * scale + half;
            float y = half - (p.y - center.y) * scale;
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            float fx = x - x0;
            float fy = y - y0;

            AddPixel(image, x0, y0, (1f - fx) * (1f - fy));
            AddPixel(image, x0 + 1, y0, fx * (1f - fy));
            AddPixel(image, x0, y0 + 1, (1f - fx) * fy);
            AddPixel(image, x0 + 1, y0 + 1, fx * fy);
        }

        static void AddPixel(float[] image, int x, int y, float weight)
        {
            if (x >= 0 && x < k_RasterSize && y >= 0 && y < k_RasterSize)
                image[y * k_RasterSize + x] += weight;
        }
    }
}
