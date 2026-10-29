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

        public float Mana { get; private set; }
        public float MaxMana { get; private set; }
        public bool IsExhausted => Time.time < m_ExhaustedUntil;

        float m_Regen;
        float m_ExhaustedUntil;

        void Start()
        {
            var spawner = FindAnyObjectByType<WaveSpawner>();
            SetDifficulty(spawner != null ? spawner.Difficulty : Difficulty.Normal);
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
            float regen = m_Regen * (IsExhausted ? m_ExhaustRegen : 1f);
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

        public void Add(float amount)
        {
            Mana = Mathf.Min(Mana + amount, MaxMana);
        }
    }
}
