using NUnit.Framework;
using UnityEditor;

namespace RuneArena.Tests
{
    // The mage's Mixamo clips travel (the walk moves its hips forward). Baked into the pose, that travel
    // pulls the body ahead of the enemy and snaps it back every loop; left out of the pose it becomes root
    // motion, which the animator drops, so the clip plays where the enemy is.
    public class MageClipImportTests
    {
        [TestCase("Assets/Models/Enemies/SkeletonMage/SkeletonMage.fbx")]
        [TestCase("Assets/Models/Enemies/SkeletonMage/Animations/Walk.fbx")]
        [TestCase("Assets/Models/Enemies/SkeletonMage/Animations/Idle.fbx")]
        [TestCase("Assets/Models/Enemies/SkeletonMage/Animations/Hit.fbx")]
        [TestCase("Assets/Models/Enemies/SkeletonMage/Animations/Death.fbx")]
        public void MageClip_TravelNotBakedIntoPose(string path)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            foreach (ModelImporterClipAnimation clip in importer.clipAnimations)
                Assert.IsFalse(clip.lockRootPositionXZ, $"{clip.name} bakes its travel into the pose");
        }
    }
}
