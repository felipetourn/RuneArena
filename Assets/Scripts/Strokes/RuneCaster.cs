using System;
using System.Collections;
using RuneArena.Player;
using RuneArena.Spells;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RuneArena.Strokes
{
    // Recognized rune -> charged (colored, follows the player) -> launched with a push or the grip.
    // Pulling the trigger again throws the charged rune away and starts a new one.
    // Each rune casts the RuneSpell with the same label found on this object.
    [RequireComponent(typeof(StrokeRecorder), typeof(RuneRecognizer))]
    public class RuneCaster : MonoBehaviour
    {
        [Tooltip("Grip or A on the right controller.")]
        [SerializeField] InputActionProperty m_LaunchAction = CreateLaunchAction();

        [Header("Push to launch")]
        [Tooltip("Forward speed of the controller (m/s, relative to the player) that counts as a push.")]
        [SerializeField] float m_PushSpeed = 1.5f;
        [SerializeField] int m_PushFrames = 3;

        [Header("Charged rune")]
        [SerializeField] float m_ChargeTimeout = 4f;
        [SerializeField] float m_FadeTime = 0.3f;
        [SerializeField] Color m_RejectColor = new Color(1f, 0.2f, 0.2f);
        [Tooltip("Color of a recognized rune there isn't enough mana for, or that isn't unlocked yet.")]
        [SerializeField] Color m_NoManaColor = new Color(0.5f, 0.5f, 0.5f);

        // Label, start position and direction, in world space.
        public event Action<string, Vector3, Vector3> RuneLaunched;
        // A recognized rune is ready to launch.
        public event Action<string> RuneCharged;
        // The rune was rejected, couldn't be cast, or was held too long. Fired before its line fades.
        public event Action RuneDiscarded;

        public bool HasChargedRune => m_Charged != null && !m_Recorder.IsDrawing;

        /// <summary>Practice: every rune casts, locked or not, and costs no mana.</summary>
        public bool FreeCasting { get; set; }

        StrokeRecorder m_Recorder;
        RuneRecognizer m_Recognizer;
        RuneSpell[] m_Spells;
        SpellBook m_Book;
        PlayerHealth m_Player;
        PlayerMana m_Mana;
        string m_Charged;
        float m_ChargedAt;
        Vector3 m_PrevTip;
        int m_PushCount;
        Coroutine m_Fade;

        void Awake()
        {
            m_Recorder = GetComponent<StrokeRecorder>();
            m_Recognizer = GetComponent<RuneRecognizer>();
            m_Spells = GetComponents<RuneSpell>();
            m_Book = GetComponent<SpellBook>();
        }

        void Start()
        {
            m_Player = FindAnyObjectByType<PlayerHealth>();
            m_Mana = FindAnyObjectByType<PlayerMana>();
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
            m_Charged = null;
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

            RuneSpell spell = SpellFor(label);
            bool locked = m_Book != null && !m_Book.IsUnlocked(label);
            if (spell != null && !FreeCasting && (locked || (m_Mana != null && !m_Mana.CanCast(CostOf(spell)))))
            {
                Discard(m_NoManaColor);
                return;
            }

            m_Charged = label;
            m_ChargedAt = Time.time;
            m_PushCount = 0;
            m_PrevTip = TipInOrigin();
            m_Recorder.SetLineColor(ColorFor(label));
            RuneCharged?.Invoke(label);
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

            LineRenderer line = m_Recorder.Line;
            Vector3 center = Vector3.zero;
            for (int i = 0; i < line.positionCount; i++)
                center += line.transform.TransformPoint(line.GetPosition(i));
            center /= Mathf.Max(line.positionCount, 1);
            m_Recorder.ClearLine();

            RuneSpell spell = SpellFor(label);
            if (spell != null)
            {
                if (m_Mana != null && !FreeCasting)
                    m_Mana.Spend(CostOf(spell));
                float power = m_Book != null ? m_Book.Power(label) : 1f;
                spell.Cast(new SpellCast(center, aim, m_Player, m_Recorder.LineMaterial, power));
            }
            else
                Debug.LogWarning($"RuneCaster: no RuneSpell for '{label}' on {name}", this);

            RuneLaunched?.Invoke(label, center, aim.direction);
        }

        void Discard(Color color)
        {
            m_Charged = null;
            m_Recorder.SetLineColor(color);
            RuneDiscarded?.Invoke();
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

        float CostOf(RuneSpell spell)
        {
            return m_Book != null ? m_Book.Cost(spell) : spell.ManaCost;
        }

        RuneSpell SpellFor(string label)
        {
            foreach (RuneSpell spell in m_Spells)
            {
                if (spell.Label == label)
                    return spell;
            }
            return null;
        }

        Color ColorFor(string label)
        {
            RuneSpell spell = SpellFor(label);
            return spell != null ? spell.Color : Color.white;
        }
    }
}
