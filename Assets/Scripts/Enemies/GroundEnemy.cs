using UnityEngine;
using UnityEngine.AI;

namespace RuneArena.Enemies
{
    // Walks on the NavMesh. Knockback goes through the agent so it can't push enemies through walls.
    [RequireComponent(typeof(NavMeshAgent))]
    public abstract class GroundEnemy : Enemy
    {
        [SerializeField] float m_RepathInterval = 0.25f;

        protected NavMeshAgent Agent { get; private set; }

        float m_NextRepath;
        float m_BaseSpeed;

        protected override void Awake()
        {
            base.Awake();
            Agent = GetComponent<NavMeshAgent>();
            m_BaseSpeed = Agent.speed;
        }

        protected sealed override void Tick(float deltaTime)
        {
            if (!Agent.isOnNavMesh)
                return;
            Agent.speed = m_BaseSpeed * SpeedMultiplier * SpeedBonus;
            // Rooted: speed 0 isn't enough, the agent would still slide on its current velocity.
            if (HasStatus(EnemyStatus.Rooted))
                Agent.velocity = Vector3.zero;

            // Still in the doorway: walk in before doing anything else, so it can't stop outside and shoot from there.
            if (!HasEntered)
            {
                MoveTo(ArenaRadius > 0f ? ArenaCenter : transform.position);
                return;
            }
            Think(deltaTime);
        }

        protected abstract void Think(float deltaTime);

        // Extra speed a subclass can add on its own, like the boss when it gets angry.
        protected virtual float SpeedBonus => 1f;

        protected void MoveTo(Vector3 destination)
        {
            Agent.isStopped = false;
            if (Time.time < m_NextRepath)
                return;
            Agent.SetDestination(KeepInside(destination));
            m_NextRepath = Time.time + m_RepathInterval;
        }

        protected void Stop()
        {
            Agent.isStopped = true;
            m_NextRepath = 0f;
        }

        protected override void ApplyKnockback(Vector3 offset)
        {
            if (Agent.isOnNavMesh)
                Agent.Move(KeepInside(transform.position + offset) - transform.position);
        }

        protected override void Interrupt()
        {
            base.Interrupt();
            if (Agent != null && Agent.isOnNavMesh)
                Stop();
        }

        protected override void OnDied()
        {
            Agent.enabled = false;
        }
    }
}
