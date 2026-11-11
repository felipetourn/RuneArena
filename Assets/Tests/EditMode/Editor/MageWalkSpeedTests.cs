using NUnit.Framework;
using RuneArena.Enemies;
using RuneArena.Enemies.Editor;
using UnityEditor;
using UnityEngine;

namespace RuneArena.Tests
{
    // The mage's walk plays faster or slower with how fast it moves, taking Move Speed as the speed its
    // stride covers at its own pace. If that is off, the feet don't keep up with the body and it slides.
    public class MageWalkSpeedTests
    {
        const string k_Path = "Assets/Prefabs/Enemies/Ranged Enemy.prefab";

        GameObject m_Mage;

        [SetUp]
        public void SetUp() => m_Mage = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(k_Path));

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(m_Mage);

        [Test]
        public void MoveSpeed_MatchesWalkStride()
        {
            var animator = m_Mage.GetComponentInChildren<Animator>();
            float stride = EnemyModels.StrideSpeed(animator, "Walk");
            Assert.Greater(stride, 0.1f, "the walk clip travels nowhere: root motion was not measured");

            float moveSpeed = new SerializedObject(m_Mage.GetComponent<EnemyAnimator>()).FindProperty("m_MoveSpeed").floatValue;
            Assert.AreEqual(stride, moveSpeed, stride * 0.1f, $"the walk covers {stride:0.00} m/s, Move Speed says {moveSpeed:0.00}");
        }
    }
}
