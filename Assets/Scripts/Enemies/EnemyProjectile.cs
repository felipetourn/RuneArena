using System;
using RuneArena.Effects;
using RuneArena.Player;
using RuneArena.Spells;
using UnityEngine;

namespace RuneArena.Enemies
{
    /// <summary>How an enemy projectile looks: its own plain ball, or the player's fireball or ice shard.</summary>
    public enum ProjectileLook
    {
        Plain,
        Fire,
        Ice,
    }

    // Flies straight. Hits the player's head or torso, and stops at walls and the floor,
    // so an earth wall in the way blocks it. One that explodes bursts wherever it stops and hurts the player
    // if they are close, even if it missed them.
    public class EnemyProjectile : MonoBehaviour
    {
        const float k_TorsoLength = 0.7f;

        [SerializeField] float m_HitRadius = 0.3f;
        [SerializeField] float m_Lifetime = 5f;
        [SerializeField] LayerMask m_BlockMask;
        [Tooltip("Above 0 it explodes where it stops, hurting the player within this radius.")]
        [SerializeField] float m_ExplosionRadius;

        [Header("Look")]
        [Tooltip("Fire looks like the player's fireball and breaks the earth wall it hits; ice like their ice shard.")]
        [SerializeField] ProjectileLook m_Look;
        [SerializeField] Color m_Color = Color.white;

        [Header("Slow")]
        [Tooltip("How much slower the player moves after a hit (0.4 = 40%). 0 for none.")]
        [SerializeField, Range(0f, 1f)] float m_Slow;
        [SerializeField] float m_SlowTime = 2f;

        Vector3 m_Velocity;
        float m_Damage;
        PlayerHealth m_Target;
        Action m_OnHit;
        float m_BaseScale;
        float m_GoneAt;

        // Grown after spawning (a boss's bigger throw), it hits from as far as it looks.
        float HitRadius => m_HitRadius * transform.localScale.x / m_BaseScale;

        void Awake()
        {
            m_BaseScale = transform.localScale.x;
        }

        // onHit runs if it reaches the player, for a boss that counts its blows.
        public void Launch(Vector3 velocity, float damage, PlayerHealth target, Action onHit = null)
        {
            m_Velocity = velocity;
            m_Damage = damage;
            m_Target = target;
            m_OnHit = onHit;
            m_GoneAt = Time.time + m_Lifetime;
            Dress();
        }

        // The look of the player's own spells, from the same effects library. Without it, the plain ball stays.
        void Dress()
        {
            if (m_Look == ProjectileLook.Plain)
                return;
            var renderer = GetComponent<Renderer>();
            FxLibrary fx = Fx.Library;
            if (m_Look == ProjectileLook.Fire)
            {
                if (fx != null && fx.FireCore != null)
                    RuneSpell.Tint(renderer, fx.FireCore, m_Color);
                SpellFx.FireballTrail(transform, HitRadius);
            }
            else
            {
                if (fx != null && fx.Ice != null)
                {
                    RuneSpell.Tint(renderer, fx.Ice, m_Color);
                    GetComponent<MeshFilter>().sharedMesh = SpellMeshes.Icicle;
                }
                // Pointing where it flies, turned on its long axis so it reads as a crystal.
                transform.rotation = Quaternion.LookRotation(m_Velocity) * Quaternion.Euler(0f, 0f, 45f);
                SpellFx.FrostTrail(transform);
            }
        }

        void Update()
        {
            if (Time.time >= m_GoneAt)
            {
                // Out of time in the air: the trail is let go so it fades instead of vanishing.
                Fx.ReleaseAll(gameObject);
                Destroy(gameObject);
                return;
            }
            Vector3 step = m_Velocity * Time.deltaTime;
            Vector3 from = transform.position;
            if (Physics.Raycast(from, m_Velocity, out RaycastHit hit, step.magnitude, m_BlockMask, QueryTriggerInteraction.Ignore))
            {
                // Fire breaks the earth wall it hits, like the player's fireball. This one still stops at it.
                if (m_Look == ProjectileLook.Fire)
                {
                    EarthWall wall = hit.collider.GetComponentInParent<EarthWall>();
                    if (wall != null)
                        wall.Break();
                }
                if (m_ExplosionRadius > 0f)
                    Explode(hit.point);
                Stop(hit.point);
                return;
            }

            transform.position = from + step;

            if (m_Target != null && PlayerDistance(transform.position) <= HitRadius)
            {
                if (m_ExplosionRadius > 0f)
                {
                    Explode(transform.position);
                }
                else
                {
                    m_Target.Damage(m_Damage);
                    Chill();
                    m_OnHit?.Invoke();
                }
                Stop(transform.position);
            }
        }

        void Stop(Vector3 point)
        {
            if (m_Look == ProjectileLook.Ice)
                SpellFx.Shatter(point, m_Color);
            else if (m_Look == ProjectileLook.Fire && m_ExplosionRadius <= 0f)
                SpellFx.FireballExplosion(point, HitRadius * 2f);
            Fx.ReleaseAll(gameObject);
            Destroy(gameObject);
        }

        void Chill()
        {
            if (m_Slow <= 0f || m_Target == null || m_Target.IsDead)
                return;
            var movement = m_Target.GetComponent<PlayerMovement>();
            if (movement != null)
                movement.Slow(m_Slow, m_SlowTime);
        }

        void Explode(Vector3 point)
        {
            SpellFx.FireballExplosion(point, m_ExplosionRadius);
            if (m_Target == null || m_Target.IsDead || PlayerDistance(point) > m_ExplosionRadius)
                return;
            // An earth wall that stopped it also shields whoever is behind it.
            Vector3 back = point - m_Velocity.normalized * 0.2f;
            if (Physics.Linecast(back, m_Target.Head.position + Vector3.down * 0.3f, LayerMask.GetMask("Wall"), QueryTriggerInteraction.Ignore))
                return;
            m_Target.Damage(m_Damage);
            Chill();
            m_OnHit?.Invoke();
        }

        // From a point to the nearest part of the player's head and torso.
        float PlayerDistance(Vector3 p)
        {
            Vector3 head = m_Target.Head.position;
            Vector3 chest = head + Vector3.down * k_TorsoLength;
            Vector3 segment = chest - head;
            float t = Mathf.Clamp01(Vector3.Dot(p - head, segment) / segment.sqrMagnitude);
            return (p - (head + segment * t)).magnitude;
        }
    }
}
