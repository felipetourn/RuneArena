using System.Collections.Generic;
using RuneArena.Strokes;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RuneArena.Player
{
    // Editor only: walk the player with WASD and launch with F, aiming with the mouse.
    // Takes WASD/QE away from the XR Interaction Simulator, which otherwise moves the selected device with them.
    public class DesktopControls : MonoBehaviour
    {
        [SerializeField] bool m_Active = true;
        [Tooltip("Used when there is no PlayerMovement; otherwise its speeds apply here too.")]
        [SerializeField] float m_MoveSpeed = 2.5f;
        [SerializeField] RuneCaster m_Caster;

        readonly List<InputAction> m_Disabled = new List<InputAction>();

        XROrigin m_Origin;
        CharacterController m_Controller;
        PlayerMovement m_Movement;
        float m_VerticalSpeed;
        float m_NextSearch;

        void Awake()
        {
            m_Origin = GetComponent<XROrigin>();
            m_Controller = GetComponent<CharacterController>();
            m_Movement = GetComponent<PlayerMovement>();
            if (m_Caster == null)
                m_Caster = GetComponentInChildren<RuneCaster>();
        }

        void OnDisable()
        {
            foreach (InputAction action in m_Disabled)
                action.Enable();
            m_Disabled.Clear();
        }

#if UNITY_EDITOR
        void Update()
        {
            if (!m_Active || Keyboard.current == null)
                return;

            // The simulator is created after the scene loads, so keep looking until its actions show up.
            if (m_Disabled.Count == 0 && Time.unscaledTime >= m_NextSearch)
            {
                DisableSimulatorTranslation();
                m_NextSearch = Time.unscaledTime + 0.5f;
            }

            Move(Time.deltaTime);

            if (Keyboard.current.fKey.wasPressedThisFrame && m_Caster != null && Mouse.current != null)
                m_Caster.Launch(m_Origin.Camera.ScreenPointToRay(Mouse.current.position.ReadValue()));
        }
#endif

        void Move(float deltaTime)
        {
            Keyboard k = Keyboard.current;
            var input = new Vector2(
                (k.dKey.isPressed ? 1f : 0f) - (k.aKey.isPressed ? 1f : 0f),
                (k.wKey.isPressed ? 1f : 0f) - (k.sKey.isPressed ? 1f : 0f));

            Transform head = m_Origin.Camera.transform;
            Vector3 forward = Vector3.ProjectOnPlane(head.forward, transform.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(head.right, transform.up).normalized;
            float speed = m_Movement != null ? m_Movement.UpdateSpeed(input, deltaTime) : m_MoveSpeed;
            Vector3 motion = Vector3.ClampMagnitude(forward * input.y + right * input.x, 1f) * speed;

            if (m_Controller == null)
            {
                transform.position += motion * deltaTime;
                return;
            }

            m_VerticalSpeed = m_Controller.isGrounded ? -1f : m_VerticalSpeed + Physics.gravity.y * deltaTime;
            m_Controller.Move((motion + Vector3.up * m_VerticalSpeed) * deltaTime);
        }

        void DisableSimulatorTranslation()
        {
            foreach (InputAction action in InputSystem.ListEnabledActions())
            {
                InputActionAsset asset = action.actionMap?.asset;
                if (asset != null && asset.name.Contains("Simulator") && action.name.EndsWith("Translate"))
                {
                    action.Disable();
                    m_Disabled.Add(action);
                }
            }
        }
    }
}
