using System;
using RuneArena.Effects;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RuneArena.Enemies
{
    // Plays the model's clips from what the enemy is doing. Its controller only needs a state per clip,
    // named like the fields below (RuneArena > Create Enemy Models makes it); the choice is made here,
    // so there are no transitions to set up.
    [RequireComponent(typeof(Enemy))]
    public class EnemyAnimator : MonoBehaviour
    {
        // The clip for one of the enemy's attacks, by the name it gives its wind-up (Enemy.AttackName).
        [Serializable]
        public struct NamedAttack
        {
            public string Name;
            public string State;
            [Tooltip("Seconds into the clip where it lands.")]
            public float Impact;
            [Tooltip("Seconds of the clip to play, counted at its own pace.")]
            public float Length;
        }

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
        [Tooltip("Played above Run Above m/s instead of the move clip, like a boss's charge. Empty for none.")]
        [SerializeField] string m_Run;
        [SerializeField] float m_RunAbove = 4f;
        [Tooltip("Speed in m/s at which the run clip plays at its own pace.")]
        [SerializeField] float m_RunSpeed = 6f;
        [Tooltip("Attacks with their own clip. Any other plays the Attack state with the timing below.")]
        [SerializeField] NamedAttack[] m_Attacks = new NamedAttack[0];

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
        [Tooltip("Seconds into the death clip it starts from. For one that dies in the air, to fall already lying down.")]
        [SerializeField] float m_DeathStart;
        [Tooltip("Seconds lying there after dying before it starts sinking into the floor.")]
        [SerializeField] float m_SinkDelay = 1.5f;
        [SerializeField] float m_SinkTime = 1f;
        [SerializeField] float m_SinkDepth = 0.6f;
        [Tooltip("How far under the floor it starts when it climbs out of it: about its height. 0 to appear where it is.")]
        [SerializeField] float m_EmergeDepth = 1.9f;
        [Tooltip("Seconds of the climb, if shorter than the whole entrance. The rest plays the Entrance clip.")]
        [SerializeField] float m_RiseTime;
        [Tooltip("Played once it is out of the floor, for the rest of its entrance. Empty to stay idle.")]
        [SerializeField] string m_Entrance;
        [Tooltip("Pace of the entrance clip: below 1 to make it last longer.")]
        [SerializeField] float m_EntrancePace = 1f;

        [Header("Sound")]
        [Tooltip("Loops while it is alive, louder while it moves. Starts at a random point, so a crowd doesn't sound in step.")]
        [SerializeField] AudioClip m_MoveLoop;
        [SerializeField] float m_StillVolume = 0.15f;
        [SerializeField] float m_MovingVolume = 0.6f;

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
        float m_EmergeAt;
        float m_EmergeTime;
        string m_AttackState;
        string m_ActionState;
        float m_ActionUntil;
        AudioSource m_Loop;
        bool m_Reported;

        void Awake()
        {
            m_Enemy = GetComponent<Enemy>();
            if (m_Animator == null)
                m_Animator = GetComponentInChildren<Animator>();
            m_LastPosition = transform.position;
            if (m_Animator != null)
                m_BodyHeight = m_Animator.transform.localPosition.y;
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
            m_Enemy.Emerging += OnEmerging;
            m_Enemy.Acting += OnActing;
        }

        void Start()
        {
            m_Loop = Sfx.Loop(m_MoveLoop, transform);
            if (m_Loop != null)
                m_Loop.time = Random.Range(0f, m_MoveLoop.length);
        }

        void OnDisable()
        {
            m_Enemy.AttackStarted -= OnAttackStarted;
            m_Enemy.Damaged -= OnDamaged;
            m_Enemy.ArmorHit -= OnArmorHit;
            m_Enemy.Emerging -= OnEmerging;
            m_Enemy.Acting -= OnActing;
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
            if (m_Loop != null)
            {
                float target = m_Enemy.IsDead ? 0f : Mathf.Lerp(m_StillVolume, m_MovingVolume, Mathf.Clamp01(m_Speed));
                m_Loop.volume = Mathf.MoveTowards(m_Loop.volume, target, dt * 2f);
            }

            if (m_Enemy.IsDead)
            {
                if (m_DiedAt < 0f)
                {
                    m_DiedAt = Time.time;
                    if (m_DeathStart > 0f && Ready())
                    {
                        m_Animator.speed = 1f;
                        m_Animator.CrossFadeInFixedTime(m_Death, m_Fade, 0, m_DeathStart);
                        m_Current = m_Death;
                    }
                }
                Play(m_Death, 1f);
                Sink();
                return;
            }
            if (Time.time < m_ActionUntil)
            {
                Play(m_ActionState, 1f);
                return;
            }
            // Climbing out of the floor, fast at first and slowing down as it gets out, then its entrance.
            if (m_Enemy.IsEmerging)
            {
                float rise = m_RiseTime > 0f ? Mathf.Min(m_RiseTime, m_EmergeTime) : m_EmergeTime;
                float t = 1f - Mathf.Clamp01((Time.time - m_EmergeAt) / rise);
                SetBodyDepth(m_EmergeDepth * t * t);
                bool risen = t <= 0f || m_EmergeDepth <= 0f;
                if (risen && !string.IsNullOrEmpty(m_Entrance))
                    Play(m_Entrance, m_EntrancePace);
                else
                    Play(m_Idle, 1f);
                return;
            }
            if (m_EmergeTime > 0f)
            {
                SetBodyDepth(0f);
                m_EmergeTime = 0f;
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
                Play(m_AttackState, (Time.time < m_ImpactAt ? m_AttackSpeed : 1f) * pace);
                return;
            }
            if (Time.time < m_HitUntil)
            {
                Play(m_Hit, pace);
                return;
            }

            if (!string.IsNullOrEmpty(m_Run) && m_Speed > m_RunAbove)
                Play(m_Run, Mathf.Min(m_Speed / m_RunSpeed, m_MaxPace));
            else if (m_Speed > 0.2f && !m_Enemy.HasStatus(EnemyStatus.Rooted))
                Play(m_Move, m_MoveSpeed > 0f ? Mathf.Min(m_Speed / m_MoveSpeed, m_MaxPace) : pace);
            else
                Play(m_Idle, pace);
        }

        void OnAttackStarted(Enemy enemy, float windUp)
        {
            string state = m_Attack;
            float impact = m_AttackImpact, length = m_AttackLength;
            foreach (NamedAttack attack in m_Attacks)
            {
                if (attack.Name != enemy.AttackName)
                    continue;
                state = attack.State;
                impact = attack.Impact;
                length = attack.Length;
                break;
            }

            m_AttackState = state;
            m_AttackSpeed = windUp > 0f ? impact / windUp : 1f;
            m_ImpactAt = Time.time + windUp;
            m_AttackUntil = m_ImpactAt + Mathf.Max(length - impact, 0f);
            m_HitUntil = 0f;
            // An attack on the clip already playing (the flyer's wind-up is its flying, faster) goes on from where it is.
            if (state == m_Current)
                m_Animator.speed = m_AttackSpeed;
            else
                Restart(state, m_AttackSpeed);
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
            SetBodyDepth(m_SinkDepth * t * t);
        }

        void SetBodyDepth(float depth)
        {
            Vector3 p = m_Animator.transform.localPosition;
            p.y = m_BodyHeight - depth;
            m_Animator.transform.localPosition = p;
        }

        void OnActing(Enemy enemy, string state, float seconds)
        {
            m_ActionState = state;
            m_ActionUntil = Time.time + seconds;
            m_AttackUntil = 0f;
            m_HitUntil = 0f;
            Restart(state, 1f);
        }

        void OnEmerging(Enemy enemy, float duration)
        {
            m_EmergeAt = Time.time;
            m_EmergeTime = Mathf.Max(duration, 0.01f);
            SetBodyDepth(m_EmergeDepth);
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
