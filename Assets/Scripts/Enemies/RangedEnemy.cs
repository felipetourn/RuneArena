using UnityEngine;
using UnityEngine.AI;

namespace RuneArena.Enemies
{
    // Keeps its distance and shoots at the player's head when it has a clear line. With both elemental
    // projectiles set, each one is born a fire or an ice mage, half and half, and glows its color so the
    // player knows what is coming: fire explodes, ice does less but slows them down.
    public class RangedEnemy : GroundEnemy
    {
        /// <summary>What a ranged mage throws.</summary>
        public enum Element
        {
            Plain,
            Fire,
            Ice,
        }

        [SerializeField] float m_MinDistance = 6f;
        [SerializeField] float m_MaxDistance = 14f;

        [Header("Attack")]
        [Tooltip("Thrown when no elemental projectiles are set.")]
        [SerializeField] EnemyProjectile m_Projectile;
        [SerializeField] Transform m_Muzzle;
        [SerializeField] float m_Damage = 10f;
        [SerializeField] float m_ProjectileSpeed = 6f;
        [SerializeField] float m_WindUp = 1.2f;
        [SerializeField] float m_Cooldown = 3.5f;
        [Tooltip("What blocks its line of sight.")]
        [SerializeField] LayerMask m_SightMask;

        [Header("Elements")]
        [SerializeField] EnemyProjectile m_FireProjectile;
        [SerializeField] EnemyProjectile m_IceProjectile;
        [Tooltip("Damage of an ice shot. Fire shots do Damage, in their explosion.")]
        [SerializeField] float m_IceDamage = 7f;
        [SerializeField] Color m_FireTint = new Color(1f, 0.85f, 0.75f);
        [SerializeField] Color m_FireGlow = new Color(0.35f, 0.1f, 0.02f);
        [SerializeField] Color m_IceTint = new Color(0.8f, 0.9f, 1f);
        [SerializeField] Color m_IceGlow = new Color(0.05f, 0.18f, 0.35f);

        float m_FireAt = -1f;
        float m_ReadyAt;

        public Element Kind { get; private set; }

        protected override void Start()
        {
            base.Start();
            m_ReadyAt = Time.time + Random.Range(0f, m_Cooldown);
            if (m_FireProjectile != null && m_IceProjectile != null)
                SetElement(Random.value < 0.5f ? Element.Fire : Element.Ice);
        }

        /// <summary>Makes it a fire or ice mage: what it throws and the color it glows.</summary>
        public void SetElement(Element element)
        {
            Kind = element;
            if (element == Element.Fire)
                SetLook(m_FireTint, m_FireGlow);
            else if (element == Element.Ice)
                SetLook(m_IceTint, m_IceGlow);
        }

        EnemyProjectile Shot => Kind == Element.Fire ? m_FireProjectile : Kind == Element.Ice ? m_IceProjectile : m_Projectile;
        float ShotDamage => Kind == Element.Ice ? m_IceDamage : m_Damage;

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
                // Through BeginAttack, so its animator plays the cast in time with the shot.
                BeginAttack(m_WindUp);
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
            EnemyProjectile projectile = Instantiate(Shot, from, Quaternion.LookRotation(direction));
            projectile.Launch(direction * m_ProjectileSpeed, ShotDamage * DamageMultiplier, Player);
        }

        protected override void Interrupt()
        {
            base.Interrupt();
            m_FireAt = -1f;
            m_ReadyAt = Time.time + m_Cooldown * 0.5f;
        }
    }
}
