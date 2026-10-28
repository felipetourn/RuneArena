using System.Collections.Generic;
using UnityEngine;

namespace RuneArena.Strokes
{
    // Ideal rune paths. Closed shapes get one variant per starting vertex and direction,
    // since the recognizer compares points in drawing order.
    public static class RuneShapes
    {
        public static List<KeyValuePair<string, List<Vector2>>> CreateVariants()
        {
            var variants = new List<KeyValuePair<string, List<Vector2>>>();

            AddClosed(variants, "square", new[]
            {
                new Vector2(-1f, 1f), new Vector2(1f, 1f), new Vector2(1f, -1f), new Vector2(-1f, -1f)
            }, 1);

            AddClosed(variants, "triangle", new[]
            {
                new Vector2(0f, 1f), new Vector2(0.866f, -0.5f), new Vector2(-0.866f, -0.5f)
            }, 1);

            AddClosed(variants, "circle", Circle(32), 4);

            AddOpen(variants, "lightning", new[]
            {
                new Vector2(-1f, 1f), new Vector2(1f, 1f), new Vector2(-1f, -1f), new Vector2(1f, -1f)
            });

            return variants;
        }

        static void AddClosed(List<KeyValuePair<string, List<Vector2>>> variants, string label, Vector2[] vertices, int startStride)
        {
            int n = vertices.Length;
            for (int start = 0; start < n; start += startStride)
            {
                for (int dir = -1; dir <= 1; dir += 2)
                {
                    var path = new List<Vector2>(n + 1);
                    for (int k = 0; k <= n; k++)
                        path.Add(vertices[((start + dir * k) % n + n) % n]);
                    variants.Add(new KeyValuePair<string, List<Vector2>>(label, path));
                }
            }
        }

        static void AddOpen(List<KeyValuePair<string, List<Vector2>>> variants, string label, Vector2[] vertices)
        {
            var forward = new List<Vector2>(vertices);
            var backward = new List<Vector2>(vertices);
            backward.Reverse();
            variants.Add(new KeyValuePair<string, List<Vector2>>(label, forward));
            variants.Add(new KeyValuePair<string, List<Vector2>>(label, backward));
        }

        static Vector2[] Circle(int segments)
        {
            var vertices = new Vector2[segments];
            for (int i = 0; i < segments; i++)
            {
                float a = Mathf.PI * 0.5f - i * 2f * Mathf.PI / segments;
                vertices[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            }
            return vertices;
        }
    }
}
