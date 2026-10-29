using System.Collections.Generic;
using RuneArena.Enemies;
using UnityEngine;

namespace RuneArena.Spells
{
    // Zigzag. Strikes where the player aims, instantly. Aimed straight at an enemy it hits only that one,
    // harder and with a longer stun; on the floor it hits everyone around. Wet enemies take extra damage
    // and dry off.
    public class LightningSpell : RuneSpell
    {
        static readonly List<Enemy> s_Enemies = new List<Enemy>();

        [SerializeField] Color m_Color = new Color(1f, 0.95f, 0.3f);
        [SerializeField] float m_ManaCost = 25f;
        [SerializeField] float m_Range = 50f;
        [Tooltip("Damage multiplier against wet enemies.")]
        [SerializeField] float m_WetMultiplier = 1.5f;

        [Header("Direct hit")]
        [SerializeField] float m_DirectDamage = 30f;
        [SerializeField] float m_DirectStun = 1.5f;

        [Header("Floor strike")]
        [SerializeField] float m_FloorRadius = 1.5f;
        [SerializeField] float m_FloorDamage = 20f;
        [SerializeField] float m_FloorStun = 1f;

        [Header("Bolt")]
        [SerializeField] float m_BoltHeight = 12f;
        [SerializeField] float m_BoltWidth = 0.08f;
        [SerializeField] float m_BoltTime = 0.2f;

        public override string Label => "lightning";
        public override Color Color => m_Color;
        public override float ManaCost => m_ManaCost;

        public override void Cast(in SpellCast cast)
        {
            if (!Physics.Raycast(cast.Aim, out RaycastHit hit, m_Range, EnemyMask | FloorMask | WallMask, QueryTriggerInteraction.Ignore))
                return;

            Enemy direct = hit.collider.GetComponentInParent<Enemy>();
            if (direct != null)
            {
                Strike(direct, m_DirectDamage, m_DirectStun);
                Bolt(hit.point, cast.Material, 0f);
                return;
            }

            if (IsInLayer(hit.collider, FloorMask))
            {
                EnemiesInSphere(hit.point, m_FloorRadius, s_Enemies);
                foreach (Enemy enemy in s_Enemies)
                    Strike(enemy, m_FloorDamage, m_FloorStun);
                Bolt(hit.point, cast.Material, m_FloorRadius);
                return;
            }

            // Against a wall the rune just fizzles out.
        }

        void Strike(Enemy enemy, float damage, float stun)
        {
            if (enemy.HasStatus(EnemyStatus.Wet))
            {
                damage *= m_WetMultiplier;
                enemy.ClearStatus(EnemyStatus.Wet);
            }
            enemy.ApplyStatus(EnemyStatus.Stunned, stun);
            enemy.Damage(damage);
        }

        void Bolt(Vector3 point, Material material, float ringRadius)
        {
            var go = new GameObject("Lightning Bolt");
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.widthMultiplier = m_BoltWidth;
            Tint(line, material, m_Color);
            go.AddComponent<LightningBolt>().Strike(point + Vector3.up * m_BoltHeight, point, m_BoltTime);

            if (ringRadius > 0f)
            {
                GameObject ring = CreateVisual(PrimitiveType.Cylinder, "Lightning Ring", material, m_Color);
                ring.transform.position = point + Vector3.up * 0.02f;
                SpellVisual.Spawn(ring, new Vector3(0.2f, 0.01f, 0.2f), new Vector3(ringRadius * 2f, 0.01f, ringRadius * 2f), m_BoltTime);
            }
        }
    }
}
