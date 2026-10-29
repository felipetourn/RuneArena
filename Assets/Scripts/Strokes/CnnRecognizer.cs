using System;
using Unity.InferenceEngine;
using UnityEngine;

namespace RuneArena.Strokes
{
    // Runs the CNN trained by ml/train.py. Labels in the same order as ml/runes/__init__.py.
    public class CnnRecognizer : IDisposable
    {
        public static string[] Labels => RuneLabels.All;

        readonly Worker m_Worker;
        readonly TensorShape m_Shape = new TensorShape(1, 1, StrokePreprocessor.k_RasterSize, StrokePreprocessor.k_RasterSize);
        readonly float[] m_Image = new float[StrokePreprocessor.k_RasterSize * StrokePreprocessor.k_RasterSize];

        public CnnRecognizer(ModelAsset modelAsset)
        {
            m_Worker = new Worker(ModelLoader.Load(modelAsset), BackendType.CPU);
        }

        // Expects the output of StrokePreprocessor.Normalize. Confidence is the softmax probability.
        public string Recognize(Vector2[] normalized, out float confidence)
        {
            StrokePreprocessor.Rasterize(normalized, m_Image);

            using var input = new Tensor<float>(m_Shape, m_Image);
            m_Worker.Schedule(input);
            float[] logits = ((Tensor<float>)m_Worker.PeekOutput()).DownloadToArray();

            int best = 0;
            for (int i = 1; i < logits.Length; i++)
            {
                if (logits[i] > logits[best])
                    best = i;
            }

            float sum = 0f;
            for (int i = 0; i < logits.Length; i++)
                sum += Mathf.Exp(logits[i] - logits[best]);

            confidence = 1f / sum;
            return Labels[best];
        }

        public void Dispose()
        {
            m_Worker.Dispose();
        }
    }
}
