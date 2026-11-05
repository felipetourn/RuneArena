using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuneArena.Arena.Editor
{
    // Puts the spectators on the colosseum in the open scene: imports their texture as the masks it is, makes
    // their material and adds the Spectators object next to the colosseum. Running it again keeps the tuning.
    static class SpectatorsBuilder
    {
        const string k_Texture = "Assets/Textures/Crowd/Spectators.png";
        const string k_Material = "Assets/Materials/Arena Spectators.mat";

        [MenuItem("RuneArena/Create Spectators")]
        static void Create()
        {
            var colosseum = Object.FindAnyObjectByType<ColosseumBuilder>();
            if (colosseum == null)
            {
                Debug.LogError("Create Spectators: no colosseum in the open scene.");
                return;
            }

            if (AssetImporter.GetAtPath(k_Texture) is TextureImporter importer)
            {
                // Masks, not colors: linear, and without bleeding colors under the cutout.
                importer.sRGBTexture = false;
                importer.alphaIsTransparency = false;
                importer.mipmapEnabled = true;
                // Keeps them from thinning out far away, where the cutout would eat into the smaller mips.
                importer.mipMapsPreserveCoverage = true;
                importer.alphaTestReferenceValue = 0.5f;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Trilinear;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
            }
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(k_Texture);
            if (texture == null)
            {
                Debug.LogError($"Create Spectators: {k_Texture} not found. Make it with tools/textures/generate.py --only Spectators.");
                return;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(k_Material);
            if (material == null)
            {
                material = new Material(Shader.Find("RuneArena/Spectators"));
                AssetDatabase.CreateAsset(material, k_Material);
            }
            material.SetTexture("_BaseMap", texture);
            EditorUtility.SetDirty(material);

            var existing = Object.FindAnyObjectByType<Spectators>(FindObjectsInactive.Include);
            Transform child = existing != null ? existing.transform : null;
            if (child == null)
            {
                var go = new GameObject("Spectators");
                Undo.RegisterCreatedObjectUndo(go, "Create Spectators");
                child = go.transform;
            }
            child.SetPositionAndRotation(colosseum.transform.position, colosseum.transform.rotation);
            var spectators = child.GetComponent<Spectators>();
            if (spectators == null)
                spectators = Undo.AddComponent<Spectators>(child.gameObject);

            var so = new SerializedObject(spectators);
            so.FindProperty("m_Colosseum").objectReferenceValue = colosseum;
            so.FindProperty("m_Material").objectReferenceValue = material;
            so.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(colosseum.gameObject.scene);
            Selection.activeObject = child.gameObject;
        }
    }
}
