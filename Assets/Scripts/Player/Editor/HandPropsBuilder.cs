using System.Collections.Generic;
using System.IO;
using RuneArena.Arena.Editor;
using RuneArena.Strokes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuneArena.Player.Editor
{
    // What the player holds instead of the Quest controllers: a wand in the right hand, whose tip is
    // where runes are drawn, and a bracer on the left wrist under the health and mana bars.
    // Positions are in the controller's space: +Z forward, +Y up.
    static class HandPropsBuilder
    {
        const string k_Folder = "Assets/Player/Generated";
        // Name of the controller model inside the XR Interaction Toolkit's controller prefabs.
        const string k_ControllerModel = "UniversalController";

        const int k_Wood = 0;
        const int k_Bronze = 1;
        const int k_Leather = 2;
        const int k_Glow = 3;

        // Wand, measured from the end of the pommel along its axis.
        const float k_GripCenter = 0.085f;
        const float k_WandLength = 0.445f;
        // How far forward of its mesh the bracer sits, so it reaches the hand. Keep the WristDisplay's
        // position the same amount forward, since the bars sit on it.
        const float k_BracerForward = 0.04f;

        [MenuItem("RuneArena/Create Hand Props")]
        static void Create()
        {
            var recorder = Object.FindAnyObjectByType<StrokeRecorder>(FindObjectsInactive.Include);
            var wrist = Object.FindAnyObjectByType<WristDisplay>(FindObjectsInactive.Include);
            if (recorder == null || wrist == null)
            {
                Debug.LogError("Create Hand Props: needs the StrokeRecorder (right controller) and the WristDisplay (left controller) in the open scene.");
                return;
            }

            ArenaMaterials.Props(out Material wood, out Material bronze, out Material leather, out Material glow);
            var materials = new[] { wood, bronze, leather, glow };

            Transform wand = Prop(recorder.transform, "Wand", SaveMesh(Wand(), "Wand"), materials);
            var tip = new GameObject("Tip").transform;
            Undo.RegisterCreatedObjectUndo(tip.gameObject, "Create Hand Props");
            tip.SetParent(wand, false);
            tip.localPosition = new Vector3(0f, 0f, k_WandLength - k_GripCenter);

            // Runes are drawn from the tip of the wand, and launched along it.
            var so = new SerializedObject(recorder);
            so.FindProperty("m_Tip").objectReferenceValue = tip;
            so.ApplyModifiedProperties();

            Transform bracer = Prop(wrist.transform, "Bracer", SaveMesh(Bracer(), "Bracer"), materials);
            bracer.localPosition = new Vector3(0f, 0f, k_BracerForward);
            var wristSettings = new SerializedObject(wrist);
            wristSettings.FindProperty("m_HideWhenPinned").objectReferenceValue = bracer.gameObject;
            wristSettings.ApplyModifiedProperties();

            HideController(recorder.transform);
            HideController(wrist.transform);
            EditorSceneManager.MarkSceneDirty(recorder.gameObject.scene);
        }

        static Transform Prop(Transform parent, string name, Mesh mesh, Material[] materials)
        {
            Transform old = parent.Find(name);
            if (old != null)
                Undo.DestroyObjectImmediate(old.gameObject);

            var prop = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(prop, "Create Hand Props");
            prop.transform.SetParent(parent, false);
            prop.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = prop.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            // Small and close to the eyes: its shadow on the floor would only flicker.
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return prop.transform;
        }

        static void HideController(Transform controller)
        {
            foreach (Transform t in controller.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != k_ControllerModel)
                    continue;
                Undo.RecordObject(t.gameObject, "Create Hand Props");
                t.gameObject.SetActive(false);
            }
        }

        // ---- Shapes ----

        // Turned along +Z, with the middle of the grip at the controller's origin.
        static Mesh Wand()
        {
            var m = new MeshBuilder(4);
            Quaternion along = Quaternion.Euler(90f, 0f, 0f);
            var offset = new Vector3(0f, 0f, -k_GripCenter);

            void Part(int submesh, List<Lathe.Step> steps, int sides) => Lathe.Revolve(m, submesh, steps, sides, along, offset);

            var pommel = new List<Lathe.Step>();
            Lathe.Arc(pommel, new Vector2(0f, 0.018f), 0.018f, -90f, 90f, 10);
            Part(k_Bronze, pommel, 16);

            // Grip wrapped in leather: a few slight ridges where the strap overlaps.
            var grip = new List<Lathe.Step>();
            for (int i = 0; i < 6; i++)
            {
                float y = 0.028f + i * 0.02f;
                Lathe.Straight(grip, new Vector2(0.0125f, y), new Vector2(0.0135f, y + 0.017f));
                Lathe.Straight(grip, new Vector2(0.0135f, y + 0.017f), new Vector2(0.0125f, y + 0.02f));
            }
            Part(k_Leather, grip, 16);

            var guard = new List<Lathe.Step>();
            Lathe.Arc(guard, new Vector2(0.0145f, 0.152f), 0.006f, -180f, 180f, 10);
            Part(k_Bronze, guard, 16);

            var shaft = new List<Lathe.Step>();
            Lathe.Straight(shaft, new Vector2(0.011f, 0.148f), new Vector2(0.006f, 0.398f));
            Part(k_Wood, shaft, 12);

            var collar = new List<Lathe.Step>();
            Lathe.Arc(collar, new Vector2(0.0065f, 0.398f), 0.0035f, -180f, 180f, 8);
            Part(k_Bronze, collar, 12);

            // A six-sided crystal on the end.
            var crystal = new List<Lathe.Step>();
            Lathe.Straight(crystal, new Vector2(0f, 0.396f), new Vector2(0.0095f, 0.414f));
            Lathe.Straight(crystal, new Vector2(0.0095f, 0.414f), new Vector2(0f, k_WandLength));
            Part(k_Glow, crystal, 6);

            return m.ToMesh();
        }

        // Leather band around the forearm, open underneath, with bronze rims. It sits just under the
        // WristDisplay's bars (0.035 up, 0.1 back) so they stay visible.
        static Mesh Bracer()
        {
            var m = new MeshBuilder(4);
            const float radius = 0.042f;
            const float thickness = 0.004f;
            const float height = -0.02f;
            const float front = -0.07f, back = -0.21f;
            const float rim = 0.012f;
            const float open = 135f;

            Shell(m, k_Leather, radius, thickness, height, back + rim, front - rim, open);
            Shell(m, k_Bronze, radius + 0.003f, thickness + 0.003f, height, front - rim, front, open);
            Shell(m, k_Bronze, radius + 0.003f, thickness + 0.003f, height, back, back + rim, open);

            // Three rivets along the top.
            for (int i = 0; i < 3; i++)
            {
                float z = Mathf.Lerp(back + 0.03f, front - 0.03f, i / 2f);
                var studs = new List<Lathe.Step>();
                Lathe.Arc(studs, Vector2.zero, 0.005f, 0f, 90f, 4);
                Lathe.Revolve(m, k_Bronze, studs, 8, Quaternion.identity, new Vector3(0f, height + radius, z));
            }
            return m.ToMesh();
        }

        // Part of a tube around the Z axis, from -open to +open degrees measured from the top,
        // between z0 and z1: outside, inside, the two long edges and the two ends.
        static void Shell(MeshBuilder m, int submesh, float radius, float thickness, float height, float z0, float z1, float open)
        {
            const int segments = 24;
            float inner = radius - thickness;
            var center = new Vector3(0f, height, 0f);

            Vector3 Dir(float a) => new Vector3(Mathf.Sin(a), Mathf.Cos(a), 0f);
            Vector3 P(float r, float a, float z) => center + Dir(a) * r + Vector3.forward * z;

            for (int i = 0; i < segments; i++)
            {
                float a0 = Mathf.Lerp(-open, open, (float)i / segments) * Mathf.Deg2Rad;
                float a1 = Mathf.Lerp(-open, open, (float)(i + 1) / segments) * Mathf.Deg2Rad;
                Vector3 d0 = Dir(a0), d1 = Dir(a1);
                var u0 = new Vector2(a0 * radius, z0);
                var u1 = new Vector2(a1 * radius, z0);
                var u2 = new Vector2(a1 * radius, z1);
                var u3 = new Vector2(a0 * radius, z1);
                m.Quad(submesh, P(radius, a0, z0), P(radius, a1, z0), P(radius, a1, z1), P(radius, a0, z1), d0, d1, d1, d0, u0, u1, u2, u3);
                m.Quad(submesh, P(inner, a0, z0), P(inner, a1, z0), P(inner, a1, z1), P(inner, a0, z1), -d0, -d1, -d1, -d0, u0, u1, u2, u3);
                m.Quad(submesh, P(inner, a0, z0), P(inner, a1, z0), P(radius, a1, z0), P(radius, a0, z0), Vector3.back, u0, u1, u2, u3);
                m.Quad(submesh, P(inner, a0, z1), P(inner, a1, z1), P(radius, a1, z1), P(radius, a0, z1), Vector3.forward, u0, u1, u2, u3);
            }

            foreach (float edge in new[] { -open, open })
            {
                float a = edge * Mathf.Deg2Rad;
                Vector3 side = Vector3.Cross(Vector3.forward, Dir(a)) * -Mathf.Sign(edge);
                m.Quad(submesh, P(inner, a, z0), P(radius, a, z0), P(radius, a, z1), P(inner, a, z1), side,
                       Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
            }
        }

        static Mesh SaveMesh(Mesh built, string name)
        {
            ColosseumBuilderEditor.EnsureFolder(k_Folder);
            string path = $"{k_Folder}/{name}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null)
                AssetDatabase.DeleteAsset(path);
            built.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(built, path);
            return built;
        }
    }
}
