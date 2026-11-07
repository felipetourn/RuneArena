using NUnit.Framework;
using RuneArena.Game;

namespace RuneArena.Tests
{
    // Proves the edit mode tests compile against the game code and get discovered.
    public class HarnessTests
    {
        [Test]
        public void Harness_SeesGameCode()
        {
            Assert.AreEqual(5, System.Enum.GetValues(typeof(UpgradeTier)).Length);
        }
    }
}
