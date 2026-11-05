using RuneArena.Strokes;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;

namespace RuneArena.Player
{
    // Full speed going forward (where the head faces), slower sideways and backwards, and slower still
    // while drawing a rune. Speed changes are smoothed: sudden changes are what makes people sick in VR.
    public class PlayerMovement : MonoBehaviour
    {
        [SerializeField] ContinuousMoveProvider m_MoveProvider;
        [SerializeField] float m_ForwardSpeed = 3f;
        [SerializeField] float m_SideSpeed = 1.8f;
        [SerializeField] float m_BackSpeed = 1.8f;
        [Tooltip("Speed multiplier while the trigger is held drawing a rune.")]
        [SerializeField, Range(0f, 1f)] float m_DrawingMultiplier = 0.5f;
        [Tooltip("How fast the speed can change, in m/s per second.")]
        [SerializeField] float m_SpeedChange = 6f;

        StrokeRecorder m_Recorder;
        float m_Speed;
        float m_SpeedScale = 1f;
        float m_Slow;
        float m_SlowUntil;

        void Awake()
        {
            if (m_MoveProvider == null)
                m_MoveProvider = GetComponentInChildren<ContinuousMoveProvider>(true);
            m_Recorder = GetComponentInChildren<StrokeRecorder>(true);
            m_Speed = m_ForwardSpeed;
        }

        void Update()
        {
            if (m_MoveProvider == null)
                return;
            Vector2 input = m_MoveProvider.leftHandMoveInput.ReadValue() + m_MoveProvider.rightHandMoveInput.ReadValue();
            m_MoveProvider.moveSpeed = UpdateSpeed(input, Time.deltaTime);
        }

        // Upgrades stack additively.
        public void AddSpeed(float fraction)
        {
            m_SpeedScale += fraction;
        }

        /// <summary>Slows the player down by fraction (0.4 = 40% slower) for some seconds, like an ice hit.
        /// While one is on, a weaker one doesn't replace it, and the longer of the two lasts.</summary>
        public void Slow(float fraction, float seconds)
        {
            bool active = Time.time < m_SlowUntil;
            m_Slow = active ? Mathf.Max(m_Slow, fraction) : fraction;
            m_SlowUntil = Mathf.Max(active ? m_SlowUntil : 0f, Time.time + seconds);
        }

        public bool IsSlowed => Time.time < m_SlowUntil;

        // input is a stick-style vector: y forward, x sideways. Returns the speed to move at this frame.
        public float UpdateSpeed(Vector2 input, float deltaTime)
        {
            if (input.sqrMagnitude > 0.01f)
                m_Speed = Mathf.MoveTowards(m_Speed, TargetSpeed(input.normalized), m_SpeedChange * deltaTime);
            return m_Speed;
        }

        float TargetSpeed(Vector2 direction)
        {
            float speed = direction.y >= 0f
                ? Mathf.Lerp(m_SideSpeed, m_ForwardSpeed, direction.y)
                : Mathf.Lerp(m_SideSpeed, m_BackSpeed, -direction.y);
            if (m_Recorder != null && m_Recorder.IsDrawing)
                speed *= m_DrawingMultiplier;
            if (IsSlowed)
                speed *= 1f - m_Slow;
            return speed * m_SpeedScale;
        }
    }
}
