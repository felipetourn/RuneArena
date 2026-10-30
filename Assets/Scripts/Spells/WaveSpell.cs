using RuneArena.Effects;
using UnityEngine;

namespace RuneArena.Spells
{
    // Triangle. A wave that leaves from the player's feet, wide at first and narrowing as it goes.
    // Little damage, but it pushes, slows and wets everything it rolls over. The closer the enemy,
    // the bigger the wave when it hits, so the stronger the push and the slow.
    public class WaveSpell : RuneSpell
    {
        [SerializeField] Color m_Color = new Color(0.2f, 0.6f, 1f);
        [SerializeField] float m_ManaCost = 20f;
        [SerializeField] float m_Speed = 8f;
        [SerializeField] float m_Range = 10f;
        [Tooltip("How far in front of the player the wave starts, so it doesn't fill the view.")]
        [SerializeField] float m_StartOffset = 1f;

        [Header("Size (start / end)")]
        [SerializeField] float m_StartWidth = 3f;
        [SerializeField] float m_EndWidth = 1f;
        [SerializeField] float m_StartHeight = 1.2f;
        [SerializeField] float m_EndHeight = 0.5f;

        [Header("Effect (close / far)")]
        [SerializeField] float m_Damage = 10f;
        [SerializeField, Range(0f, 1f)] float m_CloseSlow = 0.5f;
        [SerializeField, Range(0f, 1f)] float m_FarSlow = 0.15f;
        [SerializeField] float m_ClosePush = 10f;
        [SerializeField] float m_FarPush = 4f;
        [Tooltip("Seconds the slow and the wet status last.")]
        [SerializeField] float m_Duration = 5f;

        public override string Label => "triangle";
        public override Color Color => m_Color;
        public override float ManaCost => m_ManaCost;

        public float Speed => m_Speed;
        public float Range => m_Range * (HasPerk(SpellPerks.HighTide) ? 1.5f : 1f);
        public float Damage => m_Damage;
        public float Duration => m_Duration;

        public float Width(float t) => Mathf.Lerp(m_StartWidth, m_EndWidth, t) * (HasPerk(SpellPerks.HighTide) ? 1.5f : 1f);
        public float Height(float t) => Mathf.Lerp(m_StartHeight, m_EndHeight, t);
        public float Slow(float t) => Mathf.Lerp(m_CloseSlow, m_FarSlow, t);
        public float Push(float t) => Mathf.Lerp(m_ClosePush, m_FarPush, t);

        public override void Cast(in SpellCast cast)
        {
            Vector3 start = cast.Player != null ? cast.Player.Feet : Flat(cast.Origin);

            Vector3 target = cast.Aim.GetPoint(m_Range);
            if (Physics.Raycast(cast.Aim, out RaycastHit hit, m_Range * 2f, EnemyMask | FloorMask | WallMask, QueryTriggerInteraction.Ignore))
                target = hit.point;

            Vector3 direction = Flat(target - start);
            if (direction.sqrMagnitude < 0.01f)
                direction = Flat(cast.Aim.direction);
            if (direction.sqrMagnitude < 0.01f)
                return;

            direction.Normalize();
            GameObject go = CreateVisual(PrimitiveType.Cube, "Wave", cast.Material, m_Color);
            // With effects the wave is a curling water mesh; the box only says where it hits.
            if (Fx.Library != null)
            {
                go.GetComponent<Renderer>().enabled = false;
                SpellFx.WaveBody(go.transform);
                SpellFx.WaveSpray(go.transform, m_Color);
            }
            go.AddComponent<WaveFront>().Launch(this, start + direction * m_StartOffset, direction, cast.Power);
        }
    }
}
