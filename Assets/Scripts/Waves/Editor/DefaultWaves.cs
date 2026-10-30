using System.Collections.Generic;
using System.IO;
using RuneArena.Enemies;
using UnityEditor;
using UnityEngine;

namespace RuneArena.Waves.Editor
{
    static class DefaultWaves
    {
        const string k_Path = "Assets/Waves/Default Waves.asset";
        const string k_Prefabs = "Assets/Prefabs/Enemies";

        [MenuItem("RuneArena/Create Default Waves")]
        static void Create()
        {
            Enemy melee = Load("Melee Enemy");
            Enemy ranged = Load("Ranged Enemy");
            Enemy flying = Load("Flying Enemy");
            Enemy healer = Load("Healer Enemy");
            Enemy boss = Load("Boss Enemy");
            // Made by RuneArena > Create Enemy Models. Without it the ground boss closes every stage.
            Enemy dragon = Load("Dragon Boss");
            if (melee == null || ranged == null || flying == null || healer == null || boss == null)
            {
                Debug.LogError($"Missing enemy prefabs in {k_Prefabs}. Run RuneArena > Create Placeholder Enemies and Create Placeholder Healer and Boss first.");
                return;
            }

            var waves = new List<WaveSet.Wave>
            {
                Wave(2.5f, (melee, 3)),
                Wave(2.5f, (melee, 3), (ranged, 2)),
                Wave(2f, (melee, 3), (ranged, 2), (flying, 2), (healer, 1)),
                Wave(2f, (melee, 5), (ranged, 3), (flying, 2), (healer, 1)),
                Wave(3f, (boss, 1), (melee, 3), (ranged, 2), (healer, 1)),
            };

            // The two bosses take turns closing the stages: the ground one the odd stages, the dragon the even ones.
            if (dragon != null)
            {
                List<WaveSet.Group> last = waves[waves.Count - 1].groups;
                last[0].everyStages = 2;
                last.Insert(1, new WaveSet.Group { prefab = dragon, count = 1, fromStage = 2, everyStages = 2 });
            }

            var set = AssetDatabase.LoadAssetAtPath<WaveSet>(k_Path);
            if (set == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(k_Path));
                AssetDatabase.Refresh();
                set = ScriptableObject.CreateInstance<WaveSet>();
                AssetDatabase.CreateAsset(set, k_Path);
            }

            set.SetWaves(waves, 0.3f);
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            Selection.activeObject = set;
            Debug.Log($"Default waves written to {k_Path}");
        }

        static WaveSet.Wave Wave(float interval, params (Enemy prefab, int count)[] groups)
        {
            var wave = new WaveSet.Wave { spawnInterval = interval };
            foreach (var (prefab, count) in groups)
                wave.groups.Add(new WaveSet.Group { prefab = prefab, count = count });
            return wave;
        }

        static Enemy Load(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{k_Prefabs}/{name}.prefab");
            return prefab != null ? prefab.GetComponent<Enemy>() : null;
        }
    }
}
