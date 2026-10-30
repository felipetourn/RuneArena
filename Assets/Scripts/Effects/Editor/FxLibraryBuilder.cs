using RuneArena.Strokes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace RuneArena.Effects.Editor
{
    // Makes the particle materials from the sprites tools/textures/generate.py writes, the FxLibrary
    // that holds them, and adds the rune effects to the right controller.
    static class FxLibraryBuilder
    {
        const string k_Textures = "Assets/Textures/Fx";
        const string k_Materials = "Assets/Materials/Fx";
        const string k_Library = "Assets/Resources/FxLibrary.asset";

        [MenuItem("RuneArena/Create Effects")]
        static void Create()
        {
            EnsureFolder(k_Materials);
            EnsureFolder("Assets/Resources");

            var library = AssetDatabase.LoadAssetAtPath<FxLibrary>(k_Library);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<FxLibrary>();
                AssetDatabase.CreateAsset(library, k_Library);
            }

            library.Glow = Particles("Fx Glow", "Fx_Glow", true);
            library.Spark = Particles("Fx Spark", "Fx_Spark", true);
            library.Puff = Particles("Fx Puff", "Fx_Puff", false);
            library.Flame = Particles("Fx Flame", "Fx_Puff", true);
            library.Ring = Particles("Fx Ring", "Fx_Ring", true);
            library.Line = Particles("Fx Line", "Fx_Line", true);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();

            var recorder = Object.FindAnyObjectByType<StrokeRecorder>(FindObjectsInactive.Include);
            if (recorder != null && recorder.GetComponent<RuneVisuals>() == null)
            {
                Undo.AddComponent<RuneVisuals>(recorder.gameObject);
                EditorSceneManager.MarkSceneDirty(recorder.gameObject.scene);
            }
        }

        // URP's unlit particle shader, transparent, drawn from both sides. Additive for things that
        // glow (they only ever brighten), alpha-blended for smoke and dust.
        static Material Particles(string name, string texture, bool additive)
        {
            string path = $"{k_Materials}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{k_Textures}/{texture}.png"));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", additive ? 2f : 0f);
            material.SetFloat("_SrcBlend", (float)(additive ? BlendMode.One : BlendMode.SrcAlpha));
            material.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;
            string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
        }
    }
}
