using System.Collections.Generic;
using UnityEngine;

namespace RuneArena.Strokes
{
    public static class StrokePreprocessor
    {
        public const int k_SampleCount = 64;

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
    }
}
