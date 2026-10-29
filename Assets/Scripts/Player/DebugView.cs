using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RuneArena.Player
{
    // Goes on the XR Origin. Toggles the debug texts (REC OFF, recognition results) with F1,
    // or clicking the left thumbstick. "REC" itself always shows while strokes are being saved,
    // so a recording session is never silent.
    public class DebugView : MonoBehaviour
    {
        [SerializeField] bool m_StartEnabled;
        [SerializeField] InputActionProperty m_ToggleAction = CreateToggleAction();

        static bool s_Enabled;

        public static bool Enabled
        {
            get => s_Enabled;
            set
            {
                if (s_Enabled == value)
                    return;
                s_Enabled = value;
                Changed?.Invoke(value);
            }
        }

        public static event Action<bool> Changed;

        void Awake()
        {
            Enabled = m_StartEnabled;
        }

        void OnEnable()
        {
            if (m_ToggleAction.reference == null)
                m_ToggleAction.action.Enable();
        }

        void OnDisable()
        {
            if (m_ToggleAction.reference == null)
                m_ToggleAction.action.Disable();
        }

        void Update()
        {
            if (m_ToggleAction.action.WasPressedThisFrame())
                Enabled = !Enabled;
        }

        static InputActionProperty CreateToggleAction()
        {
            var action = new InputAction("Toggle Debug", InputActionType.Button);
            action.AddBinding("<Keyboard>/f1");
            action.AddBinding("<XRController>{LeftHand}/{Primary2DAxisClick}");
            return new InputActionProperty(action);
        }
    }
}
