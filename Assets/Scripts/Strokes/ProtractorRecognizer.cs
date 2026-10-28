using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuneArena.Strokes
{
    // Protractor (Li, 2010): $1-style template matching with a closed-form best rotation.
    public class ProtractorRecognizer
    {
        readonly List<string> m_Labels = new List<string>();
        readonly List<Vector2[]> m_Templates = new List<Vector2[]>();
        readonly Vector2[] m_Candidate = new Vector2[StrokePreprocessor.k_SampleCount];
        readonly float m_MaxRotation;

        public ProtractorRecognizer(float maxRotationDegrees)
        {
            m_MaxRotation = maxRotationDegrees * Mathf.Deg2Rad;
        }

        public void AddTemplate(string label, List<Vector2> path)
        {
            var points = new Vector2[StrokePreprocessor.k_SampleCount];
            StrokePreprocessor.Resample(path, points);
            StrokePreprocessor.Normalize(points);
            ToUnitVector(points);
            m_Labels.Add(label);
            m_Templates.Add(points);
        }

        // Expects the output of StrokePreprocessor.Normalize. Score is a cosine similarity in [-1, 1].
        public string Recognize(Vector2[] normalized, out float score)
        {
            Array.Copy(normalized, m_Candidate, m_Candidate.Length);
            ToUnitVector(m_Candidate);

            string best = null;
            score = -1f;
            for (int t = 0; t < m_Templates.Count; t++)
            {
                float s = Similarity(m_Templates[t], m_Candidate);
                if (s > score)
                {
                    score = s;
                    best = m_Labels[t];
                }
            }
            return best;
        }

        float Similarity(Vector2[] template, Vector2[] candidate)
        {
            float a = 0f;
            float b = 0f;
            for (int i = 0; i < template.Length; i++)
            {
                Vector2 t = template[i];
                Vector2 c = candidate[i];
                a += t.x * c.x + t.y * c.y;
                b += t.x * c.y - t.y * c.x;
            }

            float angle = Mathf.Clamp(Mathf.Atan2(b, a), -m_MaxRotation, m_MaxRotation);
            return a * Mathf.Cos(angle) + b * Mathf.Sin(angle);
        }

        static void ToUnitVector(Vector2[] points)
        {
            float sum = 0f;
            for (int i = 0; i < points.Length; i++)
                sum += points[i].sqrMagnitude;

            float inv = sum > 0f ? 1f / Mathf.Sqrt(sum) : 0f;
            for (int i = 0; i < points.Length; i++)
                points[i] *= inv;
        }
    }
}
