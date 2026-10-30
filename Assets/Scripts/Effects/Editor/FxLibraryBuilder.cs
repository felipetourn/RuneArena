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
        const string k_Sounds = "Assets/Resources/SfxLibrary.asset";
        const string k_SoundFolder = "Assets/Audio/Sfx";

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
            library.FireSheet = Particles("Fx Fire Sheet", "Fx_FireSheet", true);
            library.SmokeSheet = Particles("Fx Smoke Sheet", "Fx_SmokeSheet", false);
            library.FireCore = Lit("Fx Fire Core", null, new Color(1f, 0.55f, 0.15f), 0.2f, new Color(1f, 0.5f, 0.12f) * 3f, false);
            library.Earth = Lit("Fx Earth", "Assets/Textures/Arena/Earth", Color.white, 0f, Color.black, false);
            library.Earth.SetTextureScale("_BaseMap", new Vector2(1.5f, 1f));
            library.Ivy = Lit("Fx Ivy", k_Textures + "/Ivy", Color.white, 0.25f, Color.black, true);
            library.Ivy.SetTextureScale("_BaseMap", Vector2.one * 2f);

            // The spells' bodies, on the stylized Spell Surface shader.
            library.Ice = Surface("Fx Ice", null, new Color(0.35f, 0.7f, 0.95f), new Color(0.9f, 0.98f, 1f), 0.5f, 2f, new Color(0.8f, 0.95f, 1f, 1f), 2f);
            library.Rock = Surface("Fx Rock", "Assets/Textures/Arena/Earth_Albedo.png", new Color(0.55f, 0.45f, 0.38f), new Color(1.05f, 0.95f, 0.8f), 0f, 3f, new Color(1f, 0.8f, 0.5f, 0.25f), 3f);
            library.Rock.SetTextureScale("_BaseMap", new Vector2(1.5f, 1f));
            library.Vine = Surface("Fx Vine", null, new Color(0.08f, 0.18f, 0.06f), new Color(0.45f, 0.8f, 0.25f), 0.15f, 3f, new Color(0.6f, 1f, 0.3f, 0.6f), 2.5f);
            library.Vine.SetVector("_Wave", new Vector4(0.015f, 3f, 2f, 0f));
            library.Vine.SetColor("_GrowEdge", new Color(0.8f, 1f, 0.4f));
            library.Thorn = Surface("Fx Thorn", null, new Color(0.7f, 0.62f, 0.4f), new Color(0.85f, 0.8f, 0.6f), 0.1f, 3f, new Color(1f, 1f, 0.8f, 0.2f), 3f);
            library.Thorn.SetColor("_GrowEdge", new Color(0.8f, 1f, 0.4f));
            library.Water = Surface("Fx Water", k_Textures + "/Fx_Water.png", Color.white, Color.white, 0.35f, 0f, new Color(0.7f, 0.95f, 1f, 0.6f), 2.5f);
            library.Water.SetVector("_Wave", new Vector4(0.03f, 6f, 5f, 0f));
            Transparent(library.Water, 0.9f);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();

            CreateSounds();

            var recorder = Object.FindAnyObjectByType<StrokeRecorder>(FindObjectsInactive.Include);
            if (recorder != null && recorder.GetComponent<RuneVisuals>() == null)
            {
                Undo.AddComponent<RuneVisuals>(recorder.gameObject);
                EditorSceneManager.MarkSceneDirty(recorder.gameObject.scene);
            }
        }

        // Every clip field of the SfxLibrary is filled from the file with the same name, without the
        // underscores: RuneCharged from Rune_Charged.wav.
        static void CreateSounds()
        {
            var library = AssetDatabase.LoadAssetAtPath<SfxLibrary>(k_Sounds);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<SfxLibrary>();
                AssetDatabase.CreateAsset(library, k_Sounds);
            }
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { k_SoundFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string field = System.IO.Path.GetFileNameWithoutExtension(path).Replace("_", "");
                var info = typeof(SfxLibrary).GetField(field);
                if (info != null)
                    info.SetValue(library, AssetDatabase.LoadAssetAtPath<AudioClip>(path));
            }
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
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

        static Material Surface(string name, string texture, Color baseColor, Color tipColor, float emission, float bands, Color rim, float rimPower)
        {
            string path = $"{k_Materials}/{name}.mat";
            var shader = Shader.Find("RuneArena/Spell Surface");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            // An older version of this material may be on another shader.
            material.shader = shader;
            material.SetTexture("_BaseMap", texture != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(texture) : null);
            material.SetColor("_BaseColor", baseColor);
            material.SetColor("_TipColor", tipColor);
            material.SetFloat("_Emission", emission);
            material.SetFloat("_Bands", bands);
            material.SetColor("_RimColor", rim);
            material.SetFloat("_RimPower", rimPower);
            EditorUtility.SetDirty(material);
            return material;
        }

        // See-through and two-sided, drawn after the solid scene.
        static void Transparent(Material material, float alpha)
        {
            material.SetFloat("_Alpha", alpha);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetOverrideTag("RenderType", "Transparent");
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
