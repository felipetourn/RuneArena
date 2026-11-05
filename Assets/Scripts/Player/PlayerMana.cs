using System;
using RuneArena.Waves;
using UnityEngine;

namespace RuneArena.Player
{
    // Goes on the XR Origin. With any mana left a rune can be cast even if it costs more (overcast):
    // mana drops to 0 and regenerates slower for a while. While exhausted like that, runes need their full cost.
    public class PlayerMana : MonoBehaviour
    {
        [Serializable]
        struct ManaSettings
        {
            public float max;
            [Tooltip("Mana per second.")]
            public float regen;
        }

        [SerializeField] ManaSettings m_Easy = new ManaSettings { max = 100f, regen = 5f };
        [SerializeField] ManaSettings m_Normal = new ManaSettings { max = 85f, regen = 4f };
        [SerializeField] ManaSettings m_Hard = new ManaSettings { max = 70f, regen = 3f };

        [Header("Running out")]
        [SerializeField] float m_ExhaustTime = 5f;
        [Tooltip("Regeneration multiplier while exhausted.")]
        [SerializeField, Range(0f, 1f)] float m_ExhaustRegen = 0.5f;

        [Header("Clearing a wave")]
        [Tooltip("Fraction of the missing mana given back at once when a wave is cleared. Also ends exhaustion.")]
        [SerializeField, Range(0f, 1f)] float m_WaveRestore = 0.5f;

        public float Mana { get; private set; }
        public float MaxMana { get; private set; }
        public bool IsExhausted => Time.time < m_ExhaustedUntil;

        float m_Regen;
        float m_RegenScale = 1f;
        float m_ExhaustedUntil;
        WaveSpawner m_Spawner;

        void Start()
        {
            m_Spawner = FindAnyObjectByType<WaveSpawner>();
            SetDifficulty(m_Spawner != null ? m_Spawner.Difficulty : Difficulty.Easy);
            if (m_Spawner != null)
                m_Spawner.WaveCleared += OnWaveCleared;
        }

        void OnDestroy()
        {
            if (m_Spawner != null)
                m_Spawner.WaveCleared -= OnWaveCleared;
        }

        void OnWaveCleared(int wave)
        {
            Mana += (MaxMana - Mana) * m_WaveRestore;
            m_ExhaustedUntil = 0f;
        }

        // Also refills the mana.
        public void SetDifficulty(Difficulty difficulty)
        {
            ManaSettings settings = difficulty == Difficulty.Hard ? m_Hard : difficulty == Difficulty.Normal ? m_Normal : m_Easy;
            MaxMana = settings.max;
            m_Regen = settings.regen;
            Mana = MaxMana;
            m_ExhaustedUntil = 0f;
        }

        void Update()
        {
            float regen = m_Regen * m_RegenScale * (IsExhausted ? m_ExhaustRegen : 1f);
            Mana = Mathf.Min(Mana + regen * Time.deltaTime, MaxMana);
        }

        public bool CanCast(float cost)
        {
            return IsExhausted ? Mana >= cost : Mana > 0f;
        }

        public void Spend(float cost)
        {
            Mana -= cost;
            if (Mana <= 0f)
            {
                Mana = 0f;
                m_ExhaustedUntil = Time.time + m_ExhaustTime;
            }
        }

        public void AddMax(float amount)
        {
            MaxMana += amount;
            Mana += amount;
        }

        // Upgrades stack additively: two +20% are +40%.
        public void AddRegen(float fraction)
        {
            m_RegenScale += fraction;
        }

        public void Add(float amount)
        {
            Mana = Mathf.Min(Mana + amount, MaxMana);
        }
    }
}
