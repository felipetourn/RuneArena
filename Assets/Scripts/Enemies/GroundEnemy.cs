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
            Agent.speed = m_BaseSpeed * SpeedMultiplier;
            Think(deltaTime);
        }

        protected abstract void Think(float deltaTime);

        protected void MoveTo(Vector3 destination)
        {
            Agent.isStopped = false;
            if (Time.time < m_NextRepath)
                return;
            Agent.SetDestination(destination);
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
                Agent.Move(offset);
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
