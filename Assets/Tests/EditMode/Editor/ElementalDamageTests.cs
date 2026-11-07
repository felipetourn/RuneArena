using NUnit.Framework;
using RuneArena.Enemies;
using Element = RuneArena.Enemies.RangedEnemy.Element;

namespace RuneArena.Tests
{
    public class ElementalDamageTests
    {
        const float k_Strong = 1.5f;
        const float k_Weak = 0.75f;

        [Test]
        public void Multiplier_FireballVsFireMage_IsWeak()
        {
            Assert.AreEqual(k_Weak, ElementalDamage.Multiplier(Element.Fire, "circle", k_Strong, k_Weak));
        }

        [Test]
        public void Multiplier_FireballVsIceMage_IsStrong()
        {
            Assert.AreEqual(k_Strong, ElementalDamage.Multiplier(Element.Ice, "circle", k_Strong, k_Weak));
        }

        [Test]
        public void Multiplier_IceVsIceMage_IsWeak()
        {
            Assert.AreEqual(k_Weak, ElementalDamage.Multiplier(Element.Ice, "v", k_Strong, k_Weak));
        }

        [Test]
        public void Multiplier_IceVsFireMage_IsStrong()
        {
            Assert.AreEqual(k_Strong, ElementalDamage.Multiplier(Element.Fire, "v", k_Strong, k_Weak));
        }

        [Test]
        public void Multiplier_OtherSpellOrPlainMage_IsOne()
        {
            Assert.AreEqual(1f, ElementalDamage.Multiplier(Element.Fire, "lightning", k_Strong, k_Weak));
            Assert.AreEqual(1f, ElementalDamage.Multiplier(Element.Plain, "circle", k_Strong, k_Weak));
            Assert.AreEqual(1f, ElementalDamage.Multiplier(Element.Ice, null, k_Strong, k_Weak));
        }

        // A quenched fireball (x0.75) on a mage it beats (x1.5) still nets out above 1.
        [Test]
        public void Kind_FromNetMultiplier()
        {
            Assert.AreEqual(DamageKind.Combo, ElementalDamage.KindFor(0.75f * 1.5f));
            Assert.AreEqual(DamageKind.Weakened, ElementalDamage.KindFor(0.75f * 0.75f));
            Assert.AreEqual(DamageKind.Normal, ElementalDamage.KindFor(1f));
        }
    }
}
