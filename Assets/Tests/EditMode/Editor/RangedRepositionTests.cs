using NUnit.Framework;
using RuneArena.Enemies;
using UnityEngine;

namespace RuneArena.Tests
{
    // Between shots the ranged mage walks around the player, so where it goes must keep its distance.
    public class RangedRepositionTests
    {
        static readonly Vector3 k_Player = new Vector3(2f, 0f, -1f);
        static readonly Vector3 k_Mage = new Vector3(2f, 0.1f, 9f);

        [TestCase(30f)]
        [TestCase(-30f)]
        public void SpotAround_KeepsTheDistanceToThePlayer(float degrees)
        {
            Vector3 spot = RangedEnemy.SpotAround(k_Mage, k_Player, degrees);

            Assert.AreEqual(Flat(k_Mage - k_Player).magnitude, Flat(spot - k_Player).magnitude, 1e-4f);
            Assert.AreEqual(k_Mage.y, spot.y, 1e-4f, "the spot leaves the mage's height alone");
        }

        [TestCase(30f)]
        [TestCase(-30f)]
        public void SpotAround_MovesThatManyDegreesAroundThePlayer(float degrees)
        {
            Vector3 spot = RangedEnemy.SpotAround(k_Mage, k_Player, degrees);

            float turned = Vector3.SignedAngle(Flat(k_Mage - k_Player), Flat(spot - k_Player), Vector3.up);
            Assert.AreEqual(degrees, turned, 0.01f);
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
