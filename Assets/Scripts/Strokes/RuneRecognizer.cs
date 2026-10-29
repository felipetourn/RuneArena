using System;
using System.Collections.Generic;
using System.Diagnostics;
using RuneArena.Player;
using TMPro;
using Unity.InferenceEngine;
using UnityEngine;

namespace RuneArena.Strokes
{
    [RequireComponent(typeof(StrokeRecorder))]
    public class RuneRecognizer : MonoBehaviour
    {
        [Header("Protractor")]
        [SerializeField] float m_MaxRotationDegrees = 30f;
        [SerializeField, Range(0f, 1f)] float m_MinScore = 0.85f;

        [Header("CNN")]
        [SerializeField] ModelAsset m_Model;
        [SerializeField, Range(0f, 1f)] float m_MinConfidence = 0.8f;
        [Tooltip("Which recognizer decides the rune. Both run so they can be compared.")]
        [SerializeField] bool m_UseCnn = true;

        [SerializeField] TMP_Text m_ResultText;

        // Label is null when the stroke didn't match any rune well enough.
        public event Action<string, float> RuneRecognized;

        readonly List<Vector2> m_Projected = new List<Vector2>(512);
        readonly Vector2[] m_Resampled = new Vector2[StrokePreprocessor.k_SampleCount];

        StrokeRecorder m_Recorder;
        ProtractorRecognizer m_Protractor;
        CnnRecognizer m_Cnn;

        void Awake()
        {
            m_Recorder = GetComponent<StrokeRecorder>();
            m_Protractor = new ProtractorRecognizer(m_MaxRotationDegrees);
            foreach (var variant in RuneShapes.CreateVariants())
                m_Protractor.AddTemplate(variant.Key, variant.Value);

            if (m_Model != null)
                m_Cnn = new CnnRecognizer(m_Model);
        }

        void OnDestroy()
        {
            m_Cnn?.Dispose();
        }

        bool ShowResult => m_Recorder.IsSaving || DebugView.Enabled;

        void Start()
        {
            // Hidden until a recording session needs it, instead of showing its placeholder text.
            if (m_ResultText != null)
                m_ResultText.gameObject.SetActive(ShowResult);
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
            double prepMs = Elapsed(start);

            start = Stopwatch.GetTimestamp();
            string pBest = m_Protractor.Recognize(m_Resampled, out float pScore);
            string pLabel = pScore >= m_MinScore ? pBest : null;
            double pMs = prepMs + Elapsed(start);

            string cBest = null;
            string cLabel = null;
            float cScore = 0f;
            double cMs = 0;
            if (m_Cnn != null)
            {
                start = Stopwatch.GetTimestamp();
                cBest = m_Cnn.Recognize(m_Resampled, out cScore);
                cLabel = cScore >= m_MinConfidence && cBest != "noise" ? cBest : null;
                cMs = prepMs + Elapsed(start);
            }

            bool useCnn = m_UseCnn && m_Cnn != null;
            // Recognition details are for recording sessions; while playing they're just clutter.
            if (m_ResultText != null)
                m_ResultText.gameObject.SetActive(ShowResult);
            if (m_ResultText != null && ShowResult)
            {
                string text = Line(!useCnn, "P", pLabel, pBest, pScore, pMs);
                if (m_Cnn != null)
                    text += "\n" + Line(useCnn, "C", cLabel, cBest, cScore, cMs);
                m_ResultText.text = text;
            }

            if (useCnn)
                RuneRecognized?.Invoke(cLabel, cScore);
            else
                RuneRecognized?.Invoke(pLabel, pScore);
        }

        static string Line(bool active, string name, string label, string best, float score, double ms)
        {
            string result = label ?? $"? ({best})";
            return $"{(active ? ">" : " ")}{name}: {result}  {score:0.00}  ({ms:0.00} ms)";
        }

        static double Elapsed(long start)
        {
            return (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
        }
    }
}
