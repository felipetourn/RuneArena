using System;
using RuneArena.Player;
using RuneArena.Spells;
using UnityEngine;

namespace RuneArena.Enemies
{
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

        Vector3 m_Velocity;
        float m_Damage;
        PlayerHealth m_Target;
        Action m_OnHit;
        float m_BaseScale;

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
            Destroy(gameObject, m_Lifetime);
        }

        void Update()
        {
            Vector3 step = m_Velocity * Time.deltaTime;
            Vector3 from = transform.position;
            if (Physics.Raycast(from, m_Velocity, out RaycastHit hit, step.magnitude, m_BlockMask, QueryTriggerInteraction.Ignore))
            {
                if (m_ExplosionRadius > 0f)
                    Explode(hit.point);
                Destroy(gameObject);
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
                    m_OnHit?.Invoke();
                }
                Destroy(gameObject);
            }
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
