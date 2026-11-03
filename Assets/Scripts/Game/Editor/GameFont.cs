using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace RuneArena.Game.Editor
{
    // Makes the TextMeshPro font asset from the game's font, sets it as TextMeshPro's default (what every text
    // made in code gets) and puts it on the texts already in the open scenes. The old default stays as its
    // fallback for any character the font doesn't have.
    static class GameFont
    {
        const string k_Font = "Assets/Fonts/metamorphous.regular.ttf";
        const string k_Asset = "Assets/Fonts/Metamorphous SDF.asset";
        // Baked into the atlas up front, so the headset doesn't render them mid-game. Anything else is added
        // the first time it shows up.
        const string k_Characters = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~¡¿áéíóúüñÁÉÍÓÚÜÑ×·…—";

        [MenuItem("RuneArena/Apply Game Font")]
        static void Apply()
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(k_Font);
            if (font == null)
            {
                Debug.LogError($"Apply Game Font: {k_Font} not found.");
                return;
            }

            var settings = Resources.Load<TMP_Settings>("TMP Settings");
            if (settings == null)
            {
                Debug.LogError("Apply Game Font: TMP Settings not found. Import the TextMeshPro essentials first.");
                return;
            }
            var settingsSo = new SerializedObject(settings);
            SerializedProperty defaultFont = settingsSo.FindProperty("m_defaultFontAsset");

            var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(k_Asset);
            if (asset == null)
            {
                asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
                asset.name = "Metamorphous SDF";
                AssetDatabase.CreateAsset(asset, k_Asset);
                // The atlas and its material have to live inside the asset, or they are gone on the next reload.
                asset.atlasTextures[0].name = "Metamorphous Atlas";
                asset.material.name = "Metamorphous Material";
                AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
                AssetDatabase.AddObjectToAsset(asset.material, asset);

                var previous = defaultFont.objectReferenceValue as TMP_FontAsset;
                if (previous != null)
                    asset.fallbackFontAssetTable = new List<TMP_FontAsset> { previous };
            }
            asset.TryAddCharacters(k_Characters, out string missing);
            if (!string.IsNullOrEmpty(missing))
                Debug.LogWarning($"Apply Game Font: the font has no glyph for \"{missing}\"; the fallback draws those.");
            EditorUtility.SetDirty(asset);

            defaultFont.objectReferenceValue = asset;
            settingsSo.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();

            int changed = 0;
            foreach (TMP_Text text in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (text.font == asset)
                    continue;
                Undo.RecordObject(text, "Apply Game Font");
                text.font = asset;
                changed++;
            }
            if (changed > 0)
                EditorSceneManager.MarkAllScenesDirty();
            Debug.Log($"Apply Game Font: {asset.name} is the default now; {changed} texts in the open scenes switched to it.");
        }
    }
}
