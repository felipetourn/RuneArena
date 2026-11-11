using System.Collections.Generic;
using RuneArena.Waves;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace RuneArena.Game.Editor
{
    // Makes the TextMeshPro font assets from the game's fonts. Cinzel becomes TextMeshPro's default (what every
    // text made in code gets) and goes on the texts already in the open scenes; Metamorphous only goes on the
    // wave label. Each font falls back to the one before it for any character it doesn't have.
    static class GameFont
    {
        const string k_MainFont = "Assets/Fonts/cinzel.decorative-regular.ttf";
        const string k_MainAsset = "Assets/Fonts/Cinzel Decorative SDF.asset";
        const string k_LabelFont = "Assets/Fonts/metamorphous.regular.ttf";
        const string k_LabelAsset = "Assets/Fonts/Metamorphous SDF.asset";
        // Baked into the atlas up front, so the headset doesn't render them mid-game. Anything else is added
        // the first time it shows up.
        const string k_Characters = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~¡¿áéíóúüñÁÉÍÓÚÜÑ×·…—";

        [MenuItem("RuneArena/Apply Game Font")]
        static void Apply()
        {
            var settings = Resources.Load<TMP_Settings>("TMP Settings");
            if (settings == null)
            {
                Debug.LogError("Apply Game Font: TMP Settings not found. Import the TextMeshPro essentials first.");
                return;
            }
            var settingsSo = new SerializedObject(settings);
            SerializedProperty defaultFont = settingsSo.FindProperty("m_defaultFontAsset");
            var previous = defaultFont.objectReferenceValue as TMP_FontAsset;

            TMP_FontAsset label = GetOrCreate(k_LabelFont, k_LabelAsset, "Metamorphous", previous);
            if (label == null)
                return;
            TMP_FontAsset main = GetOrCreate(k_MainFont, k_MainAsset, "Cinzel Decorative", label);
            if (main == null)
                return;

            defaultFont.objectReferenceValue = main;
            settingsSo.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();

            int changed = 0;
            foreach (TMP_Text text in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (text.font == main)
                    continue;
                Undo.RecordObject(text, "Apply Game Font");
                text.font = main;
                changed++;
            }
            foreach (WaveSpawner spawner in Object.FindObjectsByType<WaveSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var so = new SerializedObject(spawner);
                SerializedProperty font = so.FindProperty("m_LabelFont");
                if (font.objectReferenceValue == label)
                    continue;
                font.objectReferenceValue = label;
                so.ApplyModifiedProperties();
                changed++;
            }
            if (changed > 0)
                EditorSceneManager.MarkAllScenesDirty();
            Debug.Log($"Apply Game Font: {main.name} is the default now and {label.name} is on the wave label; {changed} objects in the open scenes changed.");
        }

        // Also used by the menu builder for the buttons' font.
        internal static TMP_FontAsset GetOrCreate(string fontPath, string assetPath, string name, TMP_FontAsset fallback)
        {
            var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (asset == null)
            {
                var font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
                if (font == null)
                {
                    Debug.LogError($"Apply Game Font: {fontPath} not found.");
                    return null;
                }
                asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
                asset.name = name + " SDF";
                AssetDatabase.CreateAsset(asset, assetPath);
                // The atlas and its material have to live inside the asset, or they are gone on the next reload.
                asset.atlasTextures[0].name = name + " Atlas";
                asset.material.name = name + " Material";
                AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
                AssetDatabase.AddObjectToAsset(asset.material, asset);

                if (fallback != null && fallback != asset)
                    asset.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };
            }
            asset.TryAddCharacters(k_Characters, out string missing);
            if (!string.IsNullOrEmpty(missing))
                Debug.LogWarning($"Apply Game Font: {name} has no glyph for \"{missing}\"; the fallback draws those.");
            EditorUtility.SetDirty(asset);
            return asset;
        }
    }
}
