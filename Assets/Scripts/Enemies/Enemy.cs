using System;
using RuneArena.Player;
using UnityEngine;

namespace RuneArena.Enemies
{
    public enum EnemyStatus
    {
        Wet,
        Stunned,
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

        [Header("Feedback")]
        [SerializeField] Color m_TelegraphColor = new Color(1f, 0.9f, 0.3f);
        [SerializeField] Color m_WetTint = new Color(0.4f, 0.6f, 1f);
        [SerializeField] Color m_StunnedColor = new Color(0.55f, 0.55f, 0.55f);
        [SerializeField] float m_FlashTime = 0.1f;
        [SerializeField] float m_DeathTime = 0.3f;

        public event Action<Enemy> Died;

        public float Health { get; private set; }
        public float MaxHealth => m_MaxHealth;
        public bool IsDead { get; private set; }

        protected PlayerHealth Player { get; private set; }
        protected bool Telegraphing { get; set; }

        readonly float[] m_StatusUntil = new float[k_StatusCount];
        MaterialPropertyBlock m_Block;
        Color m_Color;
        Vector3 m_Knockback;
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
            if (IsDead)
                return;

            Health -= amount;
            m_FlashUntil = Time.time + m_FlashTime;
            if (Health <= 0f)
                Die();
        }

        // Velocity in m/s, applied horizontally and fading out.
        public void Push(Vector3 velocity)
        {
            velocity.y = 0f;
            m_Knockback += velocity;
        }

        public void ApplyStatus(EnemyStatus status, float duration)
        {
            int i = (int)status;
            m_StatusUntil[i] = Mathf.Max(m_StatusUntil[i], Time.time + duration);
            if (status == EnemyStatus.Stunned)
                Interrupt();
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
