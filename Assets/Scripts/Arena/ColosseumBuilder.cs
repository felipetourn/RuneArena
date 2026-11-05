using System;
using UnityEngine;

namespace RuneArena.Arena
{
    // Parameters for the generated colosseum. Build it from the button in the Inspector.
    // Only the arena wall, the gates, the pillars and the floor take part in the game; everything
    // behind the wall (seats, arcade, banners) is scenery without colliders.
    public class ColosseumBuilder : MonoBehaviour
    {
        public enum PillarStyle
        {
            Column,
            Broken,
            Brazier,
            Stele,
            Blocks,
        }

        [Serializable]
        public struct StandTier
        {
            public int Steps;
            [Tooltip("Depth of each row of seats.")]
            public float Depth;
            [Tooltip("Height of each row of seats.")]
            public float Rise;
        }

        [Header("Wall")]
        [Tooltip("Outer radius in meters.")]
        [SerializeField] float m_Radius = 15f;
        [SerializeField] float m_Thickness = 1f;
        [SerializeField] float m_Height = 5f;
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

        [Header("Pillars")]
        [SerializeField] int m_PillarCount = 4;
        [Tooltip("Distance from the center of the arena.")]
        [SerializeField] float m_PillarDistance = 9f;
        [Tooltip("Angle of the first pillar. 45 puts four pillars between four doors.")]
        [SerializeField] float m_PillarAngle = 45f;
        [Tooltip("Diameter of the shaft. Base and cap are a bit wider.")]
        [SerializeField] float m_PillarWidth = 1.2f;
        [SerializeField] float m_PillarHeight = 4f;
        [Tooltip("Look of each pillar, in order; repeats if there are more pillars than styles.")]
        [SerializeField] PillarStyle[] m_PillarStyles = { PillarStyle.Broken, PillarStyle.Brazier, PillarStyle.Stele, PillarStyle.Blocks };

        [Header("Stands")]
        [SerializeField] bool m_BuildStands = true;
        [Tooltip("Flat walk between the top of the arena wall and the first row of seats.")]
        [SerializeField] float m_TerraceWidth = 2.5f;
        [Tooltip("Groups of seats from the bottom up, each one behind a wall with entrances.")]
        [SerializeField] StandTier[] m_Tiers =
        {
            new StandTier { Steps = 8, Depth = 0.8f, Rise = 0.45f },
            new StandTier { Steps = 10, Depth = 0.8f, Rise = 0.5f },
            new StandTier { Steps = 8, Depth = 0.75f, Rise = 0.55f },
        };
        [Tooltip("Walkway in front of the wall that separates two groups of seats.")]
        [SerializeField] float m_WalkwayWidth = 1.6f;
        [SerializeField] float m_TierWallHeight = 2.4f;
        [Tooltip("Entrances in each of those walls.")]
        [SerializeField] int m_Entrances = 16;

        [Header("Arcade")]
        [Tooltip("Open arches around the top, behind the last row of seats.")]
        [SerializeField] int m_ArcadeArches = 80;
        [SerializeField] float m_ArcadeHeight = 5.5f;
        [Tooltip("Plain wall above the arches, with small windows.")]
        [SerializeField] float m_AtticHeight = 5f;
        [SerializeField] float m_MastHeight = 6f;

        [Header("Decoration")]
        [Tooltip("Banners hanging on the arena wall between two doors.")]
        [SerializeField] int m_BannersPerSection = 3;

        [Header("Materials")]
        [Tooltip("Arena wall, tier walls and arcade (travertine).")]
        [SerializeField] Material m_WallMaterial;
        [SerializeField] Material m_GateMaterial;
        [Tooltip("Cornices, parapet, door frames and columns (marble).")]
        [SerializeField] Material m_TrimMaterial;
        [Tooltip("Plinth and the floor outside the doors (granite).")]
        [SerializeField] Material m_PlinthMaterial;
        [SerializeField] Material m_SeatMaterial;
        [SerializeField] Material m_FloorMaterial;
        [SerializeField] Material m_BannerMaterial;
        [Tooltip("Inside of the entrances.")]
        [SerializeField] Material m_DarkMaterial;
        [SerializeField] Material m_WoodMaterial;
        [Tooltip("Far part of the tunnels behind the doors (darkened travertine).")]
        [SerializeField] Material m_TunnelMaterial;
        [SerializeField] Material m_BronzeMaterial;
        [Tooltip("Glowing parts of the pillars. No style uses it right now.")]
        [SerializeField] Material m_GlowMaterial;

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
        public int PillarCount => m_PillarCount;
        public float PillarDistance => m_PillarDistance;
        public float PillarAngle => m_PillarAngle;
        public float PillarWidth => m_PillarWidth;
        public float PillarHeight => m_PillarHeight;
        public PillarStyle[] PillarStyles => m_PillarStyles;
        public bool BuildStands => m_BuildStands;
        public float TerraceWidth => m_TerraceWidth;
        public StandTier[] Tiers => m_Tiers;
        public float WalkwayWidth => m_WalkwayWidth;
        public float TierWallHeight => m_TierWallHeight;
        public int Entrances => m_Entrances;
        public int ArcadeArches => m_ArcadeArches;
        public float ArcadeHeight => m_ArcadeHeight;
        public float AtticHeight => m_AtticHeight;
        public float MastHeight => m_MastHeight;
        public int BannersPerSection => m_BannersPerSection;
        public Material WallMaterial { get => m_WallMaterial; set => m_WallMaterial = value; }
        public Material GateMaterial { get => m_GateMaterial; set => m_GateMaterial = value; }
        public Material TrimMaterial { get => m_TrimMaterial; set => m_TrimMaterial = value; }
        public Material PlinthMaterial { get => m_PlinthMaterial; set => m_PlinthMaterial = value; }
        public Material SeatMaterial { get => m_SeatMaterial; set => m_SeatMaterial = value; }
        public Material FloorMaterial { get => m_FloorMaterial; set => m_FloorMaterial = value; }
        public Material BannerMaterial { get => m_BannerMaterial; set => m_BannerMaterial = value; }
        public Material DarkMaterial { get => m_DarkMaterial; set => m_DarkMaterial = value; }
        public Material WoodMaterial { get => m_WoodMaterial; set => m_WoodMaterial = value; }
        public Material TunnelMaterial { get => m_TunnelMaterial; set => m_TunnelMaterial = value; }
        public Material BronzeMaterial { get => m_BronzeMaterial; set => m_BronzeMaterial = value; }
        public Material GlowMaterial { get => m_GlowMaterial; set => m_GlowMaterial = value; }

        // Radius of the inside face of the arena wall.
        public float InnerRadius => m_Radius - m_Thickness;

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

            m_PillarCount = Mathf.Max(m_PillarCount, 0);
            m_PillarWidth = Mathf.Max(m_PillarWidth, 0.3f);
            m_PillarHeight = Mathf.Max(m_PillarHeight, 1f);
            m_PillarDistance = Mathf.Clamp(m_PillarDistance, 0f, m_Radius - m_Thickness - m_PillarWidth);

            m_TerraceWidth = Mathf.Max(m_TerraceWidth, 0.5f);
            m_WalkwayWidth = Mathf.Max(m_WalkwayWidth, 0.5f);
            m_TierWallHeight = Mathf.Max(m_TierWallHeight, 1f);
            m_Entrances = Mathf.Max(m_Entrances, 0);
            m_ArcadeArches = Mathf.Max(m_ArcadeArches, 8);
            m_ArcadeHeight = Mathf.Max(m_ArcadeHeight, 2f);
            m_AtticHeight = Mathf.Max(m_AtticHeight, 1f);
            m_BannersPerSection = Mathf.Max(m_BannersPerSection, 0);
            if (m_Tiers != null)
            {
                for (int i = 0; i < m_Tiers.Length; i++)
                {
                    m_Tiers[i].Steps = Mathf.Max(m_Tiers[i].Steps, 1);
                    m_Tiers[i].Depth = Mathf.Max(m_Tiers[i].Depth, 0.3f);
                    m_Tiers[i].Rise = Mathf.Max(m_Tiers[i].Rise, 0.1f);
                }
            }
        }
    }
}
