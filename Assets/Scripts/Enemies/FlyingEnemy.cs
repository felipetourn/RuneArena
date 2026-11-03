using RuneArena.Arena;
using RuneArena.Player;
using UnityEngine;

namespace RuneArena.Enemies
{
    // Circles the player in the air, then dives at the head after a wind-up and climbs back.
    // Doesn't use the NavMesh, so earth walls don't stop it; it steers around the arena's pillars.
    public class FlyingEnemy : Enemy
    {
        // The player's body for the dive: head to hips, this thick.
        const float k_TorsoLength = 0.8f;
        const float k_PlayerRadius = 0.3f;

        static readonly Collider[] s_Touching = new Collider[16];

        enum State
        {
            Enter,
            Orbit,
            WindUp,
            Dive,
            Recover,
            // One of a subclass's own attacks, hovering in place.
            Special,
        }

        [Header("Flight")]
        [SerializeField] float m_Speed = 3f;
        [SerializeField] float m_Acceleration = 6f;
        [Tooltip("Height above the arena floor while circling.")]
        [SerializeField] float m_Altitude = 3.5f;
        [SerializeField] float m_OrbitRadius = 7f;

        [Header("Entry")]
        [Tooltip("It comes in through a door at this height, then climbs.")]
        [SerializeField] float m_EntryHeight = 1f;
        [Tooltip("Seconds after getting inside before it starts climbing.")]
        [SerializeField] float m_ClimbDelay = 0.5f;

        [Header("Dive")]
        [SerializeField] float m_Damage = 10f;
        [SerializeField] float m_DiveSpeed = 6f;
        [SerializeField] float m_HitRadius = 0.6f;
        [SerializeField] float m_WindUp = 1.2f;
        [SerializeField] float m_Cooldown = 6f;
        [SerializeField] float m_RecoverTime = 1f;

        [Header("Pillars")]
        [Tooltip("Extra room it keeps from a pillar, besides the pillar's own size.")]
        [SerializeField] float m_PillarMargin = 0.5f;
        [Tooltip("Distance past that room at which it starts turning away.")]
        [SerializeField] float m_AvoidDistance = 2.5f;

        State m_State;
        float m_StateUntil;
        float m_ReadyAt;
        float m_OrbitDirection;
        Vector3 m_Velocity;
        Vector3 m_DiveTarget;
        float m_ArenaLimit = float.PositiveInfinity;
        bool m_Entered;
        float m_EnteredAt;
        Vector3[] m_Pillars = new Vector3[0];
        SphereCollider m_Collider;
        float m_PillarRadius;
        float m_PillarTop;

        public bool IsDiving => m_State == State.Dive;

        // Middle of the body: the collider's center, which sits above the pivot.
        protected Vector3 Body => m_Collider != null ? transform.TransformPoint(m_Collider.center) : transform.position;

        protected override void Start()
        {
            base.Start();
            m_Collider = GetComponent<SphereCollider>();
            // Put down in the arena (a boss's entrance) instead of flying in through a door.
            if (ArenaRadius > 0f && FlatDistance(transform.position, ArenaCenter) < ArenaRadius - 2f)
            {
                m_Entered = true;
                m_State = State.Orbit;
            }
            m_OrbitDirection = Random.value < 0.5f ? -1f : 1f;
            m_ReadyAt = Time.time + Random.Range(1f, m_Cooldown);

            if (ArenaRadius > 0f)
                m_ArenaLimit = ArenaRadius - 1f;

            var arena = FindAnyObjectByType<ColosseumBuilder>();
            if (arena != null && arena.PillarCount > 0)
            {
                m_Pillars = new Vector3[arena.PillarCount];
                for (int i = 0; i < m_Pillars.Length; i++)
                {
                    float angle = (arena.PillarAngle + i * 360f / arena.PillarCount) * Mathf.Deg2Rad;
                    var local = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * arena.PillarDistance;
                    m_Pillars[i] = arena.transform.TransformPoint(local);
                }
                // Square bases reach further out at the corners than the shaft's width.
                m_PillarRadius = arena.PillarWidth * 0.9f + m_PillarMargin;
                m_PillarTop = arena.transform.position.y + arena.PillarHeight + 0.5f;
            }
        }

        protected override void Tick(float deltaTime)
        {
            Vector3 head = Player.Head.position;

            switch (m_State)
            {
                case State.Enter:
                    var low = new Vector3(ArenaCenter.x, ArenaCenter.y + m_EntryHeight, ArenaCenter.z);
                    Steer(low, m_Speed * SpeedMultiplier, deltaTime);
                    if (m_Entered && Time.time >= m_EnteredAt + m_ClimbDelay)
                        m_State = State.Orbit;
                    break;

                case State.Orbit:
                    Steer(OrbitPoint(head), m_Speed * SpeedMultiplier, deltaTime);
                    if (Time.time >= m_ReadyAt)
                    {
                        if (StartSpecial(head))
                            m_State = State.Special;
                        else
                            Enter(State.WindUp, m_WindUp);
                    }
                    break;

                case State.Special:
                    m_Velocity = Vector3.MoveTowards(m_Velocity, Vector3.zero, m_Acceleration * deltaTime);
                    if (!SpecialTick(head, deltaTime))
                    {
                        Telegraphing = false;
                        m_State = State.Orbit;
                        m_ReadyAt = Time.time + m_Cooldown;
                    }
                    break;

                case State.WindUp:
                    m_Velocity = Vector3.MoveTowards(m_Velocity, Vector3.zero, m_Acceleration * deltaTime);
                    if (Time.time >= m_StateUntil)
                    {
                        m_DiveTarget = head;
                        Enter(State.Dive, 2f);
                    }
                    break;

                // Measured from the body, not the pivot under it, so what hits is what the player sees coming.
                // The whole stretch it covers this frame counts, so a fast dive can't skip over the player.
                case State.Dive:
                    Vector3 body = Body;
                    m_Velocity = (m_DiveTarget - body).normalized * (m_DiveSpeed * SpeedMultiplier);
                    if (TouchesPlayer(body, body + m_Velocity * deltaTime, head))
                    {
                        Player.Damage(m_Damage * DamageMultiplier);
                        LandedBlow();
                        Enter(State.Recover, m_RecoverTime);
                    }
                    else if ((body - m_DiveTarget).sqrMagnitude < 0.25f || Time.time >= m_StateUntil)
                    {
                        Enter(State.Recover, m_RecoverTime);
                    }
                    break;

                case State.Recover:
                    Vector3 away = transform.position - head;
                    away.y = 0f;
                    Steer(transform.position + away.normalized * 3f + Vector3.up * 3f, m_Speed * SpeedMultiplier, deltaTime);
                    if (Time.time >= m_StateUntil)
                    {
                        m_State = State.Orbit;
                        m_ReadyAt = Time.time + m_Cooldown;
                    }
                    break;
            }

            Move(m_Velocity * deltaTime);
            Vector3 look = m_State == State.WindUp || m_State == State.Dive || m_State == State.Special ? head - transform.position : m_Velocity;
            if (look.sqrMagnitude > 1e-4f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(look), 360f * deltaTime);
        }

        void Enter(State state, float duration)
        {
            m_State = state;
            m_StateUntil = Time.time + duration;
            Telegraphing = state == State.WindUp || state == State.Dive;
            if (state == State.WindUp)
                BeginAttack(duration);
        }

        Vector3 OrbitPoint(Vector3 head)
        {
            Vector3 offset = transform.position - head;
            offset.y = 0f;
            if (offset.sqrMagnitude < 0.01f)
                offset = Vector3.forward;
            offset.Normalize();
            Vector3 tangent = Vector3.Cross(Vector3.up, offset) * m_OrbitDirection;
            Vector3 point = head + offset * m_OrbitRadius + tangent * 3f;
            point.y = ArenaCenter.y + m_Altitude;
            return point;
        }

        void Steer(Vector3 target, float speed, float deltaTime)
        {
            Vector3 desired = Vector3.ClampMagnitude(target - transform.position, speed);
            desired = Vector3.ClampMagnitude(desired + AvoidPillars(desired, speed), speed);
            m_Velocity = Vector3.MoveTowards(m_Velocity, desired, m_Acceleration * deltaTime);
        }

        // Pushes away from nearby pillars and around them, toward whichever side it was already heading.
        Vector3 AvoidPillars(Vector3 desired, float speed)
        {
            Vector3 push = Vector3.zero;
            Vector3 position = transform.position;
            if (position.y > m_PillarTop)
                return push;

            foreach (Vector3 pillar in m_Pillars)
            {
                Vector3 away = position - pillar;
                away.y = 0f;
                float gap = away.magnitude - m_PillarRadius;
                if (gap > m_AvoidDistance || away.sqrMagnitude < 1e-6f)
                    continue;

                away.Normalize();
                float strength = 1f - Mathf.Clamp01(gap / m_AvoidDistance);
                Vector3 around = Vector3.Cross(Vector3.up, away);
                if (Vector3.Dot(around, desired) < 0f)
                    around = -around;
                push += (away + around) * (speed * strength);
            }
            return push;
        }

        void Move(Vector3 offset)
        {
            Vector3 p = transform.position + offset;
            p.y = Mathf.Clamp(p.y, ArenaCenter.y + 0.5f, Mathf.Max(ArenaCenter.y + m_Altitude + 3f, transform.position.y));

            // Kept inside the colosseum, but only once it is in: it spawns outside and comes through a door.
            Vector3 flat = p - ArenaCenter;
            flat.y = 0f;
            if (flat.magnitude <= m_ArenaLimit && !m_Entered)
            {
                m_Entered = true;
                m_EnteredAt = Time.time;
            }
            else if (m_Entered && flat.magnitude > m_ArenaLimit)
            {
                flat = flat.normalized * m_ArenaLimit;
                p = new Vector3(ArenaCenter.x + flat.x, p.y, ArenaCenter.z + flat.z);
            }

            // Never inside a pillar, whatever moved it: steering, a dive or a push.
            if (p.y <= m_PillarTop)
            {
                foreach (Vector3 pillar in m_Pillars)
                {
                    Vector3 away = p - pillar;
                    away.y = 0f;
                    if (away.sqrMagnitude >= m_PillarRadius * m_PillarRadius || away.sqrMagnitude < 1e-6f)
                        continue;
                    away = away.normalized * m_PillarRadius;
                    p = new Vector3(pillar.x + away.x, p.y, pillar.z + away.z);
                }
            }
            transform.position = p;
        }

        protected override void ApplyKnockback(Vector3 offset)
        {
            Move(offset);
        }

        // The player is their head and torso, and also whatever collider the rig has: its character
        // controller can be away from the head (leaning, the device simulator), and it is what a dive that
        // shoves the player aside touches.
        bool TouchesPlayer(Vector3 from, Vector3 to, Vector3 head)
        {
            Vector3 hips = head + Vector3.down * k_TorsoLength;
            if (SegmentDistance(from, to, head, hips) <= m_HitRadius + k_PlayerRadius)
                return true;
            int count = Physics.OverlapCapsuleNonAlloc(from, to, m_HitRadius, s_Touching, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (s_Touching[i].GetComponentInParent<PlayerHealth>() == Player)
                    return true;
            }
            return false;
        }

        // Closest distance between the segments a0-a1 and b0-b1.
        static float SegmentDistance(Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1)
        {
            Vector3 d1 = a1 - a0, d2 = b1 - b0, r = a0 - b0;
            float a = d1.sqrMagnitude, e = d2.sqrMagnitude, f = Vector3.Dot(d2, r);
            float s, t;
            if (a < 1e-8f)
            {
                s = 0f;
                t = e > 1e-8f ? Mathf.Clamp01(f / e) : 0f;
            }
            else
            {
                float c = Vector3.Dot(d1, r);
                if (e < 1e-8f)
                {
                    t = 0f;
                    s = Mathf.Clamp01(-c / a);
                }
                else
                {
                    float b = Vector3.Dot(d1, d2);
                    float denominator = a * e - b * b;
                    s = denominator > 1e-8f ? Mathf.Clamp01((b * f - c * e) / denominator) : 0f;
                    t = (b * s + f) / e;
                    if (t < 0f)
                    {
                        t = 0f;
                        s = Mathf.Clamp01(-c / a);
                    }
                    else if (t > 1f)
                    {
                        t = 1f;
                        s = Mathf.Clamp01((b - c) / a);
                    }
                }
            }
            return Vector3.Distance(a0 + d1 * s, b0 + d2 * t);
        }

        protected override void OnDied()
        {
            m_Velocity *= 0.3f;
        }

        // Falls out of the air and levels out, instead of shrinking where it was hit.
        protected override void DeadTick(float deltaTime)
        {
            m_Velocity += Physics.gravity * deltaTime;
            Vector3 p = transform.position + m_Velocity * deltaTime;
            if (p.y <= ArenaCenter.y)
            {
                p.y = ArenaCenter.y;
                m_Velocity = Vector3.zero;
            }
            transform.position = p;
            var level = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, level, 180f * deltaTime);
        }

        // For subclasses with attacks of their own. Called when an attack is due: return true to do one
        // instead of the dive. It then hovers facing the player, calling SpecialTick every frame until that
        // returns false.
        protected virtual bool StartSpecial(Vector3 head) => false;

        protected virtual bool SpecialTick(Vector3 head, float deltaTime) => false;

        // A stun or a freeze stopped it halfway.
        protected virtual void CancelSpecial()
        {
        }

        protected override void Interrupt()
        {
            base.Interrupt();
            if (m_State == State.Special)
                CancelSpecial();
            if (m_State == State.WindUp || m_State == State.Dive || m_State == State.Special)
            {
                m_State = State.Recover;
                m_StateUntil = Time.time + m_RecoverTime;
            }
        }
    }
}
