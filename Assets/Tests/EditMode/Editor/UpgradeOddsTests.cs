using NUnit.Framework;
using RuneArena.Game;

namespace RuneArena.Tests
{
    public class UpgradeOddsTests
    {
        static readonly float[] k_Base = { 42.5f, 35f, 12.5f, 7.5f, 2.5f };
        const float k_SkipShift = 10f;
        const float k_MinBronze = 10f;
        const float k_Better = 35f + 12.5f + 7.5f + 2.5f;

        UpgradeOdds m_Odds;

        [SetUp]
        public void SetUp() => m_Odds = new UpgradeOdds(k_Base, k_SkipShift, k_MinBronze);

        // Cumulative weights: 42.5, 77.5, 90, 97.5, 100.
        [Test]
        public void Roll_PicksByCumulativeWeight()
        {
            Assert.AreEqual(UpgradeTier.Bronze, m_Odds.Roll(UpgradeTier.Bronze, 0f));
            Assert.AreEqual(UpgradeTier.Silver, m_Odds.Roll(UpgradeTier.Bronze, 0.5f));
            Assert.AreEqual(UpgradeTier.Emerald, m_Odds.Roll(UpgradeTier.Bronze, 0.92f));
            Assert.AreEqual(UpgradeTier.Diamond, m_Odds.Roll(UpgradeTier.Bronze, 0.99f));
        }

        // From gold up the total is 22.5: 0.6 of it is 13.5, past gold's 12.5.
        [Test]
        public void Roll_WithMinimumGold_SkipsLowerTiers()
        {
            Assert.AreEqual(UpgradeTier.Gold, m_Odds.Roll(UpgradeTier.Gold, 0f));
            Assert.AreEqual(UpgradeTier.Emerald, m_Odds.Roll(UpgradeTier.Gold, 0.6f));
        }

        [Test]
        public void Skip_MovesFromBronzeProportionally()
        {
            m_Odds.Skip();
            Assert.AreEqual(32.5f, m_Odds.Of(UpgradeTier.Bronze), 1e-4f);
            Assert.AreEqual(35f + k_SkipShift * 35f / k_Better, m_Odds.Of(UpgradeTier.Silver), 1e-4f);
            Assert.AreEqual(7.5f + k_SkipShift * 7.5f / k_Better, m_Odds.Of(UpgradeTier.Emerald), 1e-4f);
        }

        [Test]
        public void Skip_Repeated_NeverBelowMinimumAndKeepsTotal()
        {
            for (int i = 0; i < 10; i++)
                m_Odds.Skip();
            float total = 0f;
            for (int t = 0; t < k_Base.Length; t++)
                total += m_Odds.Of((UpgradeTier)t);
            Assert.AreEqual(k_MinBronze, m_Odds.Of(UpgradeTier.Bronze), 1e-4f);
            Assert.AreEqual(100f, total, 1e-3f);
        }

        // Easy has nothing to unlock, so a rolled emerald has no card and falls to gold.
        [Test]
        public void PickFrom_EmptyEmerald_FallsBackToGold()
        {
            string card = UpgradeOdds.PickFrom(UpgradeTier.Emerald, tier => tier == UpgradeTier.Emerald ? null : tier.ToString());
            Assert.AreEqual("Gold", card);
        }

        // Every special taken: a rolled diamond falls to an unlock first.
        [Test]
        public void PickFrom_EmptyDiamond_FallsBackToEmerald()
        {
            string card = UpgradeOdds.PickFrom(UpgradeTier.Diamond, tier => tier == UpgradeTier.Diamond ? null : tier.ToString());
            Assert.AreEqual("Emerald", card);
        }

        [Test]
        public void PickFrom_NothingAnywhere_IsNull()
        {
            Assert.IsNull(UpgradeOdds.PickFrom<string>(UpgradeTier.Diamond, tier => null));
        }

        [Test]
        public void Reset_RestoresBaseOdds()
        {
            m_Odds.Skip();
            m_Odds.Reset();
            for (int t = 0; t < k_Base.Length; t++)
                Assert.AreEqual(k_Base[t], m_Odds.Of((UpgradeTier)t), 1e-4f);
        }
    }
}
