using System.Collections.Generic;
using RuneArena.Effects;
using RuneArena.Enemies;
using UnityEngine;

namespace RuneArena.Spells
{
    public class WaveFront : MonoBehaviour
    {
        const float k_Depth = 0.5f;

        readonly HashSet<Enemy> m_Hit = new HashSet<Enemy>();
        readonly Collider[] m_Overlaps = new Collider[32];

        WaveSpell m_Spell;
        Vector3 m_Start;
        Vector3 m_Direction;
        Quaternion m_Rotation;
        float m_Traveled;
        int m_EnemyMask;
        int m_WallMask;
        float m_Power = 1f;

        public void Launch(WaveSpell spell, Vector3 start, Vector3 direction, float power)
        {
            m_Power = power;
            m_Spell = spell;
            m_Start = start;
            m_Direction = direction;
            m_Rotation = Quaternion.LookRotation(direction);
            m_EnemyMask = LayerMask.GetMask("Enemy");
            m_WallMask = LayerMask.GetMask("Wall");
            transform.rotation = m_Rotation;
            Place(0f);
        }

        void Update()
        {
            m_Traveled += m_Spell.Speed * Time.deltaTime;
            float t = Mathf.Clamp01(m_Traveled / m_Spell.Range);
            Vector3 center = Place(t);
            var halfExtents = new Vector3(m_Spell.Width(t), m_Spell.Height(t), k_Depth) * 0.5f;

            int count = Physics.OverlapBoxNonAlloc(center, halfExtents, m_Overlaps, m_Rotation, m_EnemyMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Enemy enemy = m_Overlaps[i].GetComponentInParent<Enemy>();
                if (enemy == null || enemy.IsDead || !m_Hit.Add(enemy))
                    continue;

                // Damage first: it breaks magma armor, so the push and slow then land.
                float element = enemy is RangedEnemy mage ? mage.ElementMultiplier(m_Spell.Label) : 1f;
                enemy.Damage(m_Spell.Damage * m_Power * element, ElementalDamage.KindFor(element), m_Spell.Label);
                enemy.ApplyStatus(EnemyStatus.Wet, m_Spell.Duration);
                enemy.ApplySlow(m_Spell.Slow(t), m_Spell.Duration);
                enemy.Push(m_Direction * m_Spell.Push(t));
            }

            // Breaks when its middle hits the arena wall, a gate or an earth wall. Only the middle,
            // so a wide wave cast next to a wall isn't cut short by its edges.
            var front = new Vector3(0.5f, halfExtents.y, halfExtents.z);
            bool blocked = Physics.CheckBox(center, front, m_Rotation, m_WallMask, QueryTriggerInteraction.Ignore);
            if (blocked || t >= 1f)
            {
                Fx.ReleaseAll(gameObject);
                Destroy(gameObject);
            }
        }

        Vector3 Place(float t)
        {
            float height = m_Spell.Height(t);
            Vector3 center = m_Start + m_Direction * m_Traveled + Vector3.up * (height * 0.5f);
            transform.position = center;
            transform.localScale = new Vector3(m_Spell.Width(t), height, k_Depth);
            return center;
        }
    }
}
