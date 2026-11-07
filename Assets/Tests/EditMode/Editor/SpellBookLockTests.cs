using NUnit.Framework;
using RuneArena.Spells;
using UnityEngine;

namespace RuneArena.Tests
{
    public class SpellBookLockTests
    {
        GameObject m_Go;
        SpellBook m_Book;

        [SetUp]
        public void SetUp()
        {
            m_Go = new GameObject("SpellBook Test");
            m_Book = m_Go.AddComponent<SpellBook>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(m_Go);

        [Test]
        public void SetLocked_LocksOnlyListed()
        {
            m_Book.SetLocked(new[] { "spiral", "v" });
            Assert.IsFalse(m_Book.IsUnlocked("spiral"));
            Assert.IsFalse(m_Book.IsUnlocked("v"));
            Assert.IsTrue(m_Book.IsUnlocked("circle"));
        }

        // A run in hard after one in normal, say: the new list replaces the old one.
        [Test]
        public void SetLocked_ReplacesPreviousLocks()
        {
            m_Book.SetLocked(new[] { "spiral", "v" });
            m_Book.SetLocked(new[] { "spiral" });
            Assert.IsTrue(m_Book.IsUnlocked("v"));
            Assert.IsFalse(m_Book.IsUnlocked("spiral"));
        }

        [Test]
        public void Unlock_AfterSetLocked_Unlocks()
        {
            m_Book.SetLocked(new[] { "spiral" });
            m_Book.Unlock("spiral");
            Assert.IsTrue(m_Book.IsUnlocked("spiral"));
        }
    }
}
