using System;
using System.Collections.Generic;
using RuneArena.Enemies;
using UnityEngine;

namespace RuneArena.Waves
{
    [CreateAssetMenu(menuName = "RuneArena/Wave Set", fileName = "Waves")]
    public class WaveSet : ScriptableObject
    {
        [Serializable]
        public class Group
        {
            public Enemy prefab;
            public int count = 1;
            [Tooltip("First stage it shows up in.")]
            public int fromStage = 1;
            [Tooltip("From then on, once every this many stages: 1 is every stage, 2 every other one.")]
            public int everyStages = 1;

            public bool InStage(int stage)
            {
                int first = Mathf.Max(fromStage, 1);
                return stage >= first && (stage - first) % Mathf.Max(everyStages, 1) == 0;
            }
        }

        [Serializable]
        public class Wave
        {
            public List<Group> groups = new List<Group>();
            [Tooltip("Seconds between one enemy and the next.")]
            public float spawnInterval = 2f;
        }

        [SerializeField] List<Wave> m_Waves = new List<Wave>();
        [Tooltip("After the last wave the list starts over, with counts multiplied by 1 + this for each loop.")]
        [SerializeField] float m_LoopGrowth = 0.3f;

        public int Count => m_Waves.Count;

        // index counts from 0 and keeps going past the end of the list.
        public Wave Get(int index, out float countMultiplier)
        {
            int loop = index / m_Waves.Count;
            countMultiplier = 1f + loop * m_LoopGrowth;
            return m_Waves[index % m_Waves.Count];
        }

        public void SetWaves(List<Wave> waves, float loopGrowth)
        {
            m_Waves = waves;
            m_LoopGrowth = loopGrowth;
        }
    }
}
