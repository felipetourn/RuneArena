using System;
using RuneArena.Arena;
using RuneArena.Player;
using UnityEngine;

namespace RuneArena.Enemies
{
    public enum EnemyStatus
    {
        Wet,
        Stunned,
        Slowed,
    }

    // Shared by every enemy: health, statuses, knockback and color feedback.
    // Subclasses only decide how to move and attack.
    public abstract class Enemy : MonoBehaviour
    {
        static readonly int k_BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int k_StatusCount = Enum.GetValues(typeof(EnemyStatus)).Length;

        [SerializeField] float m_MaxHealth = 100f;
        [SerializeField] Renderer m_Body;
        [Tooltip("How fast a push slows down.")]
        [SerializeField] float m_KnockbackDrag = 6f;
        [Tooltip("Seconds after a stun ends during which it can't be stunned again.")]
        [SerializeField] float m_StunImmunity = 1f;

        [Header("Feedback")]
        [SerializeField] Color m_TelegraphColor = new Color(1f, 0.9f, 0.3f);
        [SerializeField] Color m_WetTint = new Color(0.4f, 0.6f, 1f);
        [SerializeField] Color m_StunnedColor = new Color(0.55f, 0.55f, 0.55f);
        [SerializeField] Color m_StunImmuneTint = new Color(0.9f, 0.9f, 1f);
        [SerializeField] float m_FlashTime = 0.1f;
        [SerializeField] float m_DeathTime = 0.3f;

        public event Action<Enemy> Died;

        public float Health { get; private set; }
        public float MaxHealth => m_MaxHealth;
        public bool IsDead { get; private set; }
        public bool CanBeStunned => Time.time >= m_StunImmuneUntil;

        // False while it is still outside the colosseum wall (spawning behind a gate or walking through a door).
        // Spells can't touch it until it is in, so the player can't farm enemies at the gates.
        public bool IsInsideArena => m_ArenaRadius <= 0f || FlatDistance(transform.position, m_ArenaCenter) < m_ArenaRadius;

        // 1 normally, lower while slowed. Movement code multiplies its speed by this.
        public float SpeedMultiplier => (HasStatus(EnemyStatus.Slowed) ? 1f - m_Slow : 1f) * m_SpeedScale;

        protected PlayerHealth Player { get; private set; }
        // Center of the colosseum floor and the inner radius of its wall. Radius is 0 when there is no colosseum.
        protected Vector3 ArenaCenter => m_ArenaCenter;
        protected float ArenaRadius => m_ArenaRadius;
        // Attacks multiply their damage by this. Set by the difficulty.
        protected float DamageMultiplier => m_DamageScale;
        protected bool Telegraphing { get; set; }

        readonly float[] m_StatusUntil = new float[k_StatusCount];
        MaterialPropertyBlock m_Block;
        Color m_Color;
        Vector3 m_Knockback;
        float m_Slow;
        float m_StunImmuneUntil;
        float m_SpeedScale = 1f;
        float m_DamageScale = 1f;
        Vector3 m_ArenaCenter;
        float m_ArenaRadius;
        float m_FlashUntil;
        float m_DiedAt;

        protected virtual void Awake()
        {
            Health = m_MaxHealth;
            if (m_Body == null)
                m_Body = GetComponentInChildren<Renderer>();
            m_Block = new MaterialPropertyBlock();
            m_Color = m_Body.sharedMaterial.color;
        }

        protected virtual void Start()
        {
            Player = FindAnyObjectByType<PlayerHealth>();

            var arena = FindAnyObjectByType<ColosseumBuilder>();
            if (arena != null)
            {
                m_ArenaCenter = arena.transform.position;
                m_ArenaRadius = arena.Radius - arena.Thickness;
            }
        }

        void Update()
        {
            if (IsDead)
            {
                float t = (Time.time - m_DiedAt) / m_DeathTime;
                transform.localScale = Vector3.one * Mathf.Max(1f - t, 0f);
                return;
            }

            float dt = Time.deltaTime;
            if (m_Knockback != Vector3.zero)
            {
                ApplyKnockback(m_Knockback * dt);
                m_Knockback *= Mathf.Exp(-m_KnockbackDrag * dt);
                if (m_Knockback.sqrMagnitude < 0.01f)
                    m_Knockback = Vector3.zero;
            }

            if (Player != null && !Player.IsDead && !HasStatus(EnemyStatus.Stunned))
                Tick(dt);

            UpdateColor();
        }

        // Call right after spawning. 1 leaves the stat as it is in the prefab.
        public void ApplyDifficulty(float speed, float damage, float health)
        {
            m_SpeedScale = speed;
            m_DamageScale = damage;
            m_MaxHealth *= health;
            Health = m_MaxHealth;
        }

        protected abstract void Tick(float deltaTime);

        // Moves the enemy by a knockback offset, respecting how it normally moves.
        protected abstract void ApplyKnockback(Vector3 offset);

        // Cancels whatever attack is in progress.
        protected virtual void Interrupt()
        {
            Telegraphing = false;
        }

        protected virtual void OnDied()
        {
        }

        public void Damage(float amount)
        {
            if (IsDead || !IsInsideArena)
                return;

            Health -= amount;
            m_FlashUntil = Time.time + m_FlashTime;
            if (Health <= 0f)
                Die();
        }

        // Velocity in m/s, applied horizontally and fading out.
        public void Push(Vector3 velocity)
        {
            if (!IsInsideArena)
                return;
            velocity.y = 0f;
            m_Knockback += velocity;
        }

        // Returns false if the status didn't take, which only happens to a stun right after another one.
        public bool ApplyStatus(EnemyStatus status, float duration)
        {
            if (!IsInsideArena)
                return false;
            int i = (int)status;
            if (status == EnemyStatus.Stunned)
            {
                if (!CanBeStunned)
                    return false;
                m_StatusUntil[i] = Time.time + duration;
                m_StunImmuneUntil = m_StatusUntil[i] + m_StunImmunity;
                Interrupt();
                return true;
            }

            m_StatusUntil[i] = Mathf.Max(m_StatusUntil[i], Time.time + duration);
            return true;
        }

        // fraction is how much speed is taken away (0.3 = 30% slower). A stronger slow replaces a weaker one.
        public void ApplySlow(float fraction, float duration)
        {
            if (!IsInsideArena)
                return;
            fraction = Mathf.Clamp01(fraction);
            if (!HasStatus(EnemyStatus.Slowed) || fraction >= m_Slow)
                m_Slow = fraction;
            ApplyStatus(EnemyStatus.Slowed, duration);
        }

        public void ClearStatus(EnemyStatus status)
        {
            m_StatusUntil[(int)status] = 0f;
        }

        public bool HasStatus(EnemyStatus status)
        {
            return Time.time < m_StatusUntil[(int)status];
        }

        [ContextMenu("Test/Damage 25")]
        void TestDamage() => Damage(25f);

        [ContextMenu("Test/Stun 2s")]
        void TestStun() => ApplyStatus(EnemyStatus.Stunned, 2f);

        [ContextMenu("Test/Wet 4s")]
        void TestWet() => ApplyStatus(EnemyStatus.Wet, 4f);

        [ContextMenu("Test/Slow 50% 5s")]
        void TestSlow() => ApplySlow(0.5f, 5f);

        [ContextMenu("Test/Push Back")]
        void TestPush()
        {
            if (Player != null)
                Push((transform.position - Player.Feet).normalized * 8f);
        }

        void Die()
        {
            IsDead = true;
            m_DiedAt = Time.time;
            Interrupt();
            OnDied();
            foreach (Collider c in GetComponentsInChildren<Collider>())
                c.enabled = false;
            Died?.Invoke(this);
            Destroy(gameObject, m_DeathTime);
        }

        void UpdateColor()
        {
            Color color = m_Color;
            if (HasStatus(EnemyStatus.Wet))
                color = Color.Lerp(color, m_WetTint, 0.5f);
            if (HasStatus(EnemyStatus.Stunned))
                color = m_StunnedColor;
            else if (!CanBeStunned)
                color = Color.Lerp(color, m_StunImmuneTint, 0.4f);
            if (Telegraphing)
                color = Color.Lerp(color, m_TelegraphColor, 0.5f + 0.5f * Mathf.Sin(Time.time * 25f));
            if (Time.time < m_FlashUntil)
                color = Color.white;

            m_Body.GetPropertyBlock(m_Block);
            m_Block.SetColor(k_BaseColor, color);
            m_Body.SetPropertyBlock(m_Block);
        }

        protected static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        protected void FaceTowards(Vector3 point, float degreesPerSecond, float deltaTime)
        {
            Vector3 dir = point - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f)
                return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), degreesPerSecond * deltaTime);
        }
    }
}
