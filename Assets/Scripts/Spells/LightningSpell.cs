using System.Collections.Generic;
using RuneArena.Effects;
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

        [Tooltip("With the Chain diamond upgrade: how far a direct hit jumps, and its damage multiplier.")]
        [SerializeField] float m_ChainRange = 5f;
        [SerializeField] float m_ChainDamage = 0.5f;

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

        public float DirectDamage => m_DirectDamage;
        public float FloorDamage => m_FloorDamage;
        public float WetMultiplier => m_WetMultiplier;

        public override void Cast(in SpellCast cast)
        {
            Sfx.Play(Sfx.Library != null ? Sfx.Library.LightningLaunch : null, cast.Origin);
            if (!Physics.Raycast(cast.Aim, out RaycastHit hit, m_Range, EnemyMask | FloorMask | WallMask, QueryTriggerInteraction.Ignore))
                return;

            Enemy direct = hit.collider.GetComponentInParent<Enemy>();
            if (direct != null)
            {
                Strike(direct, m_DirectDamage * cast.Power, m_DirectStun);
                Bolt(hit.point, cast.Material, 0f);
                if (HasPerk(SpellPerks.ChainLightning))
                    Chain(direct, cast);
                return;
            }

            if (IsInLayer(hit.collider, FloorMask))
            {
                EnemiesInSphere(hit.point, m_FloorRadius, s_Enemies);
                foreach (Enemy enemy in s_Enemies)
                    Strike(enemy, m_FloorDamage * cast.Power, m_FloorStun);
                Bolt(hit.point, cast.Material, m_FloorRadius);
                return;
            }

            // Against a wall the rune just fizzles out.
        }

        void Chain(Enemy from, in SpellCast cast)
        {
            Enemy next = null;
            float best = m_ChainRange * m_ChainRange;
            foreach (Enemy enemy in Enemy.All)
            {
                if (enemy == from || enemy.IsDead || !enemy.IsInsideArena)
                    continue;
                float d = (enemy.transform.position - from.transform.position).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    next = enemy;
                }
            }
            if (next == null)
                return;

            Strike(next, m_DirectDamage * cast.Power * m_ChainDamage, m_DirectStun * 0.5f);

            var go = new GameObject("Chain Bolt");
            var line = go.AddComponent<LineRenderer>();
            SpellFx.StyleBolt(line, cast.Material, m_Color, m_BoltWidth);
            go.AddComponent<LightningBolt>().Strike(from.transform.position + Vector3.up, next.transform.position + Vector3.up, m_BoltTime);
            SpellFx.LightningImpact(next.transform.position + Vector3.up, m_Color);
        }

        void Strike(Enemy enemy, float damage, float stun)
        {
            var kind = DamageKind.Normal;
            if (enemy.HasStatus(EnemyStatus.Wet))
            {
                damage *= m_WetMultiplier;
                enemy.ClearStatus(EnemyStatus.Wet);
                kind = DamageKind.Combo;
            }
            // Damage first: it breaks metal armor, so the stun then lands.
            enemy.Damage(damage, kind, Label);
            enemy.ApplyStatus(EnemyStatus.Stunned, stun);
        }

        void Bolt(Vector3 point, Material material, float ringRadius)
        {
            var go = new GameObject("Lightning Bolt");
            var line = go.AddComponent<LineRenderer>();
            SpellFx.StyleBolt(line, material, m_Color, m_BoltWidth);
            go.AddComponent<LightningBolt>().Strike(point + Vector3.up * m_BoltHeight, point, m_BoltTime);
            SpellFx.LightningImpact(point, m_Color);

            if (ringRadius > 0f && Fx.Library != null)
            {
                SpellFx.GroundRing(point, ringRadius * 2f, m_Color, m_BoltTime * 1.5f);
            }
            else if (ringRadius > 0f)
            {
                GameObject ring = CreateVisual(PrimitiveType.Cylinder, "Lightning Ring", material, m_Color);
                ring.transform.position = point + Vector3.up * 0.02f;
                SpellVisual.Spawn(ring, new Vector3(0.2f, 0.01f, 0.2f), new Vector3(ringRadius * 2f, 0.01f, ringRadius * 2f), m_BoltTime);
            }
        }
    }
}
