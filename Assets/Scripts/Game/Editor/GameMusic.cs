using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RuneArena.Game.Editor
{
    // Puts the background music in the active scene: a "Music" object whose AudioSource plays the track in a loop
    // from the start. The track is streamed instead of decompressed into memory, since it is minutes long.
    static class GameMusic
    {
        const string k_Clip = "Assets/Music/Blackmoor Colossus Loop.wav";
        const string k_Object = "Music";
        const float k_Volume = 0.5f;

        [MenuItem("RuneArena/Set Up Music")]
        static void SetUp()
        {
            if (AssetImporter.GetAtPath(k_Clip) is AudioImporter importer)
            {
                AudioImporterSampleSettings sample = importer.defaultSampleSettings;
                if (sample.loadType != AudioClipLoadType.Streaming || sample.compressionFormat != AudioCompressionFormat.Vorbis)
                {
                    sample.loadType = AudioClipLoadType.Streaming;
                    sample.compressionFormat = AudioCompressionFormat.Vorbis;
                    sample.quality = 0.6f;
                    importer.defaultSampleSettings = sample;
                    importer.SaveAndReimport();
                }
            }

            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(k_Clip);
            if (clip == null)
            {
                Debug.LogError($"Set Up Music: {k_Clip} not found.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            GameObject go = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == k_Object)
                {
                    go = root;
                    break;
                }
            }
            if (go == null)
            {
                go = new GameObject(k_Object);
                SceneManager.MoveGameObjectToScene(go, scene);
                Undo.RegisterCreatedObjectUndo(go, "Set Up Music");
            }

            AudioSource source = go.GetComponent<AudioSource>();
            if (source == null)
                source = Undo.AddComponent<AudioSource>(go);
            Undo.RecordObject(source, "Set Up Music");
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = true;
            source.spatialBlend = 0f;
            source.volume = k_Volume;
            // Never cut off when many sounds play at once.
            source.priority = 0;

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"Set Up Music: {clip.name} loops in {scene.name}. Save the scene to keep it.");
        }
    }
}
