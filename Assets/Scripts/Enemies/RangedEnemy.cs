using System.Collections.Generic;
using RuneArena.Effects;
using RuneArena.Spells;
using UnityEngine;
using UnityEngine.AI;

namespace RuneArena.Enemies
{
    // Keeps its distance and shoots at the player's head when it has a clear line. With both elemental
    // projectiles set, each one is born a fire or an ice mage, half and half, and looks it so the player
    // knows what is coming: fire explodes, ice does less but slows them down. A fire mage's hands and skull
    // burn; an ice mage grows crystals on its head and shoulders and frost falls from its hands. While it
    // winds up a shot, its element gathers between its hands.
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
        readonly List<ParticleSystem> m_Aura = new List<ParticleSystem>();
        ParticleSystem m_Charge;

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
            Dress(element);
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
                    EndCharge();
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
                m_Charge = Fx.Attach(m_Muzzle, Charge(Kind), 40f, 0f);
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
            EndCharge();
            m_FireAt = -1f;
            m_ReadyAt = Time.time + m_Cooldown * 0.5f;
        }

        protected override void OnDied()
        {
            base.OnDied();
            EndCharge();
            foreach (ParticleSystem ps in m_Aura)
                Fx.Release(ps);
            m_Aura.Clear();
        }

        void EndCharge()
        {
            Fx.Release(m_Charge);
            m_Charge = null;
        }

        void Dress(Element element)
        {
            FxLibrary fx = Fx.Library;
            Animator body = GetComponentInChildren<Animator>();
            if (fx == null || body == null || !body.isHuman)
                return;
            Transform head = body.GetBoneTransform(HumanBodyBones.Head);
            Transform[] hands = { body.GetBoneTransform(HumanBodyBones.LeftHand), body.GetBoneTransform(HumanBodyBones.RightHand) };

            if (element == Element.Fire)
            {
                foreach (Transform hand in hands)
                    Wear(hand, Flame(fx, 0.18f, 0.04f), 14f);
                Wear(head, Flame(fx, 0.3f, 0.08f), 18f);
            }
            else if (element == Element.Ice)
            {
                foreach (Transform hand in hands)
                    Wear(hand, Frost(fx), 10f);
                if (fx.Ice != null)
                {
                    Crown(fx.Ice, head);
                    Spikes(fx.Ice, body.GetBoneTransform(HumanBodyBones.LeftUpperArm), -1f);
                    Spikes(fx.Ice, body.GetBoneTransform(HumanBodyBones.RightUpperArm), 1f);
                }
            }
        }

        void Wear(Transform bone, in FxBurst burst, float perSecond)
        {
            if (bone == null)
                return;
            ParticleSystem ps = Fx.Attach(bone, burst, perSecond, 0f);
            if (ps != null)
                m_Aura.Add(ps);
        }

        // Three crystals on top of the skull, fanned out sideways.
        void Crown(Material ice, Transform head)
        {
            if (head == null)
                return;
            Vector3 top = head.position + transform.up * 0.18f;
            for (int i = -1; i <= 1; i++)
            {
                Vector3 direction = Quaternion.AngleAxis(i * 28f, transform.forward) * (transform.up - transform.forward * 0.25f);
                Crystal(ice, head, top + direction.normalized * 0.04f, direction, i == 0 ? 0.26f : 0.2f);
            }
        }

        // Two crystals leaning out of a shoulder; side is -1 for the left one.
        void Spikes(Material ice, Transform shoulder, float side)
        {
            if (shoulder == null)
                return;
            Vector3 outward = transform.right * side;
            Crystal(ice, shoulder, shoulder.position + transform.up * 0.05f, transform.up + outward * 0.7f - transform.forward * 0.2f, 0.3f);
            Crystal(ice, shoulder, shoulder.position + outward * 0.06f - transform.forward * 0.05f, transform.up * 0.6f + outward - transform.forward * 0.4f, 0.22f);
        }

        // The icicle points along its z; size is about its length in meters.
        static void Crystal(Material ice, Transform bone, Vector3 position, Vector3 direction, float size)
        {
            GameObject go = RuneSpell.CreateVisual(PrimitiveType.Cube, "Ice Crystal", ice, new Color(0.75f, 0.92f, 1f));
            go.GetComponent<MeshFilter>().sharedMesh = SpellMeshes.Icicle;
            go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction.normalized));
            go.transform.localScale = Vector3.one * size;
            go.transform.SetParent(bone, true);
        }

        static FxBurst Flame(FxLibrary fx, float size, float radius)
        {
            return new FxBurst
            {
                Material = fx.FireSheet,
                Color = new Color(1f, 0.8f, 0.6f, 0.9f),
                Count = 12,
                Size = size,
                Speed = 0.35f,
                Lifetime = 0.45f,
                Gravity = -0.2f,
                Radius = radius,
                Animated = true,
                Growth = FxGrowth.Swell,
            };
        }

        static FxBurst Frost(FxLibrary fx)
        {
            return new FxBurst
            {
                Material = fx.Puff,
                Color = new Color(0.8f, 0.92f, 1f, 0.5f),
                Count = 10,
                Size = 0.15f,
                Speed = 0.1f,
                Lifetime = 0.9f,
                Gravity = 0.05f,
                Radius = 0.05f,
                Growth = FxGrowth.Swell,
            };
        }

        static FxBurst Charge(Element element)
        {
            FxLibrary fx = Fx.Library;
            if (fx == null || element == Element.Plain)
                return default;
            return element == Element.Fire
                ? new FxBurst
                {
                    Material = fx.FireSheet,
                    Color = new Color(1f, 0.75f, 0.5f, 0.95f),
                    Count = 10,
                    Size = 0.25f,
                    Speed = 0.1f,
                    Lifetime = 0.3f,
                    Radius = 0.05f,
                    Animated = true,
                    Growth = FxGrowth.Expand,
                }
                : new FxBurst
                {
                    Material = fx.Glow,
                    Color = new Color(0.75f, 0.92f, 1f, 0.9f),
                    Count = 10,
                    Size = 0.12f,
                    Speed = 0.15f,
                    Lifetime = 0.35f,
                    Radius = 0.08f,
                    Growth = FxGrowth.Shrink,
                };
        }
    }
}
