using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuneArena.Arena.Editor
{
    [CustomEditor(typeof(ArenaLighting))]
    class ArenaLightingEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var lighting = (ArenaLighting)target;

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < lighting.Count; i++)
            {
                serializedObject.Update();
                string name = serializedObject.FindProperty("m_Presets").GetArrayElementAtIndex(i).FindPropertyRelative("Name").stringValue;
                if (GUILayout.Button(name, GUILayout.Height(24)))
                    Apply(lighting, i);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Prepare Scene turns off baked lighting and removes the old probes, so the presets " +
                                    "aren't mixed with light baked for another sky.", MessageType.None);
            if (GUILayout.Button("Prepare Scene"))
                PrepareScene(lighting);
        }

        static void Apply(ArenaLighting lighting, int index)
        {
            Light sun = RenderSettings.sun;
            Undo.RecordObject(lighting, "Apply Lighting Preset");
            if (sun != null)
            {
                Undo.RecordObject(sun, "Apply Lighting Preset");
                Undo.RecordObject(sun.transform, "Apply Lighting Preset");
            }
            lighting.Apply(index);
            EditorUtility.SetDirty(lighting);
            EditorSceneManager.MarkSceneDirty(lighting.gameObject.scene);
        }

        static void PrepareScene(ArenaLighting lighting)
        {
            if (Lightmapping.TryGetLightingSettings(out LightingSettings settings))
            {
                settings.bakedGI = false;
                settings.realtimeGI = false;
                EditorUtility.SetDirty(settings);
            }
            Lightmapping.Clear();
            Lightmapping.lightingDataAsset = null;

            foreach (ReflectionProbe probe in Object.FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Undo.DestroyObjectImmediate(probe.gameObject);
            foreach (LightProbeGroup group in Object.FindObjectsByType<LightProbeGroup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Undo.DestroyObjectImmediate(group.gameObject);

            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Undo.RecordObject(light, "Prepare Scene");
                light.lightmapBakeType = LightmapBakeType.Realtime;
            }

            Apply(lighting, lighting.Current);
        }
    }
}
