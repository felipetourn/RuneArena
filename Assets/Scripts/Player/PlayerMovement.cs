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

        void Awake()
        {
            if (m_MoveProvider == null)
                m_MoveProvider = GetComponentInChildren<ContinuousMoveProvider>();
            m_Recorder = GetComponentInChildren<StrokeRecorder>();
            m_Speed = m_ForwardSpeed;
        }

        void Update()
        {
            if (m_MoveProvider == null)
                return;
            Vector2 input = m_MoveProvider.leftHandMoveInput.ReadValue() + m_MoveProvider.rightHandMoveInput.ReadValue();
            m_MoveProvider.moveSpeed = UpdateSpeed(input, Time.deltaTime);
        }

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
            return speed;
        }
    }
}
