using RuneArena.Effects;
using RuneArena.Enemies;
using UnityEngine;

namespace RuneArena.Spells
{
    public class IceProjectile : MonoBehaviour
    {
        static readonly Collider[] s_Overlaps = new Collider[8];

        IceSpell m_Spell;
        Vector3 m_Direction;
        Material m_Material;
        float m_Power = 1f;
        float m_Traveled;

        public void Launch(IceSpell spell, Vector3 direction, Material material, float power)
        {
            m_Spell = spell;
            m_Direction = direction;
            m_Material = material;
            m_Power = power;
        }

        void Update()
        {
            float step = m_Spell.Speed * Time.deltaTime;
            int mask = LayerMask.GetMask("Enemy", "Floor", "Wall");

            int overlaps = Physics.OverlapSphereNonAlloc(transform.position, m_Spell.Radius, s_Overlaps, mask, QueryTriggerInteraction.Ignore);
            if (overlaps > 0)
            {
                Hit(s_Overlaps[0]);
                return;
            }
            if (Physics.SphereCast(transform.position, m_Spell.Radius, m_Direction, out RaycastHit hit, step, mask, QueryTriggerInteraction.Ignore))
            {
                transform.position += m_Direction * hit.distance;
                Hit(hit.collider);
                return;
            }

            transform.position += m_Direction * step;
            m_Traveled += step;
            if (m_Traveled >= m_Spell.Range)
                Hit(null);
        }

        void Hit(Collider collider)
        {
            Enemy enemy = collider != null ? collider.GetComponentInParent<Enemy>() : null;
            if (enemy != null && !enemy.IsDead)
            {
                float freeze = m_Spell.FreezeTime;
                float element = enemy is RangedEnemy mage ? mage.ElementMultiplier(m_Spell.Label) : 1f;
                // A wet enemy freezes for longer, which reads as a combo whatever the damage.
                DamageKind kind = ElementalDamage.KindFor(element);
                if (enemy.HasStatus(EnemyStatus.Wet))
                {
                    freeze *= m_Spell.WetMultiplier;
                    enemy.ClearStatus(EnemyStatus.Wet);
                    kind = DamageKind.Combo;
                }
                enemy.ApplyStatus(EnemyStatus.Frozen, freeze);
                enemy.Damage(m_Spell.Damage * m_Power * element, kind, m_Spell.Label);
            }

            if (Fx.Library != null)
            {
                SpellFx.Shatter(transform.position, m_Spell.Color);
            }
            else
            {
                GameObject flash = RuneSpell.CreateVisual(PrimitiveType.Sphere, "Ice Shatter", m_Material, m_Spell.Color);
                flash.transform.position = transform.position;
                SpellVisual.Spawn(flash, Vector3.one * 0.2f, Vector3.one * 0.9f, 0.12f);
            }
            Fx.ReleaseAll(gameObject);
            Destroy(gameObject);
        }
    }
}
