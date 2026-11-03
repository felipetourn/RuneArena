using RuneArena.Effects;
using RuneArena.Spells;
using UnityEngine;
using UnityEngine.AI;

namespace RuneArena.Enemies
{
    // The rift mage that closes the second stage. It keeps its distance and fights from afar, throwing
    // bolts at the player. Each time its fury fills it lets out the next move of a fixed cycle:
    //   rift -> orb
    //   Bolt: one quick shot at the player, a different hand thrust each time. Its plain blow.
    //   Rift: it strikes the floor with its staff and rifts open under and around the player, marked a
    //         moment before they burst.
    //   Orb: one big slow shot that hits hard. An earth wall stops it, like the bolts.
    // Whenever the player gets close it steps through a portal to the other side of the arena. Once, below
    // half health, it levitates out of reach and rifts burst all over the arena around the player.
    public class MageBoss : GroundEnemy
    {
        enum Move
        {
            Bolt,
            Rift,
            Orb,
        }

        enum State
        {
            Ready,
            Casting,
            Blinking,
            Storm,
        }

        static readonly Move[] k_Pattern = { Move.Rift, Move.Orb };

        public override bool IsBoss => true;

        [Header("Distance")]
        [Tooltip("Where it likes to stand from the player. It runs to keep about there.")]
        [SerializeField] float m_Distance = 9f;
        [Tooltip("Closer than this, it steps away through a portal.")]
        [SerializeField] float m_TooClose = 5f;
        [SerializeField] float m_BlinkCooldown = 6f;
        [Tooltip("Seconds into the portal step where it is gone and comes out the other side.")]
        [SerializeField] float m_BlinkAt = 0.5f;
        [SerializeField] float m_BlinkLength = 0.9f;
        [Tooltip("Seconds between two of its casts, plain or not.")]
        [SerializeField] float m_Rest = 1.2f;
        [Tooltip("Where its shots leave from, in front of its hands, from its feet.")]
        [SerializeField] Vector3 m_Hands = new Vector3(0f, 1.4f, 0.6f);

        [Header("Bolt")]
        [SerializeField] EnemyProjectile m_Bolt;
        [SerializeField] float m_BoltWindUp = 0.5f;
        [SerializeField] float m_BoltSpeed = 10f;
        [SerializeField] float m_BoltDamage = 10f;

        [Header("Rift")]
        [SerializeField] float m_RiftWindUp = 0.7f;
        [SerializeField] int m_Rifts = 3;
        [Tooltip("Seconds between a rift being marked and bursting.")]
        [SerializeField] float m_RiftDelay = 1.3f;
        [SerializeField] float m_RiftRadius = 1.8f;
        [SerializeField] float m_RiftSpread = 4f;
        [SerializeField] float m_RiftDamage = 18f;

        [Header("Orb")]
        [SerializeField] EnemyProjectile m_Orb;
        [SerializeField] float m_OrbWindUp = 0.8f;
        [SerializeField] float m_OrbSpeed = 4.5f;
        [SerializeField] float m_OrbDamage = 25f;

        [Header("Storm")]
        [SerializeField, Range(0f, 1f)] float m_StormHealth = 0.5f;
        [Tooltip("Length of the levitation clip it plays meanwhile, its own.")]
        [SerializeField] float m_StormTime = 8f;
        [SerializeField] float m_StormGap = 0.45f;
        [SerializeField] float m_StormSpread = 7f;
        [Tooltip("How high it floats, out of reach, while the storm lasts.")]
        [SerializeField] float m_StormHeight = 1.5f;

        [SerializeField] Color m_RiftColor = new Color(0.5f, 0.3f, 1f);

        State m_State;
        Move m_Move;
        int m_Step;
        int m_BoltCount;
        float m_Until;
        float m_NextAt;
        float m_BlinkReadyAt;
        bool m_Blinked;
        bool m_Stormed;
        Transform m_Floater;

        protected override void Start()
        {
            base.Start();
            m_Until = Time.time + m_Rest;
            var animator = GetComponentInChildren<Animator>();
            m_Floater = animator != null ? animator.transform : null;
        }

        protected override void Think(float deltaTime)
        {
            Vector3 feet = Player.Feet;
            float distance = FlatDistance(transform.position, feet);
            if (!m_Stormed && Health <= MaxHealth * m_StormHealth)
            {
                Storm();
                return;
            }

            switch (m_State)
            {
                case State.Ready:
                    if (distance < m_TooClose && Time.time >= m_BlinkReadyAt)
                    {
                        Blink();
                        return;
                    }
                    Keep(feet, distance);
                    BuildFury(deltaTime);
                    if (Time.time < m_Until)
                        break;
                    if (FuryFull)
                    {
                        SpendFury();
                        Begin(k_Pattern[m_Step++ % k_Pattern.Length]);
                    }
                    else
                    {
                        Begin(Move.Bolt);
                    }
                    break;

                case State.Casting:
                    Stop();
                    FaceTowards(feet, 360f, deltaTime);
                    if (Time.time >= m_NextAt)
                        Cast(feet);
                    break;

                case State.Blinking:
                    Stop();
                    if (!m_Blinked && Time.time >= m_NextAt)
                    {
                        m_Blinked = true;
                        Portal(transform.position);
                        Agent.Warp(Away(feet));
                        Portal(transform.position);
                    }
                    if (Time.time >= m_Until)
                        End();
                    break;

                case State.Storm:
                    UpdateStorm(feet);
                    break;
            }
        }

        // Runs to stay about its distance from the player: nearer if too far, back if too near.
        void Keep(Vector3 feet, float distance)
        {
            if (Mathf.Abs(distance - m_Distance) < 2f)
            {
                Stop();
                FaceTowards(feet, 240f, Time.deltaTime);
                return;
            }
            Vector3 from = transform.position - feet;
            from.y = 0f;
            from = from.sqrMagnitude > 0.01f ? from.normalized : transform.forward;
            MoveTo(feet + from * m_Distance);
        }

        void Begin(Move move)
        {
            m_Move = move;
            m_State = State.Casting;
            float windUp = move == Move.Bolt ? m_BoltWindUp : move == Move.Rift ? m_RiftWindUp : m_OrbWindUp;
            string name = move == Move.Bolt ? $"Bolt{m_BoltCount++ % 3 + 1}" : move.ToString();
            m_NextAt = Time.time + windUp;
            BeginAttack(windUp, name);
        }

        void Cast(Vector3 feet)
        {
            switch (m_Move)
            {
                case Move.Bolt:
                    Shoot(m_Bolt, m_BoltSpeed, m_BoltDamage, 0.12f, LandedBlow);
                    break;
                case Move.Orb:
                    Shoot(m_Orb, m_OrbSpeed, m_OrbDamage, 0.3f);
                    break;
                case Move.Rift:
                    for (int i = 0; i < m_Rifts; i++)
                    {
                        Vector2 offset = i == 0 ? Vector2.zero : Random.insideUnitCircle * m_RiftSpread;
                        Rift(KeepInside(feet + new Vector3(offset.x, 0f, offset.y)), m_RiftDelay + i * 0.15f);
                    }
                    break;
            }

            End();
        }

        void Shoot(EnemyProjectile prefab, float speed, float damage, float glow, System.Action onHit = null)
        {
            if (prefab == null)
                return;
            Vector3 from = transform.position + transform.rotation * m_Hands;
            Vector3 direction = (Player.Head.position + Vector3.down * 0.3f - from).normalized;
            EnemyProjectile shot = Instantiate(prefab, from, Quaternion.LookRotation(direction));
            if (Fx.Library != null)
            {
                Fx.Attach(shot.transform, new FxBurst
                {
                    Material = Fx.Library.Glow,
                    Color = m_RiftColor,
                    Count = 40,
                    Size = glow,
                    Lifetime = 0.35f,
                    Radius = glow * 0.3f,
                    Growth = FxGrowth.Shrink,
                }, 0f, 14f);
            }
            shot.Launch(direction * speed, damage * DamageMultiplier, Player, onHit);
        }

        // Marked now, bursting after delay.
        void Rift(Vector3 point, float delay)
        {
            DangerZone.Show(point, m_RiftRadius, m_RiftColor, delay);
            StartCoroutine(Burst(point, delay));
        }

        System.Collections.IEnumerator Burst(Vector3 point, float delay)
        {
            yield return new WaitForSeconds(delay);
            SpellFx.LightningImpact(point, m_RiftColor, 0.6f);
            if (Player != null && !Player.IsDead && FlatDistance(Player.Feet, point) <= m_RiftRadius)
                Player.Damage(m_RiftDamage * DamageMultiplier);
        }

        void Blink()
        {
            m_State = State.Blinking;
            m_Blinked = false;
            m_NextAt = Time.time + m_BlinkAt;
            m_Until = Time.time + m_BlinkLength;
            m_BlinkReadyAt = Time.time + m_BlinkCooldown;
            PlayAction("Blink", m_BlinkLength);
        }

        // Across the player from where it stands, at its distance, kept in the arena.
        Vector3 Away(Vector3 feet)
        {
            Vector3 side = feet - transform.position;
            side.y = 0f;
            side = side.sqrMagnitude > 0.01f ? side.normalized : -transform.forward;
            Vector3 spot = KeepInside(feet + side * m_Distance);
            return NavMesh.SamplePosition(spot, out NavMeshHit hit, 3f, NavMesh.AllAreas) ? hit.position : transform.position;
        }

        void Portal(Vector3 ground)
        {
            SpellFx.Summoning(ground, m_RiftColor, 0.6f, 1.2f);
        }

        void End()
        {
            Telegraphing = false;
            m_State = State.Ready;
            m_Until = Time.time + m_Rest;
        }

        // Floats up out of reach and rifts burst around the player for the whole levitation.
        void Storm()
        {
            m_Stormed = true;
            Telegraphing = false;
            Invulnerable = true;
            m_State = State.Storm;
            m_Until = Time.time + m_StormTime;
            m_NextAt = Time.time + 1f;
            PlayAction("Levitate", m_StormTime);
            Portal(transform.position);
        }

        void UpdateStorm(Vector3 feet)
        {
            Stop();
            float t = Mathf.Clamp01(1f - (m_Until - Time.time) / m_StormTime);
            if (m_Floater != null)
            {
                // Up over the first second, down over the last.
                float lift = Mathf.Min(Mathf.Clamp01(t * m_StormTime), Mathf.Clamp01((1f - t) * m_StormTime));
                Vector3 p = m_Floater.localPosition;
                p.y = lift * m_StormHeight;
                m_Floater.localPosition = p;
            }
            if (Time.time >= m_NextAt && Time.time < m_Until - 1.5f)
            {
                Vector2 offset = Random.insideUnitCircle * m_StormSpread;
                Rift(KeepInside(feet + new Vector3(offset.x, 0f, offset.y)), m_RiftDelay);
                m_NextAt = Time.time + m_StormGap;
            }
            if (Time.time >= m_Until)
            {
                Invulnerable = false;
                End();
            }
        }

        protected override void Interrupt()
        {
            base.Interrupt();
            if (m_State == State.Casting)
                End();
        }
    }
}
