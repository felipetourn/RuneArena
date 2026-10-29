using System.Collections.Generic;
using RuneArena.Enemies;
using UnityEngine;

namespace RuneArena.Spells
{
    public class FireballProjectile : MonoBehaviour
    {
        static readonly List<Enemy> s_Enemies = new List<Enemy>();
        static readonly Collider[] s_Overlaps = new Collider[8];

        FireballSpell m_Spell;
        Vector3 m_Direction;
        Material m_Material;
        float m_Traveled;

        public void Launch(FireballSpell spell, Vector3 direction, Material material)
        {
            m_Spell = spell;
            m_Direction = direction;
            m_Material = material;
        }

        void Update()
        {
            float step = m_Spell.Speed * Time.deltaTime;
            int mask = LayerMask.GetMask("Enemy", "Floor", "Wall");

            // A sweep misses anything the ball already overlaps, like an earth wall rising into it
            // or a wall it ended up touching, so check for that first.
            int overlaps = Physics.OverlapSphereNonAlloc(transform.position, m_Spell.Radius, s_Overlaps, mask, QueryTriggerInteraction.Ignore);
            if (overlaps > 0)
            {
                Explode(transform.position, s_Overlaps[0]);
                return;
            }

            if (Physics.SphereCast(transform.position, m_Spell.Radius, m_Direction, out RaycastHit hit, step, mask, QueryTriggerInteraction.Ignore))
            {
                Explode(transform.position + m_Direction * hit.distance, hit.collider);
                return;
            }

            transform.position += m_Direction * step;
            m_Traveled += step;
            if (m_Traveled >= m_Spell.Range)
                Explode(transform.position, null);
        }

        void Explode(Vector3 point, Collider touched)
        {
            Enemy direct = touched != null ? touched.GetComponentInParent<Enemy>() : null;

            // Fire breaks earth walls, including the player's own.
            EarthWall wall = touched != null ? touched.GetComponent<EarthWall>() : null;
            if (wall != null)
                wall.Break();

            if (direct != null && !direct.IsDead)
            {
                direct.ClearStatus(EnemyStatus.Wet);
                Push(direct, point);
                direct.Damage(m_Spell.Damage);
            }

            RuneSpell.EnemiesInSphere(point, m_Spell.ExplosionRadius, s_Enemies);
            foreach (Enemy enemy in s_Enemies)
            {
                if (enemy == direct)
                    continue;
                enemy.ClearStatus(EnemyStatus.Wet);
                Push(enemy, point);
                enemy.Damage(m_Spell.ExplosionDamage);
            }

            GameObject flash = RuneSpell.CreateVisual(PrimitiveType.Sphere, "Fireball Explosion", m_Material, m_Spell.Color);
            flash.transform.position = point;
            SpellVisual.Spawn(flash, Vector3.one * m_Spell.Radius * 2f, Vector3.one * m_Spell.ExplosionRadius * 2f, 0.15f);
            Destroy(gameObject);
        }

        void Push(Enemy enemy, Vector3 point)
        {
            Vector3 away = enemy.transform.position - point;
            away.y = 0f;
            if (away.sqrMagnitude < 1e-4f)
                away = m_Direction;
            enemy.Push(away.normalized * m_Spell.ExplosionPush);
        }
    }
}
