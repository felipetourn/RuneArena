using System;

namespace RuneArena.Game
{
    // Odds of each upgrade tier, in points. Skipping moves points from bronze to the better tiers,
    // each in proportion to its base odds (like the rare card offset in Slay the Spire).
    public class UpgradeOdds
    {
        readonly float[] m_Base;
        readonly float[] m_Odds;
        readonly float m_SkipShift;
        readonly float m_MinBronze;

        public UpgradeOdds(float[] baseOdds, float skipShift, float minBronze)
        {
            m_Base = (float[])baseOdds.Clone();
            m_Odds = (float[])baseOdds.Clone();
            m_SkipShift = skipShift;
            m_MinBronze = minBronze;
        }

        /// <summary>Points of that tier now.</summary>
        public float Of(UpgradeTier tier) => m_Odds[(int)tier];

        /// <summary>A tier at or above minimum. Tiers below it are left out, so the rest keep their odds relative to each other.</summary>
        public UpgradeTier Roll(UpgradeTier minimum, float random01)
        {
            float total = 0f;
            for (int i = (int)minimum; i < m_Odds.Length; i++)
                total += m_Odds[i];
            float roll = random01 * total;
            for (int i = (int)minimum; i < m_Odds.Length; i++)
            {
                roll -= m_Odds[i];
                if (roll < 0f)
                    return (UpgradeTier)i;
            }
            return (UpgradeTier)(m_Odds.Length - 1);
        }

        public void Skip()
        {
            float shift = Math.Min(m_SkipShift, m_Odds[0] - m_MinBronze);
            if (shift <= 0f)
                return;
            float better = 0f;
            for (int i = 1; i < m_Base.Length; i++)
                better += m_Base[i];
            m_Odds[0] -= shift;
            for (int i = 1; i < m_Odds.Length; i++)
                m_Odds[i] += shift * m_Base[i] / better;
        }

        public void Reset() => Array.Copy(m_Base, m_Odds, m_Odds.Length);

        /// <summary>A card of the rolled tier, or of the first tier below it that has one: a tier with nothing left
        /// (no spells to unlock on easy, every special taken) falls back. Null if no tier has a card.</summary>
        public static T PickFrom<T>(UpgradeTier rolled, Func<UpgradeTier, T> pick) where T : class
        {
            for (int tier = (int)rolled; tier >= 0; tier--)
            {
                T card = pick((UpgradeTier)tier);
                if (card != null)
                    return card;
            }
            return null;
        }
    }
}
