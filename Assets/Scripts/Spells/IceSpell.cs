using UnityEngine;

namespace RuneArena.Spells
{
    // V. A fast shard that freezes the first enemy it hits: it can't move or attack for a while.
    // Combo: a wet enemy freezes twice as long. Fire on a frozen enemy melts it and does less (see FireballProjectile).
    public class IceSpell : RuneSpell
    {
        [SerializeField] Color m_Color = new Color(0.6f, 0.9f, 1f);
        [SerializeField] float m_ManaCost = 20f;
        [SerializeField] bool m_StartsLocked = true;
        [SerializeField] float m_Speed = 16f;
        [SerializeField] float m_Radius = 0.25f;
        [SerializeField] float m_Range = 40f;
        [SerializeField] float m_Damage = 10f;
        [SerializeField] float m_FreezeTime = 1.5f;
        [Tooltip("Freeze time multiplier on a wet enemy.")]
        [SerializeField] float m_WetMultiplier = 2f;

        public override string Label => "v";
        public override Color Color => m_Color;
        public override float ManaCost => m_ManaCost;
        public override bool StartsLocked => m_StartsLocked;

        public float Speed => m_Speed;
        public float Radius => m_Radius;
        public float Range => m_Range;
        public float Damage => m_Damage;
        public float FreezeTime => m_FreezeTime;
        public float WetMultiplier => m_WetMultiplier;

        public override void Cast(in SpellCast cast)
        {
            Vector3 target = cast.Aim.GetPoint(m_Range);
            if (Physics.Raycast(cast.Aim, out RaycastHit hit, m_Range, EnemyMask | FloorMask | WallMask, QueryTriggerInteraction.Ignore))
                target = hit.point;

            GameObject go = CreateVisual(PrimitiveType.Cube, "Ice Shard", cast.Material, m_Color);
            Vector3 direction = (target - cast.Origin).normalized;
            go.transform.SetPositionAndRotation(cast.Origin, Quaternion.LookRotation(direction));
            go.transform.localScale = new Vector3(m_Radius, m_Radius, m_Radius * 3f);
            go.AddComponent<IceProjectile>().Launch(this, direction, cast.Material, cast.Power);
        }
    }
}
