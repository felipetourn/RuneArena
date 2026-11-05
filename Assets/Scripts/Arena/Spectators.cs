using System.Collections.Generic;
using RuneArena.Player;
using UnityEngine;

namespace RuneArena.Arena
{
    // The crowd in the stands: billboards sitting on a share of the seats, picked at random. The Spectators
    // shader builds and moves them, turning each one to the player; this lays them out and tells the shader how
    // worked up they are. Split in slices around the arena, so the ones behind the player aren't drawn.
    // Made by RuneArena > Create Spectators, next to the colosseum: not under it, since building the colosseum
    // clears everything under it.
    public class Spectators : MonoBehaviour
    {
        static readonly int k_Viewer = Shader.PropertyToID("_CrowdViewer");
        static readonly int k_State = Shader.PropertyToID("_CrowdState");
        static readonly int k_Kill = Shader.PropertyToID("_CrowdKill");
        static readonly int k_Ola = Shader.PropertyToID("_CrowdOla");

        [SerializeField] ColosseumBuilder m_Colosseum;
        [SerializeField] Material m_Material;
        [Tooltip("Share of the seats with someone on them.")]
        [SerializeField, Range(0f, 1f)] float m_Fill = 0.5f;
        [Tooltip("Room each spectator takes along a row, in meters.")]
        [SerializeField] float m_SeatWidth = 0.6f;
        [Tooltip("Height of a spectator's sprite, arms up included, in meters. Each one gets a size in this range.")]
        [SerializeField] Vector2 m_Height = new Vector2(1.45f, 1.7f);
        [Tooltip("People in the texture, side by side.")]
        [SerializeField] int m_People = 8;
        [Tooltip("How many of those, the last ones, are legionaries.")]
        [SerializeField] int m_Soldiers = 2;
        [Tooltip("Share of the spectators that are legionaries.")]
        [SerializeField, Range(0f, 1f)] float m_SoldierShare = 0.1f;
        [Tooltip("The legionaries' tunic and crest.")]
        [SerializeField] Color m_SoldierColor = new Color(0.62f, 0.12f, 0.1f);
        [SerializeField] int m_Slices = 24;
        [SerializeField] int m_Seed = 7;
        [Tooltip("The civilians' tunics, picked at random and darkened or lightened a little: undyed wool and " +
                 "earth dyes, with the odd purple.")]
        [SerializeField] Color[] m_Clothes =
        {
            new Color(0.88f, 0.84f, 0.72f),
            new Color(0.8f, 0.72f, 0.55f),
            new Color(0.72f, 0.55f, 0.28f),
            new Color(0.6f, 0.3f, 0.2f),
            new Color(0.35f, 0.42f, 0.55f),
            new Color(0.45f, 0.48f, 0.3f),
            new Color(0.45f, 0.32f, 0.22f),
            new Color(0.42f, 0.22f, 0.38f),
        };

        [Header("Ola")]
        [Tooltip("Laps per second of the wave going round the stands when the game is beaten.")]
        [SerializeField] float m_OlaSpeed = 0.25f;
        [SerializeField] float m_OlaLaps = 2f;

        Transform m_Head;
        float m_Cheer;
        float m_CheerFade;

        void Start()
        {
            var player = FindAnyObjectByType<PlayerHealth>();
            m_Head = player != null ? player.Head : null;
            Shader.SetGlobalVector(k_Kill, new Vector4(0f, -100f, 0f, 0f));
            Shader.SetGlobalVector(k_Ola, new Vector4(-100f, 0f, 0f, 0f));
            Build();
        }

        void Update()
        {
            m_Cheer = Mathf.MoveTowards(m_Cheer, 0f, m_CheerFade * Time.deltaTime);
            Shader.SetGlobalVector(k_State, new Vector4(Time.time, m_Cheer, 0f, 0f));
            if (m_Head != null)
                Shader.SetGlobalVector(k_Viewer, m_Head.position);
        }

        // The whole crowd on its feet, as many as level (0 to 1), sitting back down over seconds.
        public void Cheer(float level, float seconds)
        {
            m_Cheer = Mathf.Max(m_Cheer, Mathf.Clamp01(level));
            m_CheerFade = m_Cheer / Mathf.Max(seconds, 0.1f);
        }

        // The part of the stands nearest to where an enemy went down jumps up for a moment.
        public void Kill(Vector3 position)
        {
            Shader.SetGlobalVector(k_Kill, new Vector4(Angle(transform.InverseTransformPoint(position)), Time.time, 0f, 0f));
        }

        // A wave going round the stands.
        public void Ola()
        {
            float speed = m_OlaSpeed * 2f * Mathf.PI;
            Shader.SetGlobalVector(k_Ola, new Vector4(Time.time, speed, m_OlaLaps / m_OlaSpeed, 0f));
        }

        static float Angle(Vector3 local)
        {
            float angle = Mathf.Atan2(local.x, local.z);
            return angle < 0f ? angle + 2f * Mathf.PI : angle;
        }

        void Build()
        {
            if (m_Colosseum == null || m_Material == null)
                return;
            // The seats are laid out in the colosseum's space.
            transform.SetPositionAndRotation(m_Colosseum.transform.position, m_Colosseum.transform.rotation);
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            var rows = new List<Vector3>();
            m_Colosseum.SeatRows(rows);
            var slices = new Slice[Mathf.Max(m_Slices, 1)];
            for (int i = 0; i < slices.Length; i++)
                slices[i] = new Slice();

            var random = new System.Random(m_Seed);
            float Range(float lo, float hi) => lo + (float)random.NextDouble() * (hi - lo);

            foreach (Vector3 row in rows)
            {
                // Toward the back of the seat, a little sunk into it so nobody floats.
                float radius = row.x + row.z * 0.55f;
                int seats = Mathf.FloorToInt(2f * Mathf.PI * radius / m_SeatWidth);
                for (int s = 0; s < seats; s++)
                {
                    if (random.NextDouble() > m_Fill)
                        continue;
                    float angle = (s + Range(-0.2f, 0.2f)) / seats * 2f * Mathf.PI;
                    angle = Mathf.Repeat(angle, 2f * Mathf.PI);
                    var seat = new Vector3(Mathf.Sin(angle) * radius, row.y - 0.05f, Mathf.Cos(angle) * radius);
                    int civilians = Mathf.Max(m_People - m_Soldiers, 1);
                    bool soldier = m_Soldiers > 0 && random.NextDouble() < m_SoldierShare;
                    int person = soldier ? civilians + random.Next(m_Soldiers) : random.Next(civilians);
                    Color clothes = (soldier ? m_SoldierColor : m_Clothes[random.Next(m_Clothes.Length)]) * Range(0.8f, 1.1f);
                    var data = new Vector4(angle, Range(m_Height.x, m_Height.y), Range(0f, 1f), Range(0f, 1f));
                    int slice = Mathf.Min((int)(angle / (2f * Mathf.PI) * slices.Length), slices.Length - 1);
                    slices[slice].Add(seat, person, Range(0f, 1f), data, clothes);
                }
            }

            for (int i = 0; i < slices.Length; i++)
            {
                if (slices[i].Count == 0)
                    continue;
                var go = new GameObject($"Spectators {i}");
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = slices[i].ToMesh($"Spectators {i}", m_Height.y);
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = m_Material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        class Slice
        {
            readonly List<Vector3> m_Seats = new List<Vector3>();
            readonly List<Vector4> m_Corners = new List<Vector4>();
            readonly List<Vector4> m_Data = new List<Vector4>();
            readonly List<Color> m_Colors = new List<Color>();
            readonly List<int> m_Triangles = new List<int>();

            public int Count => m_Seats.Count / 4;

            public void Add(Vector3 seat, int person, float seed, Vector4 data, Color clothes)
            {
                int first = m_Seats.Count;
                for (int c = 0; c < 4; c++)
                {
                    m_Seats.Add(seat);
                    m_Corners.Add(new Vector4(c == 1 || c == 2 ? 1f : 0f, c >= 2 ? 1f : 0f, person, seed));
                    m_Data.Add(data);
                    m_Colors.Add(clothes);
                }
                m_Triangles.Add(first);
                m_Triangles.Add(first + 2);
                m_Triangles.Add(first + 1);
                m_Triangles.Add(first);
                m_Triangles.Add(first + 3);
                m_Triangles.Add(first + 2);
            }

            // Every corner sits on its seat until the shader spreads it, so the bounds are grown by hand.
            public Mesh ToMesh(string name, float height)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(m_Seats);
                mesh.SetUVs(0, m_Corners);
                mesh.SetUVs(1, m_Data);
                mesh.SetColors(m_Colors);
                mesh.SetTriangles(m_Triangles, 0);
                mesh.RecalculateBounds();
                Bounds bounds = mesh.bounds;
                bounds.Expand(new Vector3(height, height * 2.5f, height));
                mesh.bounds = bounds;
                return mesh;
            }
        }
    }
}
