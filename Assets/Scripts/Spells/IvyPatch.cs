using System.Collections.Generic;
using RuneArena.Enemies;
using UnityEngine;

namespace RuneArena.Spells
{
    public class IvyPatch : MonoBehaviour
    {
        const float k_Tick = 0.5f;
        const float k_Height = 0.02f;
        const float k_GrowTime = 0.3f;

        static readonly List<IvyPatch> s_All = new List<IvyPatch>();
        static readonly List<Enemy> s_Enemies = new List<Enemy>();

        IvySpell m_Spell;
        Vector3 m_Center;
        float m_Power;
        float m_EndAt;
        float m_BornAt;
        float m_NextTick;
        bool m_Burning;

        public static IReadOnlyList<IvyPatch> All => s_All;
        public Vector3 Center => m_Center;
        public float Radius => m_Spell.Radius;

        public void Grow(IvySpell spell, Vector3 ground, float power)
        {
            m_Spell = spell;
            m_Center = ground;
            m_Power = power;
            m_BornAt = Time.time;
            m_EndAt = Time.time + spell.Duration;
            transform.position = ground + Vector3.up * k_Height;
            SetSize(0f);
        }

        void SetSize(float t)
        {
            float diameter = Mathf.Lerp(0.3f, m_Spell.Radius * 2f, t);
            transform.localScale = new Vector3(diameter, 0.01f, diameter);
        }

        void OnEnable()
        {
            s_All.Add(this);
        }

        void OnDisable()
        {
            s_All.Remove(this);
        }

        // Fire burns it away. It doesn't hurt anyone: losing the ivy is the cost.
        public void Burn()
        {
            if (m_Burning)
                return;
            m_Burning = true;
            s_All.Remove(this);
            Vector3 size = transform.localScale;
            SpellVisual.Spawn(gameObject, size, new Vector3(0.1f, size.y, 0.1f), 0.25f);
        }

        void Update()
        {
            if (m_Burning)
                return;
            if (Time.time >= m_EndAt)
            {
                Destroy(gameObject);
                return;
            }
            SetSize(Mathf.Clamp01((Time.time - m_BornAt) / k_GrowTime));
            if (Time.time < m_NextTick)
                return;
            m_NextTick = Time.time + k_Tick;

            RuneSpell.EnemiesInSphere(m_Center, m_Spell.Radius + 0.5f, s_Enemies);
            foreach (Enemy enemy in s_Enemies)
            {
                Vector3 p = enemy.transform.position;
                // Only those standing on it: flyers above don't count.
                if (enemy.IsFlying || p.y > m_Center.y + 0.5f)
                    continue;
                Vector3 d = p - m_Center;
                d.y = 0f;
                if (d.magnitude > m_Spell.Radius)
                    continue;

                enemy.ApplyStatus(EnemyStatus.Entangled, k_Tick + 0.1f);
                enemy.ApplySlow(m_Spell.Slow, k_Tick + 0.1f);
                enemy.Damage(m_Spell.DamagePerSecond * k_Tick * m_Power, DamageKind.Normal, m_Spell.Label);
            }
        }
    }
}
