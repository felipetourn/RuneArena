using System;
using System.Collections;
using System.Collections.Generic;
using RuneArena.Arena;
using RuneArena.Enemies;
using RuneArena.Player;
using RuneArena.Spells;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

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
        [SerializeField] Difficulty m_Difficulty = Difficulty.Easy;
        [Tooltip("Multipliers over the enemy prefabs, which are tuned as Easy.")]
        [SerializeField] DifficultyScale m_Easy = new DifficultyScale { speed = 1f, damage = 1f, health = 1f };
        [SerializeField] DifficultyScale m_Normal = new DifficultyScale { speed = 1.1f, damage = 1.25f, health = 1f };
        [SerializeField] DifficultyScale m_Hard = new DifficultyScale { speed = 1.2f, damage = 1.5f, health = 1.4f };
        [Tooltip("A stage is one pass through the Wave Set, ending with its boss. Enemies are as many levels as the stage, " +
                 "and each level above 1 adds this much health and damage.")]
        [SerializeField] float m_PerLevel = 0.3f;

        [Header("Armor")]
        [Tooltip("Chance for each walking enemy (not bosses) to come armored, once armor types are in play.")]
        [SerializeField, Range(0f, 1f)] float m_ArmorChance = 0.25f;
        [Tooltip("Armor points relative to the enemy's max health.")]
        [SerializeField] float m_ArmorAmount = 0.5f;
        [Tooltip("Stage from which each type shows up: ice, magma, metal.")]
        [SerializeField] int m_IceArmorStage = 2;
        [SerializeField] int m_MagmaArmorStage = 3;
        [SerializeField] int m_MetalArmorStage = 4;
        [SerializeField] bool m_StartOnPlay = true;
        [SerializeField] float m_StartDelay = 3f;
        [Tooltip("Seconds between the end of a wave and the start of the next one.")]
        [SerializeField] float m_BreakTime = 5f;
        [Tooltip("Seconds a gate stays open after letting an enemy through, at least.")]
        [SerializeField] float m_GateHold = 2f;
        [Tooltip("How far in front of the player a boss with an entrance appears.")]
        [SerializeField] float m_EntranceDistance = 9f;
        [SerializeField] Color m_EntranceColor = new Color(1f, 0.45f, 0.15f);

        [Header("Announcement")]
        [Tooltip("Text in the middle of the view. Created if left empty.")]
        [SerializeField] TMP_Text m_Label;
        [Tooltip("Font of the label created in code. Empty uses TextMeshPro's default.")]
        [SerializeField] TMP_FontAsset m_LabelFont;
        [SerializeField] Color m_LabelColor = Color.black;
        [SerializeField] float m_AnnounceTime = 2.5f;

        public event Action<int> WaveStarted;
        public event Action<int> WaveCleared;
        public event Action<Enemy> EnemyDied;

        // Counts from 1. 0 before the first wave.
        public int CurrentWave { get; private set; }
        public int WavesPerStage => m_Waves != null ? m_Waves.Count : 0;

        // Counts from 1: one stage per pass through the Wave Set.
        public int CurrentStage { get; private set; }
        public int Alive { get; private set; }

        // While true, the next wave waits after the current one is cleared (the player is choosing an upgrade).
        public bool HoldBeforeNextWave { get; set; }

        // Set it off before Start when something else (the GameManager) decides when the waves begin.
        public bool StartOnPlay
        {
            get => m_StartOnPlay;
            set => m_StartOnPlay = value;
        }

        // Set it before the waves start; it applies to enemies spawned from then on.
        public Difficulty Difficulty
        {
            get => m_Difficulty;
            set => m_Difficulty = value;
        }

        readonly List<Enemy> m_Queue = new List<Enemy>();
        readonly List<Enemy> m_Bosses = new List<Enemy>();
        readonly List<ArmorType> m_ArmorTypes = new List<ArmorType>();

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

        public void StopWaves()
        {
            StopAllCoroutines();
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

        // first is the wave to start from, counting from 0 across stages.
        IEnumerator Run(int first = 0)
        {
            yield return new WaitForSeconds(m_StartDelay);

            for (int index = first; ; index++)
            {
                CurrentWave = index + 1;
                bool newStage = index % m_Waves.Count == 0;
                CurrentStage = index / m_Waves.Count + 1;
                WaveSet.Wave wave = m_Waves.Get(index, out float multiplier);
                BuildQueue(wave, multiplier, CurrentStage);

                Announce(newStage ? $"Etapa {CurrentStage} - Oleada {CurrentWave}" : $"Oleada {CurrentWave}", m_AnnounceTime);
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

                // The boss comes once the rest of its wave is down.
                if (m_Bosses.Count > 0)
                {
                    foreach (Enemy prefab in m_Bosses)
                        yield return Spawn(prefab);
                    while (Alive > 0)
                    {
                        if (PlayerDead)
                            yield break;
                        yield return null;
                    }
                }

                Announce($"Oleada {CurrentWave} superada", m_AnnounceTime);
                WaveCleared?.Invoke(CurrentWave);
                while (HoldBeforeNextWave)
                    yield return null;
                yield return new WaitForSeconds(m_BreakTime);
            }
        }

        bool PlayerDead => m_Player != null && m_Player.IsDead;

        void BuildQueue(WaveSet.Wave wave, float multiplier, int stage)
        {
            m_Queue.Clear();
            m_Bosses.Clear();
            foreach (WaveSet.Group group in wave.groups)
            {
                if (group.prefab == null || !group.InStage(stage))
                    continue;
                // Bosses don't grow with the loops: one is one.
                if (group.prefab.IsBoss)
                {
                    for (int i = 0; i < group.count; i++)
                        m_Bosses.Add(group.prefab);
                    continue;
                }
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
            if (prefab.EntranceTime > 0f)
            {
                Appear(prefab);
                yield break;
            }

            // Bosses come through the first door, the one in front of the player's start.
            bool boss = prefab.IsBoss;
            ArenaGate gate = boss ? m_Gates[0] : m_Gates[m_NextGate];
            if (!boss)
                m_NextGate = (m_NextGate + 1) % m_Gates.Length;
            Transform point = gate.SpawnPoint;

            gate.Open(m_GateHold);
            while (!gate.IsOpen)
                yield return null;

            Enemy enemy = Track(Instantiate(prefab, point.position, point.rotation));
            gate.LetThrough(enemy);
            gate.Open(m_GateHold);
        }

        // An entrance in the arena instead of a gate, facing the player: a boss in the middle of the arena,
        // anything else in front of the player, kept off the wall.
        void Appear(Enemy prefab)
        {
            Vector3 feet = m_Player.Feet;
            Vector3 ahead = m_Player.Head.forward;
            ahead.y = 0f;
            ahead = ahead.sqrMagnitude > 1e-4f ? ahead.normalized : Vector3.forward;
            Vector3 spot = feet + ahead * m_EntranceDistance;
            if (prefab.IsBoss && m_Arena != null)
                spot = m_Arena.transform.position;
            if (m_Arena != null)
            {
                Vector3 center = m_Arena.transform.position;
                Vector3 flat = spot - center;
                flat.y = 0f;
                float limit = m_Arena.Radius - m_Arena.Thickness - 4f;
                if (flat.magnitude > limit)
                    spot = center + flat.normalized * limit;
                spot.y = center.y;
            }
            if (NavMesh.SamplePosition(spot, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                spot = hit.position;

            Vector3 facing = feet - spot;
            facing.y = 0f;
            Enemy enemy = Track(Instantiate(prefab, spot, Quaternion.LookRotation(facing.sqrMagnitude > 1e-4f ? facing : -ahead)));
            enemy.Emerge(prefab.EntranceTime);
            SpellFx.Summoning(spot, m_EntranceColor, prefab.EntranceTime, 3f);

        }

        [ContextMenu("Test/Boss of stage 1")]
        void TestBoss1() => TestBoss(1);

        [ContextMenu("Test/Boss of stage 2")]
        void TestBoss2() => TestBoss(2);

        [ContextMenu("Test/Boss of stage 3")]
        void TestBoss3() => TestBoss(3);

        // In Play: clears the arena and brings that stage's boss in right away, entrance included. Once it
        // is down the waves go on from the next stage.
        void TestBoss(int stage)
        {
            if (!Application.isPlaying || m_Waves == null || m_Waves.Count == 0)
                return;
            StopAllCoroutines();
            for (int i = Enemy.All.Count - 1; i >= 0; i--)
                Enemy.All[i].Despawn();
            Alive = 0;
            StartCoroutine(Boss(stage));
        }

        IEnumerator Boss(int stage)
        {
            CurrentStage = stage;
            CurrentWave = stage * m_Waves.Count;
            WaveSet.Wave last = m_Waves.Get(m_Waves.Count - 1, out _);
            foreach (WaveSet.Group group in last.groups)
            {
                if (group.prefab != null && group.prefab.IsBoss && group.InStage(stage))
                    Appear(group.prefab);
            }
            while (Alive > 0)
            {
                if (PlayerDead)
                    yield break;
                yield return null;
            }
            yield return Run(stage * m_Waves.Count);
        }

        // For enemies that appear inside the arena mid-wave, like a boss's minions. They count for the wave.
        public Enemy SpawnExtra(Enemy prefab, Vector3 position, Quaternion rotation)
        {
            return Track(Instantiate(prefab, position, rotation));
        }

        Enemy Track(Enemy enemy)
        {
            DifficultyScale scale = m_Difficulty == Difficulty.Hard ? m_Hard : m_Difficulty == Difficulty.Normal ? m_Normal : m_Easy;
            enemy.ApplyDifficulty(scale.speed, scale.damage, scale.health);
            enemy.SetLevel(CurrentStage, m_PerLevel);
            RollArmor(enemy);
            Alive++;
            enemy.Died += OnEnemyDied;
            return enemy;
        }

        void RollArmor(Enemy enemy)
        {
            if (enemy.IsBoss || enemy is FlyingEnemy)
                return;

            m_ArmorTypes.Clear();
            if (CurrentStage >= m_IceArmorStage)
                m_ArmorTypes.Add(ArmorType.Ice);
            if (CurrentStage >= m_MagmaArmorStage)
                m_ArmorTypes.Add(ArmorType.Magma);
            if (CurrentStage >= m_MetalArmorStage)
                m_ArmorTypes.Add(ArmorType.Metal);
            if (m_ArmorTypes.Count == 0 || UnityEngine.Random.value >= m_ArmorChance)
                return;

            ArmorType type = m_ArmorTypes[UnityEngine.Random.Range(0, m_ArmorTypes.Count)];
            enemy.SetArmor(type, m_ArmorAmount);
        }

        void OnEnemyDied(Enemy enemy)
        {
            enemy.Died -= OnEnemyDied;
            Alive--;
            EnemyDied?.Invoke(enemy);
        }

        public void Announce(string text, float seconds = 0f)
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
            if (m_LabelFont != null)
                text.font = m_LabelFont;
            text.color = m_LabelColor;
            text.fontSize = 0.35f;
            text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.sizeDelta = new Vector2(0.6f, 0.08f);
            m_Label = text;
        }
    }
}
