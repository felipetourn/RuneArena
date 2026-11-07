using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace RuneArena.Tests
{
    // The ivy material tiles its textures, so they have to repeat: clamped, everything past the first tile
    // is the edge smeared out.
    public class IvyTextureTests
    {
        [TestCase("Assets/Textures/Fx/Ivy_Albedo.png")]
        [TestCase("Assets/Textures/Fx/Ivy_Normal.png")]
        public void IvyTexture_Repeats(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.AreEqual(TextureWrapMode.Repeat, importer.wrapModeU);
            Assert.AreEqual(TextureWrapMode.Repeat, importer.wrapModeV);
        }
    }
}
