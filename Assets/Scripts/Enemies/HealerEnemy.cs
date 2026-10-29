using RuneArena.Spells;
using RuneArena.Strokes;
using UnityEngine;
using UnityEngine.AI;

namespace RuneArena.Enemies
{
    // Stays away from the player and heals the most hurt ally it can see, after a visible wind-up.
    // It never heals itself, so it is the one to kill first.
    public class HealerEnemy : GroundEnemy
    {
        static Material s_BeamMaterial;

        [SerializeField] float m_HealAmount = 15f;
        [SerializeField] float m_HealRange = 8f;
        [SerializeField] float m_SearchRadius = 25f;
        [SerializeField] float m_WindUp = 1f;
        [SerializeField] float m_Cooldown = 4f;
        [Tooltip("Gets away when the player comes closer than this.")]
        [SerializeField] float m_SafeDistance = 7f;
        [SerializeField] LayerMask m_SightMask;
        [SerializeField] Color m_BeamColor = new Color(0.3f, 1f, 0.4f);

        Enemy m_Target;
        float m_HealAt = -1f;
        float m_ReadyAt;

        protected override void Start()
        {
            base.Start();
            m_ReadyAt = Time.time + m_Cooldown * 0.5f;
        }

        protected override void Think(float deltaTime)
        {
            if (m_HealAt >= 0f)
            {
                Stop();
                if (m_Target == null || m_Target.IsDead)
                {
                    Interrupt();
                    return;
                }
                FaceTowards(m_Target.transform.position, 360f, deltaTime);
                if (Time.time >= m_HealAt)
                {
                    m_Target.Heal(m_HealAmount);
                    Beam(m_Target);
                    m_HealAt = -1f;
                    m_ReadyAt = Time.time + m_Cooldown;
                    Telegraphing = false;
                }
                return;
            }

            Vector3 feet = Player.Feet;
            float toPlayer = FlatDistance(transform.position, feet);
            m_Target = MostHurtAlly();

            if (m_Target != null)
            {
                float toTarget = Vector3.Distance(transform.position, m_Target.transform.position);
                if (toTarget > m_HealRange || !CanSee(m_Target))
                {
                    MoveTo(m_Target.transform.position);
                    return;
                }
                if (Time.time >= m_ReadyAt)
                {
                    Stop();
                    m_HealAt = Time.time + m_WindUp;
                    Telegraphing = true;
                    return;
                }
            }

            if (toPlayer < m_SafeDistance && TryRetreat(feet, toPlayer))
                return;
            Stop();
            FaceTowards(feet, 360f, deltaTime);
        }

        Enemy MostHurtAlly()
        {
            Enemy best = null;
            float lowest = 1f;
            foreach (Enemy enemy in All)
            {
                if (enemy == this || enemy.IsDead || !enemy.IsInsideArena)
                    continue;
                float fraction = enemy.Health / enemy.MaxHealth;
                if (fraction >= lowest)
                    continue;
                if ((enemy.transform.position - transform.position).sqrMagnitude > m_SearchRadius * m_SearchRadius)
                    continue;
                best = enemy;
                lowest = fraction;
            }
            return best;
        }

        bool CanSee(Enemy enemy)
        {
            Vector3 from = transform.position + Vector3.up * 1.2f;
            Vector3 to = enemy.transform.position + Vector3.up * 0.8f;
            return !Physics.Linecast(from, to, m_SightMask, QueryTriggerInteraction.Ignore);
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

        void Beam(Enemy target)
        {
            if (s_BeamMaterial == null)
            {
                var recorder = FindAnyObjectByType<StrokeRecorder>(FindObjectsInactive.Include);
                s_BeamMaterial = recorder != null ? recorder.LineMaterial : null;
            }

            var go = new GameObject("Heal Beam");
            var line = go.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.SetPosition(0, transform.position + Vector3.up * 1.6f);
            line.SetPosition(1, target.transform.position + Vector3.up * 0.9f);
            line.widthMultiplier = 0.06f;
            if (s_BeamMaterial != null)
                RuneSpell.Tint(line, s_BeamMaterial, m_BeamColor);
            Destroy(go, 0.3f);
        }

        protected override void Interrupt()
        {
            base.Interrupt();
            m_HealAt = -1f;
            m_ReadyAt = Time.time + m_Cooldown * 0.5f;
        }
    }
}
