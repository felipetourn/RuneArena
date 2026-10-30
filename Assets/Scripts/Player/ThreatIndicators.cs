using System.Collections.Generic;
using RuneArena.Effects;
using RuneArena.Enemies;
using UnityEngine;

namespace RuneArena.Player
{
    // Red arcs around the edge of the view, each pointing at an enemy the player isn't looking at: brighter
    // the closer it is, and beating while it winds up an attack. On from the start in easy, an upgrade
    // in normal, never in hard. The GameManager adds it to the XR Origin and turns it on.
    public class ThreatIndicators : MonoBehaviour
    {
        const int k_Max = 6;
        const int k_Segments = 12;

        static readonly int k_BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField] Color m_Color = new Color(1f, 0.12f, 0.08f, 1f);
        [Tooltip("Enemies further than this don't show.")]
        [SerializeField] float m_Range = 15f;
        [Tooltip("Enemies less than this many degrees from where the player looks count as seen.")]
        [SerializeField] float m_SeenAngle = 40f;
        [Tooltip("How far in front of the eyes the arcs are, and how far out from the middle of the view.")]
        [SerializeField] float m_Distance = 1f;
        [SerializeField] float m_Radius = 0.45f;
        [SerializeField] float m_Width = 0.035f;
        [Tooltip("Degrees of the circle each arc covers.")]
        [SerializeField] float m_Span = 30f;

        public bool Active { get; set; }

        readonly List<Enemy> m_Unseen = new List<Enemy>();
        Transform m_Head;
        Transform m_Root;
        MeshRenderer[] m_Arcs;
        MaterialPropertyBlock m_Block;

        void Start()
        {
            var health = GetComponent<PlayerHealth>();
            m_Head = health != null ? health.Head : Camera.main.transform;
            m_Block = new MaterialPropertyBlock();

            m_Root = new GameObject("Threat Indicators").transform;
            m_Root.SetParent(m_Head, false);
            m_Root.localPosition = Vector3.forward * m_Distance;

            Mesh mesh = ArcMesh();
            Material material = Fx.Library != null ? Fx.Library.Line : null;
            m_Arcs = new MeshRenderer[k_Max];
            for (int i = 0; i < k_Max; i++)
            {
                var arc = new GameObject("Arc");
                arc.transform.SetParent(m_Root, false);
                arc.AddComponent<MeshFilter>().sharedMesh = mesh;
                m_Arcs[i] = arc.AddComponent<MeshRenderer>();
                m_Arcs[i].sharedMaterial = material;
                m_Arcs[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                m_Arcs[i].receiveShadows = false;
                arc.SetActive(false);
            }
        }

        void LateUpdate()
        {
            if (m_Arcs == null)
                return;

            m_Unseen.Clear();
            if (Active)
            {
                foreach (Enemy enemy in Enemy.All)
                {
                    Vector3 to = Center(enemy) - m_Head.position;
                    if (enemy.IsDead || !enemy.IsInsideArena || to.magnitude > m_Range || Vector3.Angle(m_Head.forward, to) < m_SeenAngle)
                        continue;
                    m_Unseen.Add(enemy);
                }
                // The closest ones, if there are more than arcs.
                Vector3 head = m_Head.position;
                m_Unseen.Sort((a, b) => (Center(a) - head).sqrMagnitude.CompareTo((Center(b) - head).sqrMagnitude));
            }

            for (int i = 0; i < m_Arcs.Length; i++)
            {
                bool shown = i < m_Unseen.Count;
                m_Arcs[i].gameObject.SetActive(shown);
                if (shown)
                    Point(m_Arcs[i], m_Unseen[i]);
            }
        }

        void Point(MeshRenderer arc, Enemy enemy)
        {
            // Where it is around the middle of the view; straight behind reads as below.
            Vector3 local = m_Head.InverseTransformPoint(Center(enemy));
            var around = new Vector2(local.x, local.y);
            if (around.sqrMagnitude < 1e-4f)
                around = Vector2.down;
            float angle = Mathf.Atan2(around.y, around.x) * Mathf.Rad2Deg;
            arc.transform.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);

            float near = 1f - Mathf.Clamp01(local.magnitude / m_Range);
            float strength = 0.3f + 0.7f * near;
            if (enemy.Telegraphing)
                strength *= 1.2f + 0.8f * Mathf.Sin(Time.time * 14f);
            // The material adds its color, so dimmer is fainter.
            m_Block.SetColor(k_BaseColor, m_Color * strength);
            arc.SetPropertyBlock(m_Block);
        }

        static Vector3 Center(Enemy enemy)
        {
            return enemy.transform.position + Vector3.up;
        }

        // A thin piece of ring at the top of the circle, glowing across its width.
        Mesh ArcMesh()
        {
            var vertices = new Vector3[(k_Segments + 1) * 2];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[k_Segments * 6];
            for (int i = 0; i <= k_Segments; i++)
            {
                float t = (float)i / k_Segments;
                float a = (90f + m_Span * (t - 0.5f)) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                vertices[i * 2] = dir * (m_Radius - m_Width * 0.5f);
                vertices[i * 2 + 1] = dir * (m_Radius + m_Width * 0.5f);
                uvs[i * 2] = new Vector2(t, 0f);
                uvs[i * 2 + 1] = new Vector2(t, 1f);
                if (i == k_Segments)
                    break;
                int v = i * 2, k = i * 6;
                triangles[k] = v;
                triangles[k + 1] = v + 1;
                triangles[k + 2] = v + 2;
                triangles[k + 3] = v + 1;
                triangles[k + 4] = v + 3;
                triangles[k + 5] = v + 2;
            }
            var mesh = new Mesh { name = "Threat Arc", vertices = vertices, uv = uvs, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
