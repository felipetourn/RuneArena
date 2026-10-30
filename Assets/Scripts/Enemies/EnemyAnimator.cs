using UnityEngine;

namespace RuneArena.Enemies
{
    // Plays the model's clips from what the enemy is doing. Its controller only needs a state per clip,
    // named like the fields below (RuneArena > Create Enemy Models makes it); the choice is made here,
    // so there are no transitions to set up.
    [RequireComponent(typeof(Enemy))]
    public class EnemyAnimator : MonoBehaviour
    {
        [SerializeField] Animator m_Animator;
        [Tooltip("Put on the Animator on wake, in case the model's own reference got lost.")]
        [SerializeField] RuntimeAnimatorController m_Controller;
        [SerializeField] float m_Fade = 0.15f;

        [Header("States")]
        [SerializeField] string m_Idle = "Idle";
        [SerializeField] string m_Move = "Walk";
        [SerializeField] string m_Attack = "Attack";
        [SerializeField] string m_Hit = "Hit";
        [SerializeField] string m_Stunned = "Hit";
        [SerializeField] string m_Death = "Death";
        [Tooltip("Played while a flying enemy dives at the player. Empty to keep the move clip.")]
        [SerializeField] string m_Lunge;

        [Header("Timing")]
        [Tooltip("Speed in m/s at which the move clip plays at its own pace, faster or slower with the enemy. 0 always plays it at its own pace.")]
        [SerializeField] float m_MoveSpeed = 1f;
        [Tooltip("Fastest the move clip plays. Past this the feet slide instead.")]
        [SerializeField] float m_MaxPace = 2f;
        [Tooltip("Seconds into the attack clip where the blow lands. The clip is sped up or slowed down so it lands with the attack.")]
        [SerializeField] float m_AttackImpact = 0.7f;
        [Tooltip("Seconds of the attack clip to play, counted at its own pace.")]
        [SerializeField] float m_AttackLength = 1.4f;
        [SerializeField] float m_HitLength = 0.4f;
        [Tooltip("Pace of every clip while slowed.")]
        [SerializeField] float m_SlowedPace = 0.6f;

        [Header("Death")]
        [Tooltip("Seconds lying there after dying before it starts sinking into the floor.")]
        [SerializeField] float m_SinkDelay = 1.5f;
        [SerializeField] float m_SinkTime = 1f;
        [SerializeField] float m_SinkDepth = 0.6f;

        Enemy m_Enemy;
        string m_Current;
        float m_AttackSpeed = 1f;
        float m_ImpactAt;
        float m_AttackUntil;
        float m_HitUntil;
        Vector3 m_LastPosition;
        float m_Speed;
        float m_DiedAt = -1f;
        float m_BodyHeight;
        bool m_Reported;

        void Awake()
        {
            m_Enemy = GetComponent<Enemy>();
            if (m_Animator == null)
                m_Animator = GetComponentInChildren<Animator>();
            m_LastPosition = transform.position;
            if (m_Animator != null && m_Controller != null)
                m_Animator.runtimeAnimatorController = m_Controller;
            if (m_Animator == null || m_Animator.runtimeAnimatorController == null)
            {
                Debug.LogWarning($"{name} has no animator controller. Run RuneArena > Create Enemy Models with Play off.", this);
                enabled = false;
            }
        }

        void OnEnable()
        {
            m_Enemy.AttackStarted += OnAttackStarted;
            m_Enemy.Damaged += OnDamaged;
            m_Enemy.ArmorHit += OnArmorHit;
        }

        void OnDisable()
        {
            m_Enemy.AttackStarted -= OnAttackStarted;
            m_Enemy.Damaged -= OnDamaged;
            m_Enemy.ArmorHit -= OnArmorHit;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt > 0f)
            {
                Vector3 moved = transform.position - m_LastPosition;
                moved.y = 0f;
                m_Speed = Mathf.Lerp(m_Speed, moved.magnitude / dt, 1f - Mathf.Exp(-10f * dt));
            }
            m_LastPosition = transform.position;

            if (m_Enemy.IsDead)
            {
                if (m_DiedAt < 0f)
                {
                    m_DiedAt = Time.time;
                    m_BodyHeight = m_Animator.transform.localPosition.y;
                }
                Play(m_Death, 1f);
                Sink();
                return;
            }
            // A frozen enemy is a statue: the pose it was in, held.
            if (m_Enemy.HasStatus(EnemyStatus.Frozen))
            {
                m_Animator.speed = 0f;
                return;
            }

            float pace = m_Enemy.HasStatus(EnemyStatus.Slowed) ? m_SlowedPace : 1f;
            if (m_Enemy.HasStatus(EnemyStatus.Stunned))
            {
                Play(m_Stunned, pace);
                return;
            }

            if (!string.IsNullOrEmpty(m_Lunge) && m_Enemy is FlyingEnemy flyer && flyer.IsDiving)
            {
                Play(m_Lunge, pace);
                return;
            }

            // Called off before landing, like by a stun that already ended.
            if (Time.time < m_ImpactAt && !m_Enemy.Telegraphing)
                m_AttackUntil = 0f;
            // Stretched up to the blow, then the follow-through at its own pace.
            if (Time.time < m_AttackUntil)
            {
                Play(m_Attack, (Time.time < m_ImpactAt ? m_AttackSpeed : 1f) * pace);
                return;
            }
            if (Time.time < m_HitUntil)
            {
                Play(m_Hit, pace);
                return;
            }

            if (m_Speed > 0.2f && !m_Enemy.HasStatus(EnemyStatus.Rooted))
                Play(m_Move, m_MoveSpeed > 0f ? Mathf.Min(m_Speed / m_MoveSpeed, m_MaxPace) : pace);
            else
                Play(m_Idle, pace);
        }

        void OnAttackStarted(Enemy enemy, float windUp)
        {
            m_AttackSpeed = windUp > 0f ? m_AttackImpact / windUp : 1f;
            m_ImpactAt = Time.time + windUp;
            m_AttackUntil = m_ImpactAt + Mathf.Max(m_AttackLength - m_AttackImpact, 0f);
            m_HitUntil = 0f;
            // An attack on the clip already playing (the flyer's wind-up is its flying, faster) goes on from where it is.
            if (m_Attack == m_Current)
                m_Animator.speed = m_AttackSpeed;
            else
                Restart(m_Attack, m_AttackSpeed);
        }

        void OnDamaged(Enemy enemy, float amount, DamageKind kind)
        {
            Flinch();
        }

        void OnArmorHit(Enemy enemy, float amount)
        {
            Flinch();
        }

        // Only between attacks: a hit doesn't cancel a swing, or the wind-up would stop telling when it lands.
        void Flinch()
        {
            if (m_Enemy.IsDead || Time.time < m_AttackUntil || m_Enemy.HasStatus(EnemyStatus.Stunned) || m_Enemy.HasStatus(EnemyStatus.Frozen))
                return;
            m_HitUntil = Time.time + m_HitLength;
            Restart(m_Hit, 1f);
        }

        // The body goes down through the floor, starting slow, so the corpse doesn't just vanish.
        void Sink()
        {
            float t = Mathf.Clamp01((Time.time - m_DiedAt - m_SinkDelay) / m_SinkTime);
            Vector3 p = m_Animator.transform.localPosition;
            p.y = m_BodyHeight - m_SinkDepth * t * t;
            m_Animator.transform.localPosition = p;
        }

        // Reports, once, an Animator that lost its controller after waking up.
        bool Ready()
        {
            if (m_Animator.runtimeAnimatorController != null)
                return true;
            if (!m_Reported)
                Debug.LogWarning($"{name}: the Animator on {m_Animator.name} lost its controller after waking up.", m_Animator);
            m_Reported = true;
            return false;
        }

        void Play(string state, float speed)
        {
            if (!Ready())
                return;
            m_Animator.speed = speed;
            if (state == m_Current)
                return;
            m_Animator.CrossFadeInFixedTime(state, m_Fade);
            m_Current = state;
        }

        // From the start, even if it is the state already playing.
        void Restart(string state, float speed)
        {
            if (!Ready())
                return;
            m_Animator.speed = speed;
            m_Animator.CrossFadeInFixedTime(state, m_Fade, 0, 0f);
            m_Current = state;
        }
    }
}
