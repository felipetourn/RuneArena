using NUnit.Framework;
using RuneArena.Enemies;
using UnityEditor;
using UnityEngine;

namespace RuneArena.Tests
{
    // The ranged mage prefab is built by EnemyModels.RangedMage. These pin what it must come out with.
    public class RangedMagePrefabTests
    {
        const string k_Path = "Assets/Prefabs/Enemies/Ranged Enemy.prefab";

        GameObject m_Mage;

        [SetUp]
        public void SetUp() => m_Mage = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(k_Path));

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(m_Mage);

        [Test]
        public void Prefab_HasOneBody()
        {
            Assert.AreEqual(1, m_Mage.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length);
            Assert.IsNotNull(m_Mage.transform.Find("Body"));
        }

        [Test]
        public void Prefab_AnimatorHasController()
        {
            var animator = m_Mage.GetComponentInChildren<Animator>();
            Assert.IsNotNull(animator.runtimeAnimatorController, "Animator has no controller");
            var enemyAnimator = new SerializedObject(m_Mage.GetComponent<EnemyAnimator>());
            Assert.IsNotNull(enemyAnimator.FindProperty("m_Controller").objectReferenceValue, "EnemyAnimator has no controller");
        }

        // Toes ahead of the foot along the enemy's forward: the body looks where the enemy goes.
        [Test]
        public void Prefab_BodyFacesForward()
        {
            Transform foot = null, toe = null;
            foreach (Transform t in m_Mage.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.EndsWith("LeftFoot"))
                    foot = t;
                else if (t.name.EndsWith("LeftToeBase"))
                    toe = t;
            }
            Assert.IsNotNull(foot, "no LeftFoot bone");
            Assert.IsNotNull(toe, "no LeftToeBase bone");
            Vector3 along = m_Mage.transform.InverseTransformDirection(toe.position - foot.position);
            Assert.Greater(along.z, 0f, $"toes point {along}");
        }
    }
}
