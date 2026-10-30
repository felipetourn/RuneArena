using System.Collections;
using System.Collections.Generic;
using RuneArena.Effects;
using UnityEngine;

namespace RuneArena.Strokes
{
    // How a rune looks while it's drawn and after: sparks where the line is drawn, a glow around
    // the line, and once the rune is recognized the hand-drawn line straightens into the clean rune,
    // which pulses until it's launched. A rejected or dropped rune crumbles into ash.
    [RequireComponent(typeof(StrokeRecorder), typeof(RuneCaster))]
    public class RuneVisuals : MonoBehaviour
    {
        const int k_ShapePoints = 64;

        [Header("Glow")]
        [Tooltip("Width of the glow, relative to the line.")]
        [SerializeField] float m_GlowWidth = 4f;
        [SerializeField, Range(0f, 1f)] float m_GlowAlpha = 0.45f;
        [SerializeField] float m_PulseSpeed = 7f;
        [SerializeField, Range(0f, 1f)] float m_PulseAmount = 0.3f;

        [Header("Recognized")]
        [SerializeField] float m_MorphTime = 0.25f;

        [Header("Drawing")]
        [SerializeField] Color m_SparkColor = new Color(0.65f, 0.85f, 1f);
        [Tooltip("Sparks per meter the tip travels.")]
        [SerializeField] float m_SparksPerMeter = 90f;

        StrokeRecorder m_Recorder;
        RuneCaster m_Caster;
        LineRenderer m_Glow;
        MaterialPropertyBlock m_Block;
        ParticleSystem m_TipSparks;
        Vector3[] m_Positions = new Vector3[512];
        readonly List<Vector3> m_World = new List<Vector3>(512);
        bool m_Charged;
        Coroutine m_Morph;

        static List<KeyValuePair<string, List<Vector2>>> s_Shapes;

        void Awake()
        {
            m_Recorder = GetComponent<StrokeRecorder>();
            m_Caster = GetComponent<RuneCaster>();
            m_Block = new MaterialPropertyBlock();
        }

        void Start()
        {
            // The recorder makes its line in Awake; the glow goes right next to it, in the same space.
            LineRenderer line = m_Recorder.Line;
            FxLibrary fx = Fx.Library;
            if (line == null || fx == null || fx.Line == null)
                return;

            var go = new GameObject("StrokeGlow");
            go.transform.SetParent(line.transform, false);
            m_Glow = go.AddComponent<LineRenderer>();
            m_Glow.useWorldSpace = false;
            m_Glow.sharedMaterial = fx.Line;
            m_Glow.textureMode = LineTextureMode.Stretch;
            m_Glow.numCapVertices = 4;
            m_Glow.numCornerVertices = 2;
            m_Glow.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            m_Glow.receiveShadows = false;
            m_Glow.positionCount = 0;
        }

        void OnEnable()
        {
            m_Recorder.StrokeStarted += OnStrokeStarted;
            m_Caster.RuneCharged += OnRuneCharged;
            m_Caster.RuneDiscarded += OnRuneDiscarded;
            m_Caster.RuneLaunched += OnRuneLaunched;
        }

        void OnDisable()
        {
            m_Recorder.StrokeStarted -= OnStrokeStarted;
            m_Caster.RuneCharged -= OnRuneCharged;
            m_Caster.RuneDiscarded -= OnRuneDiscarded;
            m_Caster.RuneLaunched -= OnRuneLaunched;
            Fx.Release(m_TipSparks);
            m_TipSparks = null;
        }

        void LateUpdate()
        {
            if (m_TipSparks != null && !m_Recorder.IsDrawing)
            {
                Fx.Release(m_TipSparks);
                m_TipSparks = null;
            }
            // At the end of the line rather than on the wand, so mouse drawing in the editor sparks too.
            LineRenderer line = m_Recorder.Line;
            if (m_TipSparks != null && line.positionCount > 0)
                m_TipSparks.transform.position = line.transform.TransformPoint(line.GetPosition(line.positionCount - 1));
            UpdateGlow();
        }

        // The glow follows the line: same points, wider, the line's color, softly pulsing while charged.
        void UpdateGlow()
        {
            if (m_Glow == null)
                return;
            LineRenderer line = m_Recorder.Line;
            int count = line.positionCount;
            if (count == 0)
            {
                m_Glow.positionCount = 0;
                return;
            }

            if (m_Positions.Length < count)
                m_Positions = new Vector3[count];
            line.GetPositions(m_Positions);
            m_Glow.positionCount = count;
            m_Glow.SetPositions(m_Positions);

            float pulse = m_Charged ? 1f + m_PulseAmount * Mathf.Sin(Time.time * m_PulseSpeed) : 1f;
            m_Glow.widthMultiplier = line.widthMultiplier * m_GlowWidth * pulse;
            Color color = m_Recorder.LineColor;
            color.a = m_GlowAlpha;
            m_Glow.GetPropertyBlock(m_Block);
            m_Block.SetColor("_BaseColor", color);
            m_Glow.SetPropertyBlock(m_Block);
        }

        void OnStrokeStarted(StrokeRecorder recorder)
        {
            StopMorph();
            m_Charged = false;
            Fx.Release(m_TipSparks);
            FxLibrary fx = Fx.Library;
            if (fx == null)
                return;
            m_TipSparks = Fx.Attach(transform, new FxBurst
            {
                Material = fx.Glow,
                Color = m_SparkColor,
                Count = 64,
                Size = 0.012f,
                Speed = 0.15f,
                Lifetime = 0.5f,
                Gravity = 0.05f,
                Radius = 0.004f,
                Growth = FxGrowth.Shrink,
            }, 0f, m_SparksPerMeter);
        }

        void OnRuneCharged(string label)
        {
            m_Charged = true;
            StopMorph();
            m_Morph = StartCoroutine(Morph(label));
        }

        void OnRuneDiscarded()
        {
            m_Charged = false;
            StopMorph();
            FxLibrary fx = Fx.Library;
            if (fx == null)
                return;
            WorldPoints(4);
            Fx.Along(m_World, new FxBurst
            {
                Material = fx.Puff,
                Color = new Color(0.35f, 0.33f, 0.32f, 0.8f),
                Count = 1,
                Size = 0.03f,
                Speed = 0.08f,
                Lifetime = 1.2f,
                Gravity = 0.12f,
                Radius = 0.01f,
                Growth = FxGrowth.Swell,
            });
        }

        void OnRuneLaunched(string label, Vector3 center, Vector3 direction)
        {
            m_Charged = false;
            StopMorph();
            FxLibrary fx = Fx.Library;
            if (fx == null)
                return;
            Color color = m_Recorder.LineColor;
            Fx.Burst(center, new FxBurst
            {
                Material = fx.Glow,
                Color = color,
                Count = 1,
                Size = 0.5f,
                Lifetime = 0.25f,
                Growth = FxGrowth.Expand,
            });
            Fx.Burst(center, new FxBurst
            {
                Material = fx.Spark,
                Color = color,
                Count = 24,
                Size = 0.03f,
                Speed = 2.5f,
                Lifetime = 0.4f,
                Radius = 0.05f,
                Drag = 4f,
                Stretch = true,
                Growth = FxGrowth.Shrink,
            });
        }

        void StopMorph()
        {
            if (m_Morph == null)
                return;
            StopCoroutine(m_Morph);
            m_Morph = null;
        }

        // From the stroke as drawn to the clean rune, fitted over it: same place, same size, in the plane
        // the stroke was drawn in. The best of the rune's variants is the one that needs the least moving,
        // so the line doesn't turn inside out on the way.
        IEnumerator Morph(string label)
        {
            LineRenderer line = m_Recorder.Line;
            if (line.positionCount < 2)
                yield break;

            Quaternion head = m_Recorder.HeadRotation;
            Quaternion toHead = Quaternion.Inverse(head);
            int count = line.positionCount;
            if (m_Positions.Length < count)
                m_Positions = new Vector3[count];
            line.GetPositions(m_Positions);

            var flat = new List<Vector2>(count);
            float depth = 0f;
            for (int i = 0; i < count; i++)
            {
                Vector3 p = toHead * m_Positions[i];
                flat.Add(new Vector2(p.x, p.y));
                depth += p.z;
            }
            depth /= count;

            var drawn = new Vector2[k_ShapePoints];
            StrokePreprocessor.Resample(flat, drawn);
            Vector2[] target = Fit(label, drawn);
            if (target == null)
                yield break;

            var from = new Vector3[k_ShapePoints];
            var to = new Vector3[k_ShapePoints];
            for (int i = 0; i < k_ShapePoints; i++)
            {
                from[i] = head * new Vector3(drawn[i].x, drawn[i].y, depth);
                to[i] = head * new Vector3(target[i].x, target[i].y, depth);
            }

            var current = new Vector3[k_ShapePoints];
            line.positionCount = k_ShapePoints;
            for (float t = 0f; t < m_MorphTime; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / m_MorphTime);
                for (int i = 0; i < k_ShapePoints; i++)
                    current[i] = Vector3.LerpUnclamped(from[i], to[i], k);
                line.SetPositions(current);
                yield return null;
            }
            line.SetPositions(to);
            m_Morph = null;

            FxLibrary fx = Fx.Library;
            if (fx == null)
                yield break;
            WorldPoints(4);
            Fx.Along(m_World, new FxBurst
            {
                Material = fx.Glow,
                Color = m_Recorder.LineColor,
                Count = 1,
                Size = 0.025f,
                Speed = 0.12f,
                Lifetime = 0.6f,
                Radius = 0.005f,
                Growth = FxGrowth.Shrink,
            });
        }

        // The variant of the rune that best matches the drawn points, scaled and moved over them.
        static Vector2[] Fit(string label, Vector2[] drawn)
        {
            if (s_Shapes == null)
                s_Shapes = RuneShapes.CreateVariants();

            Bounds(drawn, out Vector2 center, out float size);
            Vector2[] best = null;
            float bestError = float.MaxValue;
            var shape = new Vector2[drawn.Length];
            foreach (var variant in s_Shapes)
            {
                if (variant.Key != label)
                    continue;
                StrokePreprocessor.Resample(variant.Value, shape);
                Bounds(shape, out Vector2 shapeCenter, out float shapeSize);
                float scale = shapeSize > 1e-6f ? size / shapeSize : 1f;

                float error = 0f;
                for (int i = 0; i < shape.Length; i++)
                    error += (center + (shape[i] - shapeCenter) * scale - drawn[i]).sqrMagnitude;
                if (error >= bestError)
                    continue;

                bestError = error;
                best = new Vector2[shape.Length];
                for (int i = 0; i < shape.Length; i++)
                    best[i] = center + (shape[i] - shapeCenter) * scale;
            }
            return best;
        }

        static void Bounds(Vector2[] points, out Vector2 center, out float size)
        {
            Vector2 min = points[0], max = points[0];
            foreach (Vector2 p in points)
            {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            center = (min + max) * 0.5f;
            size = Mathf.Max(max.x - min.x, max.y - min.y);
        }

        // Every step-th point of the line, in world space.
        void WorldPoints(int step)
        {
            m_World.Clear();
            LineRenderer line = m_Recorder.Line;
            int count = line.positionCount;
            if (m_Positions.Length < count)
                m_Positions = new Vector3[count];
            line.GetPositions(m_Positions);
            for (int i = 0; i < count; i += step)
                m_World.Add(line.transform.TransformPoint(m_Positions[i]));
        }
    }
}
