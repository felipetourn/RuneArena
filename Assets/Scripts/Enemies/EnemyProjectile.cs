using RuneArena.Player;
using UnityEngine;

namespace RuneArena.Enemies
{
    // Flies straight. Hits the player's head or torso, and stops at walls and the floor,
    // so an earth wall in the way blocks it.
    public class EnemyProjectile : MonoBehaviour
    {
        const float k_TorsoLength = 0.7f;

        [SerializeField] float m_HitRadius = 0.3f;
        [SerializeField] float m_Lifetime = 5f;
        [SerializeField] LayerMask m_BlockMask;

        Vector3 m_Velocity;
        float m_Damage;
        PlayerHealth m_Target;

        public void Launch(Vector3 velocity, float damage, PlayerHealth target)
        {
            m_Velocity = velocity;
            m_Damage = damage;
            m_Target = target;
            Destroy(gameObject, m_Lifetime);
        }

        void Update()
        {
            Vector3 step = m_Velocity * Time.deltaTime;
            Vector3 from = transform.position;
            if (Physics.Raycast(from, m_Velocity, step.magnitude, m_BlockMask, QueryTriggerInteraction.Ignore))
            {
                Destroy(gameObject);
                return;
            }

            transform.position = from + step;

            if (m_Target != null && HitsPlayer(transform.position))
            {
                m_Target.Damage(m_Damage);
                Destroy(gameObject);
            }
        }

        bool HitsPlayer(Vector3 p)
        {
            Vector3 head = m_Target.Head.position;
            Vector3 chest = head + Vector3.down * k_TorsoLength;
            Vector3 segment = chest - head;
            float t = Mathf.Clamp01(Vector3.Dot(p - head, segment) / segment.sqrMagnitude);
            return (p - (head + segment * t)).sqrMagnitude <= m_HitRadius * m_HitRadius;
        }
    }
}
