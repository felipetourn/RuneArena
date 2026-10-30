using RuneArena.Arena;
using UnityEngine;

namespace RuneArena.Enemies
{
    // Circles the player in the air, then dives at the head after a wind-up and climbs back.
    // Doesn't use the NavMesh, so earth walls don't stop it; it steers around the arena's pillars.
    public class FlyingEnemy : Enemy
    {
        enum State
        {
            Enter,
            Orbit,
            WindUp,
            Dive,
            Recover,
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
        float m_PillarRadius;
        float m_PillarTop;

        protected override void Start()
        {
            base.Start();
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
                        Enter(State.WindUp, m_WindUp);
                    break;

                case State.WindUp:
                    m_Velocity = Vector3.MoveTowards(m_Velocity, Vector3.zero, m_Acceleration * deltaTime);
                    if (Time.time >= m_StateUntil)
                    {
                        m_DiveTarget = head;
                        Enter(State.Dive, 2f);
                    }
                    break;

                case State.Dive:
                    m_Velocity = (m_DiveTarget - transform.position).normalized * (m_DiveSpeed * SpeedMultiplier);
                    if ((transform.position - head).sqrMagnitude <= m_HitRadius * m_HitRadius)
                    {
                        Player.Damage(m_Damage * DamageMultiplier);
                        Enter(State.Recover, m_RecoverTime);
                    }
                    else if ((transform.position - m_DiveTarget).sqrMagnitude < 0.25f || Time.time >= m_StateUntil)
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
            Vector3 look = m_State == State.WindUp || m_State == State.Dive ? head - transform.position : m_Velocity;
            if (look.sqrMagnitude > 1e-4f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(look), 360f * deltaTime);
        }

        void Enter(State state, float duration)
        {
            m_State = state;
            m_StateUntil = Time.time + duration;
            Telegraphing = state == State.WindUp || state == State.Dive;
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

        protected override void Interrupt()
        {
            base.Interrupt();
            if (m_State == State.WindUp || m_State == State.Dive)
            {
                m_State = State.Recover;
                m_StateUntil = Time.time + m_RecoverTime;
            }
        }
    }
}
