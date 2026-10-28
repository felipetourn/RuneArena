using UnityEngine;

namespace RuneArena.Arena
{
    // Parameters for the generated colosseum. Build it from the button in the Inspector.
    public class ColosseumBuilder : MonoBehaviour
    {
        [Header("Wall")]
        [Tooltip("Outer radius in meters.")]
        [SerializeField] float m_Radius = 15f;
        [SerializeField] float m_Thickness = 1f;
        [SerializeField] float m_Height = 4f;
        [Tooltip("Segments around the full circle.")]
        [SerializeField] int m_Segments = 128;

        [Header("Doors")]
        [SerializeField] int m_DoorCount = 4;
        [Tooltip("Width of the opening, measured along the middle of the wall.")]
        [SerializeField] float m_DoorWidth = 3f;
        [Tooltip("Height at the top of the arch.")]
        [SerializeField] float m_DoorHeight = 3.5f;
        [Tooltip("Angle of the first door. 0 is straight ahead (+Z).")]
        [SerializeField] float m_DoorAngle = 0f;
        [SerializeField] int m_ArchSegments = 16;
        [Tooltip("Depth of the floor outside each door, where enemies spawn.")]
        [SerializeField] float m_OutsideDepth = 3f;

        [Header("Materials")]
        [SerializeField] Material m_WallMaterial;
        [SerializeField] Material m_GateMaterial;

        public float Radius => m_Radius;
        public float Thickness => m_Thickness;
        public float Height => m_Height;
        public int Segments => m_Segments;
        public int DoorCount => m_DoorCount;
        public float DoorWidth => m_DoorWidth;
        public float DoorHeight => m_DoorHeight;
        public float DoorAngle => m_DoorAngle;
        public int ArchSegments => m_ArchSegments;
        public float OutsideDepth => m_OutsideDepth;
        public Material WallMaterial { get => m_WallMaterial; set => m_WallMaterial = value; }
        public Material GateMaterial { get => m_GateMaterial; set => m_GateMaterial = value; }

        void OnValidate()
        {
            m_Thickness = Mathf.Clamp(m_Thickness, 0.1f, m_Radius - 1f);
            m_Height = Mathf.Max(m_Height, 0.5f);
            m_Segments = Mathf.Max(m_Segments, 16);
            m_DoorCount = Mathf.Max(m_DoorCount, 0);
            m_ArchSegments = Mathf.Max(m_ArchSegments, 2);
            m_OutsideDepth = Mathf.Max(m_OutsideDepth, 0f);

            if (m_DoorCount > 0)
            {
                float section = 2f * Mathf.PI * (m_Radius - m_Thickness * 0.5f) / m_DoorCount;
                m_DoorWidth = Mathf.Clamp(m_DoorWidth, 0.5f, section - 0.5f);
            }
            m_DoorHeight = Mathf.Clamp(m_DoorHeight, m_DoorWidth * 0.5f, m_Height - 0.25f);
        }
    }
}
