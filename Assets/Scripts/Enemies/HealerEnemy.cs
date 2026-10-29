using RuneArena.Spells;
using RuneArena.Strokes;
using UnityEngine;
using UnityEngine.AI;

namespace RuneArena.Enemies
{
    public enum HealPriority
    {
        // Lowest health fraction first.
        MostHurt,
        // Nearest hurt ally first: heals more often, but not always who needs it most.
        Closest,
        // Missing health weighed against distance, with the boss counting extra.
        Smart,
    }

    // Comes into the arena and heals hurt allies one after another, after a visible wind-up. It sticks to
    // one patient until it is full, so the player can read who is being healed. Never heals itself.
    // With nobody to heal it escorts the ally closest to the player, or zaps the player if it is alone.
    public class HealerEnemy : GroundEnemy
    {
        static Material s_BeamMaterial;

        [Header("Healing")]
        [SerializeField] HealPriority m_Priority = HealPriority.Smart;
        [SerializeField] float m_HealAmount = 15f;
        [SerializeField] float m_HealRange = 8f;
        [SerializeField] float m_SearchRadius = 25f;
        [SerializeField] float m_WindUp = 1f;
        [SerializeField] float m_Cooldown = 4f;
        [SerializeField] Color m_HealColor = new Color(0.3f, 1f, 0.4f);

        [Header("With nobody to heal")]
        [Tooltip("Distance kept behind the escorted ally, on the side away from the player.")]
        [SerializeField] float m_EscortDistance = 3f;
        [SerializeField] float m_AttackDamage = 6f;
        [SerializeField] float m_AttackRange = 12f;
        [SerializeField] float m_AttackCooldown = 3f;
        [SerializeField] Color m_AttackColor = new Color(1f, 0.3f, 0.3f);

        [Header("Safety")]
        [Tooltip("Gets away when the player comes closer than this.")]
        [SerializeField] float m_SafeDistance = 7f;
        [SerializeField] LayerMask m_SightMask;

        Enemy m_Target;
        bool m_Attacking;
        float m_ActAt = -1f;
        float m_ReadyAt;

        protected override void Start()
        {
            base.Start();
            m_ReadyAt = Time.time + m_Cooldown * 0.5f;
        }

        protected override void Think(float deltaTime)
        {
            Vector3 feet = Player.Feet;

            // Spawned outside: walk in through the gate before anything else.
            if (!IsInsideArena)
            {
                MoveTo(ArenaRadius > 0f ? ArenaCenter : feet);
                return;
            }

            if (m_ActAt >= 0f)
            {
                WindingUp(feet, deltaTime);
                return;
            }

            float toPlayer = FlatDistance(transform.position, feet);
            if (toPlayer < m_SafeDistance && TryRetreat(feet, toPlayer))
                return;

            if (!NeedsHealing(m_Target))
                m_Target = ChooseTarget();

            if (m_Target != null)
            {
                bool inRange = Vector3.Distance(transform.position, m_Target.transform.position) <= m_HealRange && CanSee(m_Target.transform.position);
                if (!inRange)
                {
                    MoveTo(m_Target.transform.position);
                }
                else if (Time.time >= m_ReadyAt)
                {
                    Begin(false, m_WindUp);
                }
                else
                {
                    Stop();
                    FaceTowards(m_Target.transform.position, 360f, deltaTime);
                }
                return;
            }

            Enemy escort = EscortTarget(feet);
            if (escort != null)
            {
                Vector3 away = escort.transform.position - feet;
                away.y = 0f;
                MoveTo(escort.transform.position + away.normalized * m_EscortDistance);
                return;
            }

            // Alone: a weak zap so it doesn't just stand there.
            if (toPlayer <= m_AttackRange && CanSee(feet))
            {
                if (Time.time >= m_ReadyAt)
                    Begin(true, m_WindUp);
                else
                {
                    Stop();
                    FaceTowards(feet, 360f, deltaTime);
                }
            }
            else
            {
                MoveTo(feet);
            }
        }

        void Begin(bool attack, float windUp)
        {
            Stop();
            m_Attacking = attack;
            m_ActAt = Time.time + windUp;
            Telegraphing = true;
        }

        void WindingUp(Vector3 feet, float deltaTime)
        {
            Stop();
            if (!m_Attacking && !NeedsHealing(m_Target))
            {
                Interrupt();
                return;
            }

            FaceTowards(m_Attacking ? feet : m_Target.transform.position, 360f, deltaTime);
            if (Time.time < m_ActAt)
                return;

            if (m_Attacking)
            {
                Player.Damage(m_AttackDamage * DamageMultiplier);
                Beam(Player.Head.position + Vector3.down * 0.3f, m_AttackColor);
                m_ReadyAt = Time.time + m_AttackCooldown;
            }
            else
            {
                m_Target.Heal(m_HealAmount);
                Beam(m_Target.transform.position + Vector3.up * 0.9f, m_HealColor);
                m_ReadyAt = Time.time + m_Cooldown;
            }

            m_ActAt = -1f;
            Telegraphing = false;
        }

        bool NeedsHealing(Enemy enemy)
        {
            return enemy != null && enemy != this && !enemy.IsDead && enemy.IsInsideArena && enemy.Health < enemy.MaxHealth;
        }

        Enemy ChooseTarget()
        {
            Enemy best = null;
            float bestScore = float.NegativeInfinity;
            foreach (Enemy enemy in All)
            {
                if (!NeedsHealing(enemy))
                    continue;
                float distance = Vector3.Distance(transform.position, enemy.transform.position);
                if (distance > m_SearchRadius)
                    continue;

                float missing = 1f - enemy.Health / enemy.MaxHealth;
                float score;
                switch (m_Priority)
                {
                    case HealPriority.Closest:
                        score = -distance;
                        break;
                    case HealPriority.Smart:
                        score = missing * (enemy is BossEnemy ? 1.5f : 1f) / (1f + distance / 10f);
                        break;
                    default:
                        score = missing;
                        break;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = enemy;
                }
            }
            return best;
        }

        // The ally closest to the player, so the healer hangs back behind the front line.
        Enemy EscortTarget(Vector3 feet)
        {
            Enemy best = null;
            float closest = float.PositiveInfinity;
            foreach (Enemy enemy in All)
            {
                if (enemy == this || enemy.IsDead || !enemy.IsInsideArena || enemy is HealerEnemy)
                    continue;
                float d = FlatDistance(enemy.transform.position, feet);
                if (d < closest)
                {
                    closest = d;
                    best = enemy;
                }
            }
            return best;
        }

        bool CanSee(Vector3 point)
        {
            Vector3 from = transform.position + Vector3.up * 1.2f;
            return !Physics.Linecast(from, point + Vector3.up * 0.8f, m_SightMask, QueryTriggerInteraction.Ignore);
        }

        bool TryRetreat(Vector3 feet, float distance)
        {
            Vector3 away = transform.position - feet;
            away.y = 0f;
            Vector3 target = transform.position + away.normalized * (m_SafeDistance - distance + 2f);
            if (!NavMesh.SamplePosition(target, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                return false;
            MoveTo(hit.position);
            return true;
        }

        void Beam(Vector3 to, Color color)
        {
            if (s_BeamMaterial == null)
            {
                var recorder = FindAnyObjectByType<StrokeRecorder>(FindObjectsInactive.Include);
                s_BeamMaterial = recorder != null ? recorder.LineMaterial : null;
            }

            var go = new GameObject("Healer Beam");
            var line = go.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.SetPosition(0, transform.position + Vector3.up * 1.6f);
            line.SetPosition(1, to);
            line.widthMultiplier = 0.06f;
            if (s_BeamMaterial != null)
                RuneSpell.Tint(line, s_BeamMaterial, color);
            Destroy(go, 0.3f);
        }

        protected override void Interrupt()
        {
            base.Interrupt();
            m_ActAt = -1f;
            m_ReadyAt = Time.time + m_Cooldown * 0.5f;
        }
    }
}
