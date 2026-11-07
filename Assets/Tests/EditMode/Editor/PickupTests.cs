using NUnit.Framework;
using RuneArena.Pickups;

namespace RuneArena.Tests
{
    public class PickupTests
    {
        const float k_Warning = 3f;
        const float k_Lifetime = 10f;

        [Test]
        public void WarningProgress_BeforeWarning_IsZero()
        {
            Assert.AreEqual(0f, Pickup.WarningProgress(5f, k_Warning, k_Lifetime), 1e-5f);
        }

        [Test]
        public void WarningProgress_Midway_IsHalf()
        {
            Assert.AreEqual(0.5f, Pickup.WarningProgress(1.5f, k_Warning, k_Lifetime), 1e-5f);
        }

        [Test]
        public void WarningProgress_AtExpiry_IsOne()
        {
            Assert.AreEqual(1f, Pickup.WarningProgress(0f, k_Warning, k_Lifetime), 1e-5f);
        }

        // A stone that lives less than the warning shakes from the start, without jumping in halfway.
        [Test]
        public void WarningProgress_ShortLifetime_StartsAtSpawn()
        {
            Assert.AreEqual(0f, Pickup.WarningProgress(2f, k_Warning, 2f), 1e-5f);
            Assert.AreEqual(0.5f, Pickup.WarningProgress(1f, k_Warning, 2f), 1e-5f);
        }
    }
}
