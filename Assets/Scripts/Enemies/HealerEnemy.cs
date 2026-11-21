using System.Collections.Generic;
using RuneArena.Effects;
using RuneArena.Spells;
using RuneArena.Waves;
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

    // Comes into the arena, keeps one summoned skeleton on its feet and heals hurt allies one after another,
    // after a visible wind-up. It sticks to one patient until it is full, so the player can read who is being
    // healed. Never heals itself. There is no limit to the skeletons: the way to stop them is to kill it first.
    public class HealerEnemy : GroundEnemy
    {
        enum Action
        {
            None,
            Heal,
            Summon,
        }

        [Header("Healing")]
        [SerializeField] HealPriority m_Priority = HealPriority.Smart;
        [SerializeField] float m_HealAmount = 15f;
        [SerializeField] float m_HealRange = 8f;
        [SerializeField] float m_SearchRadius = 25f;
        [SerializeField] float m_WindUp = 1f;
        [SerializeField] float m_Cooldown = 4f;
        [SerializeField] Color m_HealColor = new Color(0.3f, 1f, 0.4f);
        [Tooltip("Seconds the patient's body stays lit after being healed.")]
        [SerializeField] float m_HealGlowTime = 0.9f;

        [Header("Summon")]
        [Tooltip("The enemy it keeps alive at its side, one at a time.")]
        [SerializeField] Enemy m_Minion;
        [Tooltip("Seconds after its skeleton dies before it raises another.")]
        [SerializeField] float m_SummonCooldown = 8f;
        [SerializeField] float m_SummonWindUp = 1f;
        [Tooltip("Seconds the skeleton takes to climb out of the floor, during which it can't move or attack.")]
        [SerializeField] float m_EmergeTime = 1.5f;
        [Tooltip("How far ahead of the healer, toward the player, it comes up.")]
        [SerializeField] float m_SummonDistance = 2f;
        [Tooltip("Seconds before trying again when the summon is interrupted or there's no room for it.")]
        [SerializeField] float m_SummonRetry = 2f;
        [SerializeField] Color m_SummonColor = new Color(0.55f, 0.25f, 1f);

        [Header("With nobody to heal")]
        [Tooltip("Distance kept behind the escorted ally, on the side away from the player.")]
        [SerializeField] float m_EscortDistance = 3f;

        [Header("Safety")]
        [Tooltip("Gets away when the player comes closer than this.")]
        [SerializeField] float m_SafeDistance = 7f;
        [SerializeField] LayerMask m_SightMask;

        readonly List<ParticleSystem> m_HandGlows = new List<ParticleSystem>();
        readonly List<Transform> m_Hands = new List<Transform>();

        WaveSpawner m_Spawner;
        Enemy m_Target;
        Enemy m_Summoned;
        ParticleSystem m_PatientMark;
        Action m_Action;
        float m_ActAt = -1f;
        float m_ReadyAt;
        float m_SummonAt;

        protected override void Start()
        {
            base.Start();
            m_Spawner = FindAnyObjectByType<WaveSpawner>();
            m_ReadyAt = Time.time + m_Cooldown * 0.5f;
            foreach (Transform t in GetComponentsInChildren<Transform>())
            {
                if (t.name.Contains("L Hand") || t.name.Contains("R Hand"))
                    m_Hands.Add(t);
            }
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

            if (m_Minion != null && m_Summoned == null && Time.time >= m_SummonAt)
            {
                Begin(Action.Summon, m_SummonWindUp);
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
                    Begin(Action.Heal, m_WindUp);
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

            // Alone, waiting to raise the next skeleton.
            Stop();
            FaceTowards(feet, 360f, deltaTime);
        }

        void Begin(Action action, float windUp)
        {
            Stop();
            m_Action = action;
            m_ActAt = Time.time + windUp;
            BeginAttack(windUp, action == Action.Heal ? "Heal" : "Summon");

            Color color = action == Action.Heal ? m_HealColor : m_SummonColor;
            foreach (Transform hand in m_Hands)
            {
                ParticleSystem glow = SpellFx.HandGlow(hand, color);
                if (glow != null)
                    m_HandGlows.Add(glow);
            }
            if (action == Action.Heal)
                m_PatientMark = SpellFx.HealMark(m_Target.transform, m_HealColor);
        }

        void WindingUp(Vector3 feet, float deltaTime)
        {
            Stop();
            if (m_Action == Action.Heal && !NeedsHealing(m_Target))
            {
                Interrupt();
                return;
            }

            FaceTowards(m_Action == Action.Heal ? m_Target.transform.position : feet, 360f, deltaTime);
            if (Time.time < m_ActAt)
                return;

            if (m_Action == Action.Heal)
            {
                m_Target.Heal(m_HealAmount);
                m_Target.ShowGlow(m_HealColor, m_HealGlowTime);
                SpellFx.HealAura(m_Target.transform, m_Target.BodyBounds, m_HealColor);
                m_ReadyAt = Time.time + m_Cooldown;
            }
            else
            {
                Summon(feet);
            }

            EndAction();
            Telegraphing = false;
        }

        void EndAction()
        {
            m_Action = Action.None;
            m_ActAt = -1f;
            foreach (ParticleSystem glow in m_HandGlows)
                Fx.Release(glow);
            m_HandGlows.Clear();
            Fx.Release(m_PatientMark);
            m_PatientMark = null;
        }

        // Comes up between the healer and the player, so it has to be gone through.
        void Summon(Vector3 feet)
        {
            Vector3 ahead = feet - transform.position;
            ahead.y = 0f;
            ahead = ahead.sqrMagnitude > 1e-4f ? ahead.normalized : transform.forward;
            Vector3 spot = KeepInside(transform.position + ahead * m_SummonDistance);
            if (!NavMesh.SamplePosition(spot, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                m_SummonAt = Time.time + m_SummonRetry;
                return;
            }

            Quaternion facing = Quaternion.LookRotation(ahead);
            m_Summoned = m_Spawner != null
                ? m_Spawner.SpawnExtra(m_Minion, hit.position, facing)
                : Instantiate(m_Minion, hit.position, facing);
            m_Summoned.Died += OnSummonedDied;
            m_Summoned.Emerge(m_EmergeTime);
            SpellFx.Summoning(hit.position, m_SummonColor, m_EmergeTime);
        }

        void OnSummonedDied(Enemy enemy)
        {
            enemy.Died -= OnSummonedDied;
            m_Summoned = null;
            m_SummonAt = Time.time + m_SummonCooldown;
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

        protected override void Interrupt()
        {
            base.Interrupt();
            if (m_Action == Action.Summon)
                m_SummonAt = Time.time + m_SummonRetry;
            EndAction();
            m_ReadyAt = Time.time + m_Cooldown * 0.5f;
        }
    }
}
