using UnityEngine;
using UnityEngine.AI;

namespace RuneArena.Enemies
{
    // Keeps its distance and shoots at the player's head when it has a clear line.
    public class RangedEnemy : GroundEnemy
    {
        [SerializeField] float m_MinDistance = 6f;
        [SerializeField] float m_MaxDistance = 14f;

        [Header("Attack")]
        [SerializeField] EnemyProjectile m_Projectile;
        [SerializeField] Transform m_Muzzle;
        [SerializeField] float m_Damage = 10f;
        [SerializeField] float m_ProjectileSpeed = 6f;
        [SerializeField] float m_WindUp = 1.2f;
        [SerializeField] float m_Cooldown = 3.5f;
        [Tooltip("What blocks its line of sight.")]
        [SerializeField] LayerMask m_SightMask;

        float m_FireAt = -1f;
        float m_ReadyAt;

        protected override void Start()
        {
            base.Start();
            m_ReadyAt = Time.time + Random.Range(0f, m_Cooldown);
        }

        protected override void Think(float deltaTime)
        {
            Vector3 feet = Player.Feet;
            Vector3 head = Player.Head.position;
            float distance = FlatDistance(transform.position, feet);

            if (m_FireAt >= 0f)
            {
                Stop();
                FaceTowards(feet, 360f, deltaTime);
                if (Time.time >= m_FireAt)
                {
                    Fire(head);
                    m_FireAt = -1f;
                    m_ReadyAt = Time.time + m_Cooldown;
                    Telegraphing = false;
                }
                return;
            }

            bool canSee = !Physics.Linecast(m_Muzzle.position, head, m_SightMask, QueryTriggerInteraction.Ignore);

            if (distance > m_MaxDistance || !canSee)
            {
                MoveTo(feet);
                return;
            }

            if (distance < m_MinDistance && TryRetreat(feet, distance))
                return;

            Stop();
            FaceTowards(feet, 360f, deltaTime);
            if (Time.time >= m_ReadyAt)
            {
                m_FireAt = Time.time + m_WindUp;
                Telegraphing = true;
            }
        }

        bool TryRetreat(Vector3 feet, float distance)
        {
            Vector3 away = transform.position - feet;
            away.y = 0f;
            Vector3 target = transform.position + away.normalized * (m_MinDistance - distance + 2f);
            if (!NavMesh.SamplePosition(target, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                return false;
            if (FlatDistance(hit.position, feet) <= distance + 0.5f)
                return false;
            MoveTo(hit.position);
            return true;
        }

        void Fire(Vector3 target)
        {
            Vector3 from = m_Muzzle.position;
            Vector3 direction = (target - from).normalized;
            EnemyProjectile projectile = Instantiate(m_Projectile, from, Quaternion.LookRotation(direction));
            projectile.Launch(direction * m_ProjectileSpeed, m_Damage, Player);
        }

        protected override void Interrupt()
        {
            base.Interrupt();
            m_FireAt = -1f;
            m_ReadyAt = Time.time + m_Cooldown * 0.5f;
        }
    }
}
