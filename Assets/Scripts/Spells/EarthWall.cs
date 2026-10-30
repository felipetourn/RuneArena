using System.Collections.Generic;
using RuneArena.Enemies;
using UnityEngine;
using UnityEngine.AI;

namespace RuneArena.Spells
{
    // Comes up out of the floor, stays, then sinks back. Carves the NavMesh so walking enemies go around it.
    public class EarthWall : MonoBehaviour
    {
        static readonly List<EarthWall> s_All = new List<EarthWall>();

        // Standing earth walls, for spells that combo with them.
        public static IReadOnlyList<EarthWall> All => s_All;

        public bool IsBroken => m_BrokenAt >= 0f;

        void OnEnable()
        {
            s_All.Add(this);
        }

        void OnDisable()
        {
            s_All.Remove(this);
        }

        // Closest point on the wall's footprint to p, on the ground.
        public Vector3 ClosestPoint(Vector3 p)
        {
            Vector3 local = transform.InverseTransformPoint(p);
            local.x = Mathf.Clamp(local.x, -0.5f, 0.5f);
            local.z = Mathf.Clamp(local.z, -0.5f, 0.5f);
            Vector3 world = transform.TransformPoint(local);
            world.y = m_Ground.y;
            return world;
        }

        readonly List<Enemy> m_Enemies = new List<Enemy>();

        const float k_BreakTime = 0.15f;

        EarthWallSpell m_Spell;
        Vector3 m_Ground;
        float m_Start;
        float m_Duration;
        float m_BrokenAt = -1f;
        float m_HeightAtBreak;

        public void Raise(EarthWallSpell spell, Vector3 ground, Quaternion rotation, float duration)
        {
            m_Duration = duration;
            m_Spell = spell;
            m_Ground = ground;
            m_Start = Time.time;
            transform.SetPositionAndRotation(ground, rotation);
            transform.localScale = new Vector3(spell.Width, spell.Height, spell.Thickness);

            var obstacle = gameObject.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = Vector3.one;
            obstacle.carving = true;

            PushOut();
            Place(0f);
        }

        // Collapses into the floor right away and stops blocking.
        public void Break()
        {
            if (m_BrokenAt >= 0f)
                return;
            m_BrokenAt = Time.time;
            m_HeightAtBreak = CurrentUp();
            SpellFx.Debris(transform.position, transform.localScale.x, transform.localScale.y);
            GetComponent<Collider>().enabled = false;
            Destroy(GetComponent<NavMeshObstacle>());
        }

        void Update()
        {
            if (m_BrokenAt >= 0f)
            {
                float t = (Time.time - m_BrokenAt) / k_BreakTime;
                Place(Mathf.Lerp(m_HeightAtBreak, 0f, t));
                if (t >= 1f)
                    Destroy(gameObject);
                return;
            }

            Place(CurrentUp());
            if (Time.time - m_Start >= m_Duration)
                Destroy(gameObject);
        }

        float CurrentUp()
        {
            float age = Time.time - m_Start;
            float rise = m_Spell.RiseTime;
            float up;
            if (age < rise)
                up = age / rise;
            else if (age < m_Duration - rise)
                up = 1f;
            else
                up = Mathf.Clamp01((m_Duration - age) / rise);
            return up;
        }

        // up = 0 buried, 1 fully raised.
        void Place(float up)
        {
            float height = m_Spell.Height;
            transform.position = m_Ground + Vector3.up * (height * (up - 0.5f));
        }

        // Enemies standing on the spot get shoved to whichever side they were on.
        void PushOut()
        {
            float reach = Mathf.Max(m_Spell.Width, m_Spell.Thickness) * 0.5f + 0.5f;
            RuneSpell.EnemiesInSphere(m_Ground, reach, m_Enemies);
            Vector3 normal = transform.forward;
            foreach (Enemy enemy in m_Enemies)
            {
                Vector3 local = transform.InverseTransformPoint(enemy.transform.position);
                // Local units are scaled by the wall size; 0.5 m of slack covers the enemy's own radius.
                if (Mathf.Abs(local.x) > 0.5f + 0.5f / m_Spell.Width || Mathf.Abs(local.z) > 0.5f + 0.5f / m_Spell.Thickness)
                    continue;
                float side = Vector3.Dot(enemy.transform.position - m_Ground, normal) >= 0f ? 1f : -1f;
                enemy.Push(normal * (side * m_Spell.PushSpeed));
            }
        }
    }
}
