using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace RuneArena.Strokes.Editor
{
    // Runs the C# pipeline over ml/data/mouse so ml/parity.py can check it matches Python.
    static class ParityExport
    {
        static readonly Regex k_Number = new Regex(@"-?\d+(?:\.\d+)?(?:[eE][-+]?\d+)?");

        [MenuItem("RuneArena/Export Parity Data")]
        static void Export()
        {
            string data = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ml", "data"));
            string input = Path.Combine(data, "mouse");
            string output = Path.Combine(data, "parity_cs.txt");

            var protractor = new ProtractorRecognizer(30f);
            foreach (var variant in RuneShapes.CreateVariants())
                protractor.AddTemplate(variant.Key, variant.Value);

            var projected = new List<Vector2>(512);
            var resampled = new Vector2[StrokePreprocessor.k_SampleCount];
            var image = new float[StrokePreprocessor.k_RasterSize * StrokePreprocessor.k_RasterSize];
            var sb = new StringBuilder();
            int count = 0;

            foreach (string file in Directory.GetFiles(input, "*.json", SearchOption.AllDirectories))
            {
                string json = File.ReadAllText(file);
                float[] head = Numbers(json, "\"headRotation\":[", "]");
                float[] raw = Numbers(json, "\"points\":[", "]}");

                var points = new List<Vector4>(raw.Length / 4);
                for (int i = 0; i + 3 < raw.Length; i += 4)
                    points.Add(new Vector4(raw[i], raw[i + 1], raw[i + 2], raw[i + 3]));

                StrokePreprocessor.Project(points, new Quaternion(head[0], head[1], head[2], head[3]), projected);
                StrokePreprocessor.Resample(projected, resampled);
                StrokePreprocessor.Normalize(resampled);
                StrokePreprocessor.Rasterize(resampled, image);
                string label = protractor.Recognize(resampled, out float score);

                sb.Append(Path.GetRelativePath(input, file).Replace('\\', '/'))
                  .Append('\t').Append(label)
                  .Append('\t').Append(score.ToString("R", CultureInfo.InvariantCulture));
                foreach (float v in image)
                    sb.Append('\t').Append(v.ToString("R", CultureInfo.InvariantCulture));
                sb.Append('\n');
                count++;
            }

            File.WriteAllText(output, sb.ToString());
            Debug.Log($"Parity data for {count} strokes written to {output}");
        }

        static float[] Numbers(string json, string from, string to)
        {
            int start = json.IndexOf(from) + from.Length;
            string section = json.Substring(start, json.IndexOf(to, start) - start);
            MatchCollection matches = k_Number.Matches(section);
            var values = new float[matches.Count];
            for (int i = 0; i < values.Length; i++)
                values[i] = float.Parse(matches[i].Value, CultureInfo.InvariantCulture);
            return values;
        }
    }
}
