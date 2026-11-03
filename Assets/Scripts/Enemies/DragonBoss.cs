using RuneArena.Effects;
using RuneArena.Spells;
using UnityEngine;

namespace RuneArena.Enemies
{
    // The big dragon that closes the even stages. The dive is its plain blow; each time its fury fills it
    // lets out the next of two attacks from the air, in turn: spit -> rain.
    //   Spit: a few fireballs at the player, one after the other. An earth wall stops them.
    //   Rain: fireballs shot up that come down on marked spots around the player, one right under them,
    //         so standing still isn't an option.
    public class DragonBoss : FlyingEnemy
    {
        enum Attack
        {
            Spit,
            Rain,
        }

        static readonly Attack[] k_Pattern = { Attack.Spit, Attack.Rain };

        [Header("Spit")]
        [SerializeField] EnemyProjectile m_Projectile;
        [SerializeField] int m_SpitCount = 3;
        [SerializeField] float m_SpitWindUp = 1f;
        [SerializeField] float m_SpitGap = 0.35f;
        [SerializeField] float m_SpitSpeed = 9f;
        [SerializeField] float m_SpitDamage = 12f;
        [Tooltip("Degrees each shot can stray from the player, so not all of them hit if they stand still.")]
        [SerializeField] float m_SpitSpread = 4f;

        [Header("Rain")]
        [SerializeField] int m_RainCount = 6;
        [SerializeField] float m_RainWindUp = 1.5f;
        [Tooltip("Seconds from the fireballs going up to them landing, while the spots are marked.")]
        [SerializeField] float m_RainWarning = 1.8f;
        [SerializeField] float m_RainRadius = 1.6f;
        [SerializeField] float m_RainDamage = 20f;
        [Tooltip("How far from the player the other spots can be.")]
        [SerializeField] float m_RainSpread = 7f;
        [SerializeField] Color m_RainColor = new Color(1f, 0.35f, 0.1f);

        int m_Step;
        Attack m_Attack;
        float m_ActAt;
        int m_ShotsLeft;

        // Between attacks, not while winding one up or diving.
        protected override void Tick(float deltaTime)
        {
            if (!Telegraphing)
                BuildFury(deltaTime);
            base.Tick(deltaTime);
        }

        protected override bool StartSpecial(Vector3 head)
        {
            if (!FuryFull)
                return false;
            m_Attack = k_Pattern[m_Step++ % k_Pattern.Length];
            if (m_Attack == Attack.Spit && m_Projectile == null)
                return false;
            SpendFury();

            float windUp = m_Attack == Attack.Spit ? m_SpitWindUp : m_RainWindUp;
            m_ActAt = Time.time + windUp;
            m_ShotsLeft = m_SpitCount;
            BeginAttack(windUp);
            return true;
        }

        protected override bool SpecialTick(Vector3 head, float deltaTime)
        {
            if (Time.time < m_ActAt)
                return true;

            if (m_Attack == Attack.Rain)
            {
                Rain();
                return false;
            }

            Spit(head);
            m_ShotsLeft--;
            m_ActAt = Time.time + m_SpitGap;
            return m_ShotsLeft > 0;
        }

        // Where the fire comes out: in front of the body, about where the head is. The body's height over
        // the pivot grows with the model, so it measures how big it is.
        Vector3 Mouth => Body + transform.forward * ((Body - transform.position).magnitude * 3f);

        void Spit(Vector3 head)
        {
            Vector3 from = Mouth;
            Vector3 direction = Quaternion.Euler(Random.Range(-m_SpitSpread, m_SpitSpread), Random.Range(-m_SpitSpread, m_SpitSpread), 0f)
                                * (head - from).normalized;
            EnemyProjectile shot = Instantiate(m_Projectile, from, Quaternion.LookRotation(direction));
            var ball = shot.GetComponent<Renderer>();
            if (ball != null && Fx.Library != null && Fx.Library.FireCore != null)
                ball.sharedMaterial = Fx.Library.FireCore;
            SpellFx.FireballTrail(shot.transform, 0.25f);
            shot.Launch(direction * m_SpitSpeed, m_SpitDamage * DamageMultiplier, Player);
        }

        void Rain()
        {
            Vector3 feet = Player.Feet;
            for (int i = 0; i < m_RainCount; i++)
            {
                // The first one where the player stands; the rest around them, kept off the wall.
                Vector3 spot = feet;
                if (i > 0)
                {
                    Vector2 offset = Random.insideUnitCircle * m_RainSpread;
                    spot += new Vector3(offset.x, 0f, offset.y);
                }
                spot = KeepInside(spot);
                spot.y = ArenaCenter.y;
                // A little apart in time too, so they land as a rattle and not all at once.
                MeteorStrike.Launch(Mouth, spot, m_RainWarning + i * 0.12f, m_RainRadius, m_RainDamage * DamageMultiplier, Player, m_RainColor);
            }
        }
    }
}
