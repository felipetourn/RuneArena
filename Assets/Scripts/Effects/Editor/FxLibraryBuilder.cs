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
            library.FireCore = Lit("Fx Fire Core", null, new Color(1f, 0.55f, 0.15f), 0.2f, new Color(1f, 0.5f, 0.12f) * 3f, false);
            library.Earth = Lit("Fx Earth", "Assets/Textures/Arena/Earth", Color.white, 0f, Color.black, false);
            library.Earth.SetTextureScale("_BaseMap", new Vector2(1.5f, 1f));
            library.Ivy = Lit("Fx Ivy", k_Textures + "/Ivy", Color.white, 0.25f, Color.black, true);
            library.Ivy.SetTextureScale("_BaseMap", Vector2.one * 2f);
            library.Ice = Lit("Fx Ice", null, new Color(0.65f, 0.88f, 1f), 0.92f, new Color(0.2f, 0.45f, 0.6f), false);
            library.Water = Particles("Fx Water", "Fx_Water", false);
            library.Vine = Lit("Fx Vine", null, new Color(0.16f, 0.3f, 0.12f), 0.35f, Color.black, false);
            library.Thorn = Lit("Fx Thorn", null, new Color(0.78f, 0.72f, 0.52f), 0.4f, Color.black, false);
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

        // Lit surface. texture is a path without the _Albedo/_Normal ending, or null for a plain color.
        // A black emission leaves it off.
        static Material Lit(string name, string texture, Color color, float smoothness, Color emission, bool cutout)
        {
            string path = $"{k_Materials}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            if (texture != null)
            {
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texture + "_Albedo.png"));
                var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(texture + "_Normal.png");
                if (normal != null)
                {
                    material.SetTexture("_BumpMap", normal);
                    material.EnableKeyword("_NORMALMAP");
                }
            }
            if (emission.maxColorComponent > 0f)
            {
                material.SetColor("_EmissionColor", emission);
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            if (cutout)
            {
                material.SetFloat("_AlphaClip", 1f);
                material.SetFloat("_Cutoff", 0.5f);
                material.SetFloat("_Cull", 0f);
                material.EnableKeyword("_ALPHATEST_ON");
                material.renderQueue = (int)RenderQueue.AlphaTest;
                material.SetOverrideTag("RenderType", "TransparentCutout");
            }
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
