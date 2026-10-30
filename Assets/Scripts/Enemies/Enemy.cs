using System;
using System.Collections.Generic;
using RuneArena.Arena;
using RuneArena.Player;
using UnityEngine;

namespace RuneArena.Enemies
{
    // How a hit relates to the enemy's state, so feedback can tell a combo apart.
    public enum DamageKind
    {
        Normal,
        // Boosted by a combo, like lightning on a wet enemy.
        Combo,
        // Reduced by a bad combo, like fire on a wet enemy.
        Weakened,
    }

    public enum EnemyStatus
    {
        Wet,
        Stunned,
        Slowed,
        // Like a stun: can't move or attack.
        Frozen,
        // Standing on ivy. Only matters for fire, which does less to it.
        Entangled,
        // Held by roots: can't move, can still attack.
        Rooted,
    }

    // Shared by every enemy: health, statuses, knockback and color feedback.
    // Subclasses only decide how to move and attack.
    public abstract class Enemy : MonoBehaviour
    {
        static readonly int k_BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int k_EmissionColor = Shader.PropertyToID("_EmissionColor");
        static readonly int k_StatusCount = Enum.GetValues(typeof(EnemyStatus)).Length;

        [SerializeField] float m_MaxHealth = 100f;
        [SerializeField] Renderer m_Body;
        [Tooltip("How fast a push slows down.")]
        [SerializeField] float m_KnockbackDrag = 6f;
        [Tooltip("Seconds after a stun ends during which it can't be stunned again.")]
        [SerializeField] float m_StunImmunity = 1f;
        [Tooltip("0 takes stuns in full, 1 ignores them. For tougher enemies like the boss.")]
        [SerializeField, Range(0f, 1f)] float m_StunResistance = 0f;
        [SerializeField, Range(0f, 1f)] float m_KnockbackResistance = 0f;
        [Tooltip("While armored, damage from the wrong spells wears the armor down at this rate instead of hurting.")]
        [SerializeField, Range(0f, 1f)] float m_ArmorChip = 0.25f;

        [Header("Health bar")]
        [SerializeField] float m_BarScale = 1f;
        [Tooltip("Show the bar from the start instead of after the first hit.")]
        [SerializeField] bool m_AlwaysShowBar;
        [Tooltip("Shown over the bar. Empty for regular enemies.")]
        [SerializeField] string m_DisplayName;
        [Tooltip("Opens its wave and comes in alone through the main gate, announced.")]
        [SerializeField] bool m_Boss;

        [Header("Feedback")]
        [SerializeField] Color m_TelegraphColor = new Color(1f, 0.9f, 0.3f);
        [SerializeField] Color m_WetTint = new Color(0.4f, 0.6f, 1f);
        [SerializeField] Color m_StunnedColor = new Color(0.55f, 0.55f, 0.55f);
        [SerializeField] Color m_StunImmuneTint = new Color(0.9f, 0.9f, 1f);
        [SerializeField] Color m_FrozenColor = new Color(0.65f, 0.9f, 1f);
        [SerializeField] Color m_EntangledTint = new Color(0.3f, 0.75f, 0.3f);
        [SerializeField] float m_FlashTime = 0.1f;
        [SerializeField] float m_DeathTime = 0.3f;
        [Tooltip("Last part of the death time, in which it shrinks away. What comes before is for a death animation. 0 doesn't shrink.")]
        [SerializeField] float m_ShrinkTime = 0.3f;
        [Tooltip("Color multiplier of the hit flash. Above 1 for textured bodies, where white changes nothing.")]
        [SerializeField] float m_FlashBrightness = 1f;
        [Tooltip("How much the wind-up lights up a body whose material has emission. 0 leaves it to the color.")]
        [SerializeField] float m_TelegraphGlow;

        static readonly List<Enemy> s_All = new List<Enemy>();

        // Every living enemy in the scene.
        public static IReadOnlyList<Enemy> All => s_All;

        public event Action<Enemy> Died;
        public event Action<Enemy, float> Healed;
        // Armor worn down by a hit that couldn't break it.
        public event Action<Enemy, float> ArmorHit;
        public event Action<Enemy> ArmorBroken;
        // An attack starts winding up, and lands this many seconds later.
        public event Action<Enemy, float> AttackStarted;
        // Amount actually taken and what kind of hit it was.
        public event Action<Enemy, float, DamageKind> Damaged;

        public float Health { get; private set; }
        public float MaxHealth => m_MaxHealth;
        public bool IsDead { get; private set; }
        public int Level { get; private set; } = 1;
        public float BarScale => m_BarScale;
        public bool AlwaysShowBar => m_AlwaysShowBar;
        public string DisplayName => m_DisplayName;
        public virtual bool IsBoss => m_Boss;

        public ArmorType Armor { get; private set; }
        public float ArmorPoints { get; private set; }
        public float MaxArmor { get; private set; }
        public bool IsArmored => Armor != ArmorType.None && ArmorPoints > 0f;
        public bool CanBeStunned => Time.time >= m_StunImmuneUntil;

        // False while it is still outside the colosseum wall (spawning behind a gate or walking through a door).
        // Spells can't touch it until it is in, so the player can't farm enemies at the gates.
        public bool IsInsideArena => m_ArenaRadius <= 0f || FlatDistance(transform.position, m_ArenaCenter) < m_ArenaRadius;
        // Stays true once it is well past the doorway. From then on it can't leave: not walking, not pushed.
        public bool HasEntered { get; private set; }

        // 1 normally, lower while slowed. Movement code multiplies its speed by this.
        public float SpeedMultiplier => HasStatus(EnemyStatus.Rooted) ? 0f : (HasStatus(EnemyStatus.Slowed) ? 1f - m_Slow : 1f) * m_SpeedScale;
        public bool IsFlying => this is FlyingEnemy;

        protected PlayerHealth Player { get; private set; }
        // Center of the colosseum floor and the inner radius of its wall. Radius is 0 when there is no colosseum.
        protected Vector3 ArenaCenter => m_ArenaCenter;
        protected float ArenaRadius => m_ArenaRadius;
        // Attacks multiply their damage by this. Set by the difficulty.
        protected float DamageMultiplier => m_DamageScale;
        public bool Telegraphing { get; protected set; }

        readonly float[] m_StatusUntil = new float[k_StatusCount];
        MaterialPropertyBlock m_Block;
        Color m_Color;
        Color m_Emission;
        bool m_Glows;
        Vector3 m_Knockback;
        float m_Slow;
        float m_StunImmuneUntil;
        float m_FreezeImmuneUntil;
        float m_SpeedScale = 1f;
        float m_DamageScale = 1f;
        Vector3 m_ArenaCenter;
        float m_ArenaRadius;

        // How far past the wall it has to get to count as in, and how close to the wall it can get after that.
        const float k_EntryDepth = 1.5f;
        const float k_WallMargin = 1f;
        float m_FlashUntil;
        float m_DiedAt;

        protected virtual void Awake()
        {
            Health = m_MaxHealth;
            if (m_Body == null)
                m_Body = GetComponentInChildren<Renderer>();
            m_Block = new MaterialPropertyBlock();
            m_Color = m_Body.sharedMaterial.color;
            m_Glows = m_TelegraphGlow > 0f && m_Body.sharedMaterial.IsKeywordEnabled("_EMISSION");
            if (m_Glows)
                m_Emission = m_Body.sharedMaterial.GetColor(k_EmissionColor);
            if (GetComponent<EnemyHealthBar>() == null)
                gameObject.AddComponent<EnemyHealthBar>();
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
                float shrink = Mathf.Min(m_ShrinkTime, m_DeathTime);
                if (shrink > 0f)
                {
                    float t = (Time.time - m_DiedAt - (m_DeathTime - shrink)) / shrink;
                    transform.localScale = Vector3.one * Mathf.Clamp01(1f - t);
                }
                DeadTick(Time.deltaTime);
                return;
            }

            if (!HasEntered && (m_ArenaRadius <= 0f || FlatDistance(transform.position, m_ArenaCenter) < m_ArenaRadius - k_EntryDepth))
                HasEntered = true;

            float dt = Time.deltaTime;
            if (m_Knockback != Vector3.zero)
            {
                ApplyKnockback(m_Knockback * dt);
                m_Knockback *= Mathf.Exp(-m_KnockbackDrag * dt);
                if (m_Knockback.sqrMagnitude < 0.01f)
                    m_Knockback = Vector3.zero;
            }

            if (Player != null && !Player.IsDead && !HasStatus(EnemyStatus.Stunned) && !HasStatus(EnemyStatus.Frozen))
                Tick(dt);

            UpdateColor();
        }

        // Pulls a point back from the colosseum wall once the enemy is in, so nothing takes it out again.
        protected Vector3 KeepInside(Vector3 point)
        {
            if (!HasEntered || m_ArenaRadius <= 0f)
                return point;
            Vector3 flat = point - m_ArenaCenter;
            flat.y = 0f;
            float limit = m_ArenaRadius - k_WallMargin;
            if (flat.magnitude <= limit)
                return point;
            flat = flat.normalized * limit;
            return new Vector3(m_ArenaCenter.x + flat.x, point.y, m_ArenaCenter.z + flat.z);
        }

        // Call right after spawning. 1 leaves the stat as it is in the prefab.
        public void ApplyDifficulty(float speed, float damage, float health)
        {
            m_SpeedScale = speed;
            m_DamageScale *= damage;
            m_MaxHealth *= health;
            Health = m_MaxHealth;
        }

        // Call right after spawning, after the level. fraction is the armor points relative to max health.
        public void SetArmor(ArmorType type, float fraction)
        {
            Armor = type;
            MaxArmor = type == ArmorType.None ? 0f : m_MaxHealth * fraction;
            ArmorPoints = MaxArmor;
        }

        // Call right after spawning. Level 1 leaves the stats as they are; each level above adds perLevel.
        public void SetLevel(int level, float perLevel)
        {
            Level = Mathf.Max(level, 1);
            float scale = 1f + (Level - 1) * perLevel;
            m_DamageScale *= scale;
            m_MaxHealth *= scale;
            Health = m_MaxHealth;
        }

        protected virtual void OnEnable()
        {
            s_All.Add(this);
        }

        protected virtual void OnDisable()
        {
            s_All.Remove(this);
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

        // Every frame while dying, for things like falling out of the air.
        protected virtual void DeadTick(float deltaTime)
        {
        }

        // Starts the wind-up of an attack that lands after windUp seconds.
        protected void BeginAttack(float windUp)
        {
            Telegraphing = true;
            AttackStarted?.Invoke(this, windUp);
        }

        // source is the label of the rune whose spell did it, so the right one can break armor.
        public void Damage(float amount, DamageKind kind = DamageKind.Normal, string source = null)
        {
            if (IsDead || !IsInsideArena)
                return;

            // Health is untouched until the armor is gone. The hit that breaks it doesn't carry over:
            // the right spell breaks it outright, the wrong one only wears it down.
            if (IsArmored)
            {
                bool breaker = source != null && source == RuneArena.Enemies.Armor.Breaker(Armor);
                float worn = breaker ? ArmorPoints : Mathf.Min(amount * m_ArmorChip, ArmorPoints);
                ArmorPoints -= worn;
                m_FlashUntil = Time.time + m_FlashTime;
                ArmorHit?.Invoke(this, worn);
                if (ArmorPoints <= 0f)
                    BreakArmor();
                return;
            }

            float taken = Mathf.Min(amount, Health);
            Health -= taken;
            m_FlashUntil = Time.time + m_FlashTime;
            Damaged?.Invoke(this, taken, kind);
            if (Health <= 0f)
                Die();
        }

        void BreakArmor()
        {
            ArmorPoints = 0f;
            ArmorBroken?.Invoke(this);
        }

        public void Heal(float amount)
        {
            if (IsDead || Health >= m_MaxHealth)
                return;
            float healed = Mathf.Min(amount, m_MaxHealth - Health);
            Health += healed;
            Healed?.Invoke(this, healed);
        }

        // Velocity in m/s, applied horizontally and fading out.
        public void Push(Vector3 velocity)
        {
            if (!IsInsideArena || IsArmored)
                return;
            velocity.y = 0f;
            m_Knockback += velocity * (1f - m_KnockbackResistance);
        }

        // Returns false if the status didn't take, which only happens to a stun right after another one.
        public bool ApplyStatus(EnemyStatus status, float duration)
        {
            if (!IsInsideArena)
                return false;
            // Armor keeps it on its feet: no stuns, freezes or roots until it breaks.
            if (IsArmored && (status == EnemyStatus.Stunned || status == EnemyStatus.Frozen || status == EnemyStatus.Rooted))
                return false;
            int i = (int)status;
            if (status == EnemyStatus.Stunned)
            {
                if (!CanBeStunned || m_StunResistance >= 1f)
                    return false;
                m_StatusUntil[i] = Time.time + duration * (1f - m_StunResistance);
                m_StunImmuneUntil = m_StatusUntil[i] + m_StunImmunity;
                Interrupt();
                return true;
            }

            // Freezing works like a stun, with its own resistance and immunity window.
            if (status == EnemyStatus.Frozen)
            {
                if (Time.time < m_FreezeImmuneUntil || m_StunResistance >= 1f)
                    return false;
                m_StatusUntil[i] = Time.time + duration * (1f - m_StunResistance);
                m_FreezeImmuneUntil = m_StatusUntil[i] + m_StunImmunity;
                Interrupt();
                return true;
            }

            m_StatusUntil[i] = Mathf.Max(m_StatusUntil[i], Time.time + duration);
            return true;
        }

        // Removes it without dying: no death event, no drops. For clearing the arena.
        public void Despawn()
        {
            Destroy(gameObject);
        }

        // A stun that ignores resistance and immunity, for the enemy's own mistakes (a boss charging into a wall).
        public void ForceStun(float duration)
        {
            if (IsDead)
                return;
            int i = (int)EnemyStatus.Stunned;
            m_StatusUntil[i] = Time.time + duration;
            m_StunImmuneUntil = m_StatusUntil[i] + m_StunImmunity;
            Interrupt();
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

        [ContextMenu("Test/Armor Ice")]
        void TestArmorIce() => SetArmor(ArmorType.Ice, 0.5f);

        [ContextMenu("Test/Armor Magma")]
        void TestArmorMagma() => SetArmor(ArmorType.Magma, 0.5f);

        [ContextMenu("Test/Armor Metal")]
        void TestArmorMetal() => SetArmor(ArmorType.Metal, 0.5f);

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
            s_All.Remove(this);
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
            if (IsArmored)
                color = Color.Lerp(color, RuneArena.Enemies.Armor.ColorOf(Armor), 0.6f);
            if (HasStatus(EnemyStatus.Wet))
                color = Color.Lerp(color, m_WetTint, 0.5f);
            if (HasStatus(EnemyStatus.Entangled) || HasStatus(EnemyStatus.Rooted))
                color = Color.Lerp(color, m_EntangledTint, 0.5f);
            if (HasStatus(EnemyStatus.Frozen))
                color = m_FrozenColor;
            else if (HasStatus(EnemyStatus.Stunned))
                color = m_StunnedColor;
            else if (!CanBeStunned)
                color = Color.Lerp(color, m_StunImmuneTint, 0.4f);
            if (Telegraphing)
                color = Color.Lerp(color, m_TelegraphColor, 0.5f + 0.5f * Mathf.Sin(Time.time * 25f));
            if (Time.time < m_FlashUntil)
                color = Color.white * m_FlashBrightness;

            m_Body.GetPropertyBlock(m_Block);
            m_Block.SetColor(k_BaseColor, color);
            if (m_Glows)
            {
                float glow = Telegraphing ? m_TelegraphGlow * (0.5f + 0.5f * Mathf.Sin(Time.time * 25f)) : 0f;
                m_Block.SetColor(k_EmissionColor, m_Emission + m_TelegraphColor * glow);
            }
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
