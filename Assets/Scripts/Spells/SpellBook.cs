using System.Collections.Generic;
using UnityEngine;

namespace RuneArena.Spells
{
    // One-off special effects from diamond upgrades.
    public static class SpellPerks
    {
        public const string ChainLightning = "chain-lightning";
        public const string BigExplosion = "big-explosion";
        public const string HighTide = "high-tide";
    }

    // Goes on the right controller with the RuneCaster and the spells. Keeps which spells the player has
    // unlocked and how upgraded each one is. The spells themselves keep their base tuning. Which spells
    // start locked depends on the difficulty, and the game sets it when a run starts.
    public class SpellBook : MonoBehaviour
    {
        readonly HashSet<string> m_Locked = new HashSet<string>();
        readonly Dictionary<string, float> m_Power = new Dictionary<string, float>();
        readonly Dictionary<string, float> m_Cost = new Dictionary<string, float>();
        readonly HashSet<string> m_Perks = new HashSet<string>();

        RuneSpell[] m_Spells;

        public IReadOnlyList<RuneSpell> Spells => m_Spells;

        void Awake()
        {
            m_Spells = GetComponents<RuneSpell>();
        }

        public RuneSpell Find(string label)
        {
            foreach (RuneSpell spell in m_Spells)
            {
                if (spell.Label == label)
                    return spell;
            }
            return null;
        }

        public bool IsUnlocked(string label) => !m_Locked.Contains(label);
        public void Unlock(string label) => m_Locked.Remove(label);

        /// <summary>Locks exactly these spells, unlocking any other.</summary>
        public void SetLocked(IEnumerable<string> labels)
        {
            m_Locked.Clear();
            m_Locked.UnionWith(labels);
        }

        public float Power(string label) => m_Power.TryGetValue(label, out float v) ? v : 1f;
        public float CostMultiplier(string label) => m_Cost.TryGetValue(label, out float v) ? v : 1f;

        public float Cost(RuneSpell spell) => spell.ManaCost * CostMultiplier(spell.Label);

        public void AddPerk(string perk) => m_Perks.Add(perk);
        public bool HasPerk(string perk) => m_Perks.Contains(perk);

        // Upgrades stack additively: two +25% are +50%.
        public void AddPower(string label, float amount) => m_Power[label] = Power(label) + amount;

        // Each cost upgrade takes a fraction off what it costs now, so it never reaches 0.
        public void ReduceCost(string label, float fraction) => m_Cost[label] = CostMultiplier(label) * (1f - fraction);
    }
}
