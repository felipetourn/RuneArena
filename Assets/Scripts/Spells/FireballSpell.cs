using RuneArena.Effects;
using UnityEngine;

namespace RuneArena.Spells
{
    // Circle. A ball that is easy to hit with: big direct damage to the first enemy it touches,
    // plus a small explosion that hurts and pushes whoever is around. Dries wet enemies, but does less damage to them.
    public class FireballSpell : RuneSpell
    {
        [SerializeField] Color m_Color = new Color(1f, 0.45f, 0.1f);
        [SerializeField] float m_ManaCost = 15f;
        [SerializeField] float m_Speed = 14f;
        [Tooltip("Radius of the ball. Bigger is easier to hit with.")]
        [SerializeField] float m_Radius = 0.35f;
        [SerializeField] float m_Range = 40f;
        [SerializeField] float m_Damage = 35f;
        [Tooltip("Damage multiplier against wet, frozen or entangled enemies. Keep the loss smaller than the lightning's gain.")]
        [SerializeField, Range(0f, 1f)] float m_WetMultiplier = 0.75f;

        [Header("Explosion")]
        [SerializeField] float m_ExplosionRadius = 1f;
        [SerializeField] float m_ExplosionDamage = 10f;
        [Tooltip("Push speed (m/s) away from the explosion.")]
        [SerializeField] float m_ExplosionPush = 6f;

        public override string Label => "circle";
        public override Color Color => m_Color;
        public override float ManaCost => m_ManaCost;

        public float Speed => m_Speed;
        public float Radius => m_Radius;
        public float Range => m_Range;
        public float Damage => m_Damage;
        public float WetMultiplier => m_WetMultiplier;
        public float ExplosionRadius => m_ExplosionRadius;
        public float ExplosionDamage => m_ExplosionDamage;
        public float ExplosionPush => m_ExplosionPush;

        public override void Cast(in SpellCast cast)
        {
            Vector3 target = cast.Aim.GetPoint(m_Range);
            if (Physics.Raycast(cast.Aim, out RaycastHit hit, m_Range, EnemyMask | FloorMask | WallMask, QueryTriggerInteraction.Ignore))
                target = hit.point;

            FxLibrary fx = Fx.Library;
            GameObject go = CreateVisual(PrimitiveType.Sphere, "Fireball", SpellFx.Surface(fx != null ? fx.FireCore : null, cast.Material), m_Color);
            go.transform.position = cast.Origin;
            // With effects the flames around it make up the rest of its size.
            go.transform.localScale = Vector3.one * (m_Radius * (fx != null ? 1.1f : 2f));
            SpellFx.FireballTrail(go.transform, m_Radius);
            go.AddComponent<FireballProjectile>().Launch(this, (target - cast.Origin).normalized, cast.Material, cast.Power,
                HasPerk(SpellPerks.BigExplosion) ? 2f : 1f);
        }
    }
}
