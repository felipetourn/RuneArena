using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RuneArena.Strokes
{
    // Recognized rune -> charged (colored, follows the player) -> launched with a push or the grip.
    // Pulling the trigger again throws the charged rune away and starts a new one.
    [RequireComponent(typeof(StrokeRecorder), typeof(RuneRecognizer))]
    public class RuneCaster : MonoBehaviour
    {
        [Serializable]
        struct RuneColor
        {
            public string label;
            public Color color;
        }

        [Tooltip("Grip or A on the right controller.")]
        [SerializeField] InputActionProperty m_LaunchAction = CreateLaunchAction();

        [Header("Aim")]
        [Tooltip("The rune flies from where it was drawn towards what the right controller points at.")]
        [SerializeField] float m_AimDistance = 50f;
        [SerializeField] LayerMask m_AimMask = ~0;

        [Header("Push to launch")]
        [Tooltip("Forward speed of the controller (m/s, relative to the player) that counts as a push.")]
        [SerializeField] float m_PushSpeed = 1.5f;
        [SerializeField] int m_PushFrames = 3;

        [Header("Charged rune")]
        [SerializeField] float m_ChargeTimeout = 4f;
        [SerializeField] float m_FadeTime = 0.3f;
        [SerializeField] Color m_RejectColor = new Color(1f, 0.2f, 0.2f);
        [SerializeField] RuneColor[] m_Colors =
        {
            new RuneColor { label = "square", color = new Color(0.6f, 0.4f, 0.2f) },
            new RuneColor { label = "circle", color = new Color(1f, 0.45f, 0.1f) },
            new RuneColor { label = "lightning", color = new Color(1f, 0.95f, 0.3f) },
            new RuneColor { label = "triangle", color = new Color(0.2f, 0.6f, 1f) },
        };

        [Header("Projectile")]
        [SerializeField] float m_ProjectileSpeed = 8f;
        [SerializeField] float m_ProjectileLifetime = 3f;

        // Label, start position and direction, in world space.
        public event Action<string, Vector3, Vector3> RuneLaunched;

        public bool HasChargedRune => m_Charged != null && !m_Recorder.IsDrawing;

        StrokeRecorder m_Recorder;
        RuneRecognizer m_Recognizer;
        string m_Charged;
        float m_ChargedAt;
        Vector3 m_PrevTip;
        int m_PushCount;
        Coroutine m_Fade;

        void Awake()
        {
            m_Recorder = GetComponent<StrokeRecorder>();
            m_Recognizer = GetComponent<RuneRecognizer>();
        }

        void OnEnable()
        {
            m_Recognizer.RuneRecognized += OnRuneRecognized;
            m_Recorder.StrokeStarted += OnStrokeStarted;
            if (m_LaunchAction.reference == null)
                m_LaunchAction.action.Enable();
        }

        void OnDisable()
        {
            m_Recognizer.RuneRecognized -= OnRuneRecognized;
            m_Recorder.StrokeStarted -= OnStrokeStarted;
            if (m_LaunchAction.reference == null)
                m_LaunchAction.action.Disable();
        }

        void Update()
        {
            if (m_Charged == null || m_Recorder.IsDrawing)
                return;

            if (Time.time - m_ChargedAt > m_ChargeTimeout)
            {
                Discard(ColorFor(m_Charged));
                return;
            }

            Vector3 tip = TipInOrigin();
            Vector3 velocity = (tip - m_PrevTip) / Mathf.Max(Time.deltaTime, 1e-4f);
            m_PrevTip = tip;

            Vector3 forward = m_Recorder.Origin.InverseTransformDirection(m_Recorder.Tip.forward);
            m_PushCount = Vector3.Dot(velocity, forward) > m_PushSpeed ? m_PushCount + 1 : 0;

            if (m_PushCount >= m_PushFrames || m_LaunchAction.action.WasPressedThisFrame())
                Launch(new Ray(m_Recorder.Tip.position, m_Recorder.Tip.forward));
        }

        void OnRuneRecognized(string label, float score)
        {
            if (label == null)
            {
                Discard(m_RejectColor);
                return;
            }

            m_Charged = label;
            m_ChargedAt = Time.time;
            m_PushCount = 0;
            m_PrevTip = TipInOrigin();
            m_Recorder.SetLineColor(ColorFor(label));
        }

        void OnStrokeStarted(StrokeRecorder recorder)
        {
            if (m_Fade != null)
            {
                StopCoroutine(m_Fade);
                m_Fade = null;
            }
            m_Charged = null;
        }

        // Launches the charged rune towards whatever the ray hits. Desktop controls aim with the mouse.
        public void Launch(Ray aim)
        {
            if (!HasChargedRune)
                return;

            string label = m_Charged;
            m_Charged = null;

            LineRenderer source = m_Recorder.Line;
            var positions = new Vector3[source.positionCount];
            Vector3 center = Vector3.zero;
            for (int i = 0; i < positions.Length; i++)
            {
                positions[i] = source.transform.TransformPoint(source.GetPosition(i));
                center += positions[i];
            }
            center /= positions.Length;
            for (int i = 0; i < positions.Length; i++)
                positions[i] -= center;

            var go = new GameObject($"Rune ({label})");
            go.transform.position = center;

            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.widthMultiplier = source.widthMultiplier;
            line.sharedMaterial = source.sharedMaterial;
            line.numCapVertices = source.numCapVertices;
            line.numCornerVertices = source.numCornerVertices;
            line.shadowCastingMode = source.shadowCastingMode;
            line.receiveShadows = false;
            line.positionCount = positions.Length;
            line.SetPositions(positions);

            var block = new MaterialPropertyBlock();
            source.GetPropertyBlock(block);
            line.SetPropertyBlock(block);

            Vector3 direction = (AimPoint(aim) - center).normalized;
            go.AddComponent<RuneProjectile>().Launch(direction * m_ProjectileSpeed, m_ProjectileLifetime);
            m_Recorder.ClearLine();

            RuneLaunched?.Invoke(label, center, direction);
        }

        void Discard(Color color)
        {
            m_Charged = null;
            m_Recorder.SetLineColor(color);
            m_Fade = StartCoroutine(FadeOut());
        }

        IEnumerator FadeOut()
        {
            LineRenderer line = m_Recorder.Line;
            float startWidth = line.widthMultiplier;
            for (float t = 0f; t < m_FadeTime; t += Time.deltaTime)
            {
                line.widthMultiplier = Mathf.Lerp(startWidth, 0f, t / m_FadeTime);
                yield return null;
            }
            m_Recorder.ClearLine();
            m_Fade = null;
        }

        Vector3 AimPoint(Ray aim)
        {
            if (Physics.Raycast(aim, out RaycastHit hit, m_AimDistance, m_AimMask, QueryTriggerInteraction.Ignore))
                return hit.point;
            return aim.GetPoint(m_AimDistance);
        }

        static InputActionProperty CreateLaunchAction()
        {
            var action = new InputAction("Launch", InputActionType.Button);
            action.AddBinding("<XRController>{RightHand}/{GripButton}");
            action.AddBinding("<XRController>{RightHand}/{PrimaryButton}");
            return new InputActionProperty(action);
        }

        Vector3 TipInOrigin()
        {
            return m_Recorder.Origin.InverseTransformPoint(m_Recorder.Tip.position);
        }

        Color ColorFor(string label)
        {
            foreach (RuneColor entry in m_Colors)
            {
                if (entry.label == label)
                    return entry.color;
            }
            return Color.white;
        }
    }
}
