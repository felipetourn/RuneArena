using RuneArena.Spells;
using RuneArena.Strokes;
using UnityEngine;

namespace RuneArena.Enemies
{
    // Slow and tough. Close up it slams the floor around itself after a long wind-up, marked by a ring;
    // from afar it throws a fan of shots that pillars and earth walls can block.
    // Below half health it gets angry: faster, with shorter cooldowns.
    public class BossEnemy : GroundEnemy
    {
        static Material s_Material;

        [Header("Slam")]
        [SerializeField] float m_SlamRange = 3f;
        [SerializeField] float m_SlamRadius = 3.5f;
        [SerializeField] float m_SlamDamage = 30f;
        [SerializeField] float m_SlamWindUp = 1.5f;
        [SerializeField] float m_SlamCooldown = 3f;
        [SerializeField] Color m_SlamColor = new Color(0.8f, 0.1f, 0.1f);

        [Header("Volley")]
        [SerializeField] EnemyProjectile m_Projectile;
        [SerializeField] Transform m_Muzzle;
        [SerializeField] int m_Shots = 3;
        [Tooltip("Degrees between shots in the fan.")]
        [SerializeField] float m_Spread = 12f;
        [SerializeField] float m_ShotDamage = 12f;
        [SerializeField] float m_ShotSpeed = 7f;
        [SerializeField] float m_VolleyWindUp = 1.2f;
        [SerializeField] float m_VolleyCooldown = 4.5f;
        [Tooltip("Only throws when the player is farther than this.")]
        [SerializeField] float m_VolleyMinDistance = 6f;

        [Header("Anger")]
        [SerializeField, Range(0f, 1f)] float m_AngerHealth = 0.5f;
        [SerializeField] float m_AngerSpeed = 1.3f;
        [SerializeField] float m_AngerCooldown = 0.7f;

        enum Attack
        {
            None,
            Slam,
            Volley,
        }

        Attack m_Attack;
        float m_AttackAt;
        float m_SlamReadyAt;
        float m_VolleyReadyAt;
        GameObject m_Ring;

        bool Angry => Health <= MaxHealth * m_AngerHealth;
        float CooldownScale => Angry ? m_AngerCooldown : 1f;
        protected override float SpeedBonus => Angry ? m_AngerSpeed : 1f;

        protected override void Start()
        {
            base.Start();
            m_VolleyReadyAt = Time.time + m_VolleyCooldown * 0.5f;
        }

        protected override void Think(float deltaTime)
        {
            Vector3 feet = Player.Feet;
            float distance = FlatDistance(transform.position, feet);

            if (m_Attack != Attack.None)
            {
                Stop();
                FaceTowards(feet, 180f, deltaTime);
                if (Time.time >= m_AttackAt)
                    Release(feet, distance);
                return;
            }

            if (distance <= m_SlamRange && Time.time >= m_SlamReadyAt)
            {
                Begin(Attack.Slam, m_SlamWindUp);
                return;
            }

            if (distance >= m_VolleyMinDistance && Time.time >= m_VolleyReadyAt && m_Projectile != null)
            {
                Begin(Attack.Volley, m_VolleyWindUp);
                return;
            }

            if (distance <= m_SlamRange * 0.8f)
            {
                Stop();
                FaceTowards(feet, 180f, deltaTime);
            }
            else
            {
                MoveTo(feet);
            }
        }

        void Begin(Attack attack, float windUp)
        {
            m_Attack = attack;
            m_AttackAt = Time.time + windUp;
            Telegraphing = true;
            if (attack == Attack.Slam)
                ShowRing();
        }

        void Release(Vector3 feet, float distance)
        {
            if (m_Attack == Attack.Slam)
            {
                // Out of the ring by the time it lands: no damage.
                if (distance <= m_SlamRadius)
                    Player.Damage(m_SlamDamage * DamageMultiplier);
                m_SlamReadyAt = Time.time + m_SlamCooldown * CooldownScale;
                HideRing(true);
            }
            else
            {
                Volley();
                m_VolleyReadyAt = Time.time + m_VolleyCooldown * CooldownScale;
            }

            m_Attack = Attack.None;
            Telegraphing = false;
        }

        void Volley()
        {
            Vector3 from = m_Muzzle != null ? m_Muzzle.position : transform.position + Vector3.up * 2f;
            Vector3 aim = (Player.Head.position - from).normalized;
            float start = -m_Spread * (m_Shots - 1) * 0.5f;
            for (int i = 0; i < m_Shots; i++)
            {
                Vector3 direction = Quaternion.AngleAxis(start + i * m_Spread, Vector3.up) * aim;
                EnemyProjectile shot = Instantiate(m_Projectile, from, Quaternion.LookRotation(direction));
                shot.Launch(direction * m_ShotSpeed, m_ShotDamage * DamageMultiplier, Player);
            }
        }

        void ShowRing()
        {
            if (s_Material == null)
            {
                var recorder = FindAnyObjectByType<StrokeRecorder>(FindObjectsInactive.Include);
                s_Material = recorder != null ? recorder.LineMaterial : null;
            }
            m_Ring = RuneSpell.CreateVisual(PrimitiveType.Cylinder, "Slam Ring", s_Material, m_SlamColor);
            m_Ring.transform.SetParent(transform, false);
            m_Ring.transform.localPosition = Vector3.up * 0.02f;
            m_Ring.transform.localScale = new Vector3(m_SlamRadius * 2f, 0.005f, m_SlamRadius * 2f);
        }

        void HideRing(bool slammed)
        {
            if (m_Ring == null)
                return;
            if (slammed)
            {
                // Flash outwards, detached so it isn't hidden by the boss turning.
                m_Ring.transform.SetParent(null, true);
                Vector3 size = m_Ring.transform.localScale;
                SpellVisual.Spawn(m_Ring, size, new Vector3(size.x * 1.15f, size.y, size.z * 1.15f), 0.15f);
            }
            else
            {
                Destroy(m_Ring);
            }
            m_Ring = null;
        }

        protected override void Interrupt()
        {
            base.Interrupt();
            if (m_Attack == Attack.Slam)
                m_SlamReadyAt = Time.time + m_SlamCooldown * 0.5f;
            else if (m_Attack == Attack.Volley)
                m_VolleyReadyAt = Time.time + m_VolleyCooldown * 0.5f;
            m_Attack = Attack.None;
            HideRing(false);
        }
    }
}
