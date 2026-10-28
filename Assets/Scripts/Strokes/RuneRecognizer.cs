using System;
using System.Collections.Generic;
using System.Diagnostics;
using TMPro;
using UnityEngine;

namespace RuneArena.Strokes
{
    [RequireComponent(typeof(StrokeRecorder))]
    public class RuneRecognizer : MonoBehaviour
    {
        [SerializeField] float m_MaxRotationDegrees = 30f;
        [SerializeField, Range(0f, 1f)] float m_MinScore = 0.85f;
        [SerializeField] TMP_Text m_ResultText;

        // Label is null when the stroke didn't match any rune well enough.
        public event Action<string, float> RuneRecognized;

        readonly List<Vector2> m_Projected = new List<Vector2>(512);
        readonly Vector2[] m_Resampled = new Vector2[StrokePreprocessor.k_SampleCount];

        StrokeRecorder m_Recorder;
        ProtractorRecognizer m_Protractor;

        void Awake()
        {
            m_Recorder = GetComponent<StrokeRecorder>();
            m_Protractor = new ProtractorRecognizer(m_MaxRotationDegrees);
            foreach (var variant in RuneShapes.CreateVariants())
                m_Protractor.AddTemplate(variant.Key, variant.Value);
        }

        void OnEnable()
        {
            m_Recorder.StrokeCompleted += OnStrokeCompleted;
        }

        void OnDisable()
        {
            m_Recorder.StrokeCompleted -= OnStrokeCompleted;
        }

        void OnStrokeCompleted(StrokeRecorder recorder)
        {
            long start = Stopwatch.GetTimestamp();

            StrokePreprocessor.Project(recorder.Points, recorder.HeadRotation, m_Projected);
            StrokePreprocessor.Resample(m_Projected, m_Resampled);
            StrokePreprocessor.Normalize(m_Resampled);
            string best = m_Protractor.Recognize(m_Resampled, out float score);

            double ms = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
            string label = score >= m_MinScore ? best : null;

            if (m_ResultText != null)
                m_ResultText.text = label != null
                    ? $"{label}  {score:0.00}  ({ms:0.00} ms)"
                    : $"? ({best} {score:0.00})  ({ms:0.00} ms)";

            RuneRecognized?.Invoke(label, score);
        }
    }
}
