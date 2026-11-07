using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuneArena.Arena.Editor
{
    // Screenshots of the arena from where the player stands, for QA evidence: the stands all around, and a
    // close look at the nearest ones. It opens the arena scene, asking to save the open one first, and opens
    // it again at the end so the crowd built for the shots is never saved into it. Runs in batch mode too:
    //   Unity -batchmode -projectPath . -executeMethod RuneArena.Arena.Editor.ArenaSnapshot.Take -quit
    static class ArenaSnapshot
    {
        const string k_Scene = "Assets/Scenes/Arena.unity";
        const string k_Folder = "production/qa/evidence";

        [MenuItem("RuneArena/Take Arena Snapshots")]
        public static void Take()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            EditorSceneManager.OpenScene(k_Scene);
            // The crowd is built when the game starts; here it is built by hand, facing the camera's spot.
            var crowd = Object.FindAnyObjectByType<Spectators>();
            if (crowd != null)
                crowd.Build();
            Shader.SetGlobalVector("_CrowdViewer", new Vector3(0f, 1.7f, 0f));
            Shader.SetGlobalVector("_CrowdKill", new Vector4(0f, -100f, 0f, 0f));
            Shader.SetGlobalVector("_CrowdOla", new Vector4(-100f, 0f, 0f, 0f));
            Directory.CreateDirectory(k_Folder);
            string stamp = System.DateTime.Now.ToString("yyyyMMdd-HHmm");

            var go = new GameObject("Snapshot Camera");
            var camera = go.AddComponent<Camera>();
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 500f;
            var target = new RenderTexture(1920, 1080, 24);
            camera.targetTexture = target;

            // Eye height in the middle of the arena, looking at the stands in four directions; then zoomed in.
            for (int i = 0; i < 4; i++)
                Shoot(camera, target, new Vector3(0f, 1.7f, 0f), Quaternion.Euler(-12f, i * 90f, 0f), 75f, $"{k_Folder}/arena-stands-{i}-{stamp}.png");
            Shoot(camera, target, new Vector3(0f, 1.7f, 0f), Quaternion.Euler(-14f, 0f, 0f), 20f, $"{k_Folder}/arena-stands-zoom-{stamp}.png");

            camera.targetTexture = null;
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(go);
            // Back to the scene as saved, without the crowd built for the shots.
            EditorSceneManager.OpenScene(k_Scene);
            Debug.Log($"Arena snapshots saved to {k_Folder}");
        }

        static void Shoot(Camera camera, RenderTexture target, Vector3 position, Quaternion rotation, float fov, string path)
        {
            camera.transform.SetPositionAndRotation(position, rotation);
            camera.fieldOfView = fov;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.DestroyImmediate(image);
        }
    }
}
