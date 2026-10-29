using System.Collections.Generic;
using RuneArena.Enemies;
using UnityEngine;
using UnityEngine.AI;

namespace RuneArena.Arena
{
    // A door of the colosseum. The gate sinks into the floor to open and comes back up to close.
    // While it isn't fully open it carves the NavMesh, so walking enemies only path through an open door.
    public class ArenaGate : MonoBehaviour
    {
        [SerializeField] Transform m_Gate;
        [SerializeField] Transform m_SpawnPoint;
        [SerializeField] float m_MoveTime = 1f;

        readonly List<Enemy> m_Passing = new List<Enemy>();

        NavMeshObstacle m_Obstacle;
        float m_ClosedY;
        float m_Travel;
        float m_Open;
        bool m_WantOpen;
        float m_HoldUntil;

        public Transform SpawnPoint => m_SpawnPoint;
        public bool IsOpen => m_Open >= 1f;

        public void Setup(Transform gate, Transform spawnPoint)
        {
            m_Gate = gate;
            m_SpawnPoint = spawnPoint;
        }

        void Awake()
        {
            m_ClosedY = m_Gate.localPosition.y;
            m_Travel = m_Gate.localScale.y;
            m_Obstacle = m_Gate.GetComponent<NavMeshObstacle>();
        }

        // Opens and stays open for at least seconds, and until every enemy let through is inside.
        public void Open(float seconds)
        {
            m_WantOpen = true;
            m_HoldUntil = Mathf.Max(m_HoldUntil, Time.time + seconds);
        }

        public void LetThrough(Enemy enemy)
        {
            m_Passing.Add(enemy);
        }

        void Update()
        {
            for (int i = m_Passing.Count - 1; i >= 0; i--)
            {
                Enemy enemy = m_Passing[i];
                if (enemy == null || enemy.IsDead || enemy.IsInsideArena)
                    m_Passing.RemoveAt(i);
            }

            if (m_WantOpen && Time.time >= m_HoldUntil && m_Passing.Count == 0)
                m_WantOpen = false;

            float target = m_WantOpen ? 1f : 0f;
            if (Mathf.Approximately(m_Open, target))
                return;

            m_Open = Mathf.MoveTowards(m_Open, target, Time.deltaTime / m_MoveTime);
            Vector3 p = m_Gate.localPosition;
            p.y = m_ClosedY - m_Travel * m_Open;
            m_Gate.localPosition = p;
            if (m_Obstacle != null)
                m_Obstacle.enabled = m_Open < 1f;
        }
    }
}
