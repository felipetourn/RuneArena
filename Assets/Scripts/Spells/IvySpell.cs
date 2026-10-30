using System.Collections.Generic;
using RuneArena.Effects;
using RuneArena.Enemies;
using UnityEngine;

namespace RuneArena.Spells
{
    // Spiral. Covers the floor where the player aims with ivy: steady damage and a slow to whoever stands on it.
    // Combo: on or next to an earth wall, roots come out of the wall and grab the closest walking enemy.
    // Fire burns the ivy away (see FireballProjectile).
    public class IvySpell : RuneSpell
    {
        static readonly List<Enemy> s_Enemies = new List<Enemy>();

        [SerializeField] Color m_Color = new Color(0.25f, 0.7f, 0.25f);
        [SerializeField] float m_ManaCost = 25f;
        [SerializeField] bool m_StartsLocked = true;
        [SerializeField] float m_Range = 30f;

        [Header("Patch")]
        [SerializeField] float m_Radius = 3f;
        [SerializeField] float m_Duration = 6f;
        [SerializeField] float m_DamagePerSecond = 5f;
        [SerializeField, Range(0f, 1f)] float m_Slow = 0.3f;

        [Header("Roots (with an earth wall)")]
        [Tooltip("How close to an earth wall the ivy has to land to make roots.")]
        [SerializeField] float m_WallRange = 1.5f;
        [SerializeField] float m_RootReach = 4f;
        [SerializeField] float m_RootDamage = 40f;
        [SerializeField] float m_RootTime = 2f;

        public override string Label => "spiral";
        public override Color Color => m_Color;
        public override float ManaCost => m_ManaCost;
        public override bool StartsLocked => m_StartsLocked;

        public float Radius => m_Radius;
        public float Duration => m_Duration;
        public float DamagePerSecond => m_DamagePerSecond;
        public float Slow => m_Slow;

        public override void Cast(in SpellCast cast)
        {
            if (!Physics.Raycast(cast.Aim, out RaycastHit hit, m_Range, FloorMask | WallMask, QueryTriggerInteraction.Ignore))
                return;

            EarthWall wall = hit.collider.GetComponent<EarthWall>();
            Vector3 ground = hit.point;
            if (wall == null)
            {
                // The arena wall or a pillar: nothing grows there.
                if (!IsInLayer(hit.collider, FloorMask))
                    return;
                wall = NearestWall(ground);
            }
            else
            {
                ground = wall.ClosestPoint(cast.Player != null ? cast.Player.Feet : cast.Origin);
            }

            if (wall != null)
                Roots(wall, cast);

            FxLibrary fx = Fx.Library;
            bool leafy = fx != null && fx.Ivy != null;
            GameObject go = CreateVisual(PrimitiveType.Cylinder, "Ivy", leafy ? fx.Ivy : cast.Material, leafy ? Color.white : m_Color);
            go.AddComponent<IvyPatch>().Grow(this, ground, cast.Power);
            SpellFx.Leaves(ground, Radius, m_Color);
        }

        EarthWall NearestWall(Vector3 point)
        {
            EarthWall best = null;
            float closest = m_WallRange;
            foreach (EarthWall wall in EarthWall.All)
            {
                if (wall.IsBroken)
                    continue;
                float d = Vector3.Distance(Flat(wall.ClosestPoint(point)), Flat(point));
                if (d <= closest)
                {
                    closest = d;
                    best = wall;
                }
            }
            return best;
        }

        void Roots(EarthWall wall, in SpellCast cast)
        {
            Enemy target = null;
            float closest = float.PositiveInfinity;
            EnemiesInSphere(wall.transform.position, m_RootReach + wall.transform.localScale.x * 0.5f, s_Enemies);
            foreach (Enemy enemy in s_Enemies)
            {
                if (enemy.IsFlying)
                    continue;
                Vector3 p = enemy.transform.position;
                float d = Vector3.Distance(Flat(wall.ClosestPoint(p)), Flat(p));
                if (d <= m_RootReach && d < closest)
                {
                    closest = d;
                    target = enemy;
                }
            }
            if (target == null)
                return;

            target.ApplyStatus(EnemyStatus.Rooted, m_RootTime);
            target.Damage(m_RootDamage * cast.Power, DamageKind.Combo, Label);

            var go = new GameObject("Roots");
            var line = go.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.SetPosition(0, wall.ClosestPoint(target.transform.position) + Vector3.up * 0.3f);
            line.SetPosition(1, target.transform.position + Vector3.up * 0.3f);
            line.widthMultiplier = 0.15f;
            Tint(line, cast.Material, m_Color * 0.7f);
            Destroy(go, m_RootTime);
        }
    }
}
