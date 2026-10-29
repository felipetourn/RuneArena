using System;
using System.Collections;
using System.Collections.Generic;
using RuneArena.Arena;
using RuneArena.Enemies;
using RuneArena.Player;
using TMPro;
using UnityEngine;

namespace RuneArena.Waves
{
    public enum Difficulty
    {
        Easy,
        Normal,
        Hard,
    }

    // Runs the waves: opens a gate, lets each enemy in through it, and starts the next wave
    // a few seconds after the last enemy dies.
    public class WaveSpawner : MonoBehaviour
    {
        [Serializable]
        struct DifficultyScale
        {
            [Tooltip("Movement speed multiplier.")]
            public float speed;
            public float damage;
            public float health;
        }

        [SerializeField] WaveSet m_Waves;
        [SerializeField] Difficulty m_Difficulty = Difficulty.Normal;
        [Tooltip("Multipliers over the enemy prefabs, which are tuned as Easy.")]
        [SerializeField] DifficultyScale m_Easy = new DifficultyScale { speed = 1f, damage = 1f, health = 1f };
        [SerializeField] DifficultyScale m_Normal = new DifficultyScale { speed = 1.1f, damage = 1.25f, health = 1f };
        [SerializeField] DifficultyScale m_Hard = new DifficultyScale { speed = 1.2f, damage = 1.5f, health = 1.4f };
        [SerializeField] bool m_StartOnPlay = true;
        [SerializeField] float m_StartDelay = 3f;
        [Tooltip("Seconds between the end of a wave and the start of the next one.")]
        [SerializeField] float m_BreakTime = 5f;
        [Tooltip("Seconds a gate stays open after letting an enemy through, at least.")]
        [SerializeField] float m_GateHold = 2f;

        [Header("Announcement")]
        [Tooltip("Text in the middle of the view. Created if left empty.")]
        [SerializeField] TMP_Text m_Label;
        [SerializeField] float m_AnnounceTime = 2.5f;

        public event Action<int> WaveStarted;
        public event Action<int> WaveCleared;
        public event Action<Enemy> EnemyDied;

        // Counts from 1. 0 before the first wave.
        public int CurrentWave { get; private set; }
        public int Alive { get; private set; }

        // Set it before the waves start; it applies to enemies spawned from then on.
        public Difficulty Difficulty
        {
            get => m_Difficulty;
            set => m_Difficulty = value;
        }

        readonly List<Enemy> m_Queue = new List<Enemy>();

        ColosseumBuilder m_Arena;
        ArenaGate[] m_Gates;
        PlayerHealth m_Player;
        int m_NextGate;
        float m_HideLabelAt;

        void Start()
        {
            m_Arena = FindAnyObjectByType<ColosseumBuilder>();
            m_Gates = m_Arena != null ? m_Arena.GetComponentsInChildren<ArenaGate>() : new ArenaGate[0];
            m_Player = FindAnyObjectByType<PlayerHealth>();
            m_NextGate = UnityEngine.Random.Range(0, Mathf.Max(m_Gates.Length, 1));

            if (m_Label == null && m_Player != null)
                CreateLabel(m_Player.Head);
            Announce("");

            if (m_Waves == null || m_Waves.Count == 0 || m_Gates.Length == 0)
            {
                Debug.LogWarning("WaveSpawner: needs a Wave Set with waves and a colosseum with doors", this);
                return;
            }

            if (m_StartOnPlay)
                StartWaves();
        }

        public void StartWaves()
        {
            StopAllCoroutines();
            StartCoroutine(Run());
        }

        void Update()
        {
            if (m_Label != null && m_HideLabelAt > 0f && Time.time >= m_HideLabelAt)
                Announce("");
        }

        IEnumerator Run()
        {
            yield return new WaitForSeconds(m_StartDelay);

            for (int index = 0; ; index++)
            {
                CurrentWave = index + 1;
                WaveSet.Wave wave = m_Waves.Get(index, out float multiplier);
                BuildQueue(wave, multiplier);

                Announce($"Oleada {CurrentWave}", m_AnnounceTime);
                WaveStarted?.Invoke(CurrentWave);

                foreach (Enemy prefab in m_Queue)
                {
                    if (PlayerDead)
                        yield break;
                    yield return Spawn(prefab);
                    yield return new WaitForSeconds(wave.spawnInterval);
                }

                while (Alive > 0)
                {
                    if (PlayerDead)
                        yield break;
                    yield return null;
                }

                Announce($"Oleada {CurrentWave} superada", m_AnnounceTime);
                WaveCleared?.Invoke(CurrentWave);
                yield return new WaitForSeconds(m_BreakTime);
            }
        }

        bool PlayerDead => m_Player != null && m_Player.IsDead;

        void BuildQueue(WaveSet.Wave wave, float multiplier)
        {
            m_Queue.Clear();
            foreach (WaveSet.Group group in wave.groups)
            {
                if (group.prefab == null)
                    continue;
                int count = Mathf.CeilToInt(group.count * multiplier);
                for (int i = 0; i < count; i++)
                    m_Queue.Add(group.prefab);
            }

            // Mixed order, so a wave doesn't send all of one type first.
            for (int i = m_Queue.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (m_Queue[i], m_Queue[j]) = (m_Queue[j], m_Queue[i]);
            }
        }

        IEnumerator Spawn(Enemy prefab)
        {
            ArenaGate gate = m_Gates[m_NextGate];
            m_NextGate = (m_NextGate + 1) % m_Gates.Length;
            Transform point = gate.SpawnPoint;

            gate.Open(m_GateHold);
            while (!gate.IsOpen)
                yield return null;

            Enemy enemy = Instantiate(prefab, point.position, point.rotation);
            DifficultyScale scale = m_Difficulty == Difficulty.Hard ? m_Hard : m_Difficulty == Difficulty.Normal ? m_Normal : m_Easy;
            enemy.ApplyDifficulty(scale.speed, scale.damage, scale.health);
            gate.LetThrough(enemy);
            gate.Open(m_GateHold);

            Alive++;
            enemy.Died += OnEnemyDied;
        }

        void OnEnemyDied(Enemy enemy)
        {
            enemy.Died -= OnEnemyDied;
            Alive--;
            EnemyDied?.Invoke(enemy);
        }

        void Announce(string text, float seconds = 0f)
        {
            if (m_Label == null)
                return;
            m_Label.text = text;
            m_HideLabelAt = seconds > 0f ? Time.time + seconds : 0f;
        }

        void CreateLabel(Transform head)
        {
            var go = new GameObject("WaveLabel");
            go.transform.SetParent(head, false);
            go.transform.localPosition = new Vector3(0f, 0.12f, 0.6f);

            var text = go.AddComponent<TextMeshPro>();
            text.fontSize = 0.35f;
            text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.sizeDelta = new Vector2(0.6f, 0.08f);
            m_Label = text;
        }
    }
}
