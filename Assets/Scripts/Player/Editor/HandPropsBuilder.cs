using System.Collections.Generic;
using System.IO;
using RuneArena.Arena.Editor;
using RuneArena.Strokes;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuneArena.Player.Editor
{
    // What the player holds instead of the Quest controllers: a wand in the right hand, whose tip is
    // where runes are drawn, and an open grimoire in the left, which the WristDisplay writes on, with a
    // bracer behind it for the WristBars.
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
        const int k_Parchment = 4;

        // Wand, measured from the end of the pommel along its axis.
        const float k_GripCenter = 0.085f;
        const float k_WandLength = 0.445f;

        // The open book: each page this wide and tall past a gap at the gutter, their top this high over the
        // bottom of the covers. The WristDisplay writes on them, so keep its k_Page values the same.
        const float k_PageWidth = 0.1f;
        const float k_PageHeight = 0.13f;
        const float k_PageTop = 0.016f;
        const float k_Gutter = 0.002f;
        const string k_FontPath = "Assets/Fonts/Cinzel SDF.asset";
        // Resting on the closed hand, leaned back toward the eyes. Easier to tune on the Grimoire object
        // in the scene, which keeps until the props are made again.
        static readonly Vector3 k_GrimoirePosition = new Vector3(0f, 0.03f, 0.02f);
        const float k_GrimoireTilt = -35f;
        // The book and the bracer, with the bars on it, are built life-size and shown at this scale. At 0.75
        // they read too small in the headset. The bracer scales toward the controller, so it stays behind the hand.
        const float k_PropScale = 1f;

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

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(k_FontPath);
            Transform bracer = Prop(wrist.transform, "Bracer", SaveMesh(Bracer(), "Bracer"), materials);
            bracer.localScale = Vector3.one * k_PropScale;
            if (!wrist.TryGetComponent(out WristBars bars))
                bars = Undo.AddComponent<WristBars>(wrist.gameObject);
            var barSettings = new SerializedObject(bars);
            barSettings.FindProperty("m_Font").objectReferenceValue = font;
            barSettings.FindProperty("m_Bracer").objectReferenceValue = bracer;
            // On the leather between the rims, the number included. The scene keeps its own values otherwise.
            barSettings.FindProperty("m_Along").floatValue = -0.137f;
            barSettings.FindProperty("m_Length").floatValue = 0.09f;
            barSettings.ApplyModifiedProperties();

            Material parchment = ArenaMaterials.Parchment();
            var bookMaterials = new[] { wood, bronze, leather, glow, parchment };
            Transform grimoire = Prop(wrist.transform, "Grimoire", SaveMesh(Grimoire(), "Grimoire"), bookMaterials);
            grimoire.localPosition = k_GrimoirePosition;
            grimoire.localRotation = Quaternion.Euler(k_GrimoireTilt, 0f, 0f);
            grimoire.localScale = Vector3.one * k_PropScale;

            var wristSettings = new SerializedObject(wrist);
            wristSettings.FindProperty("m_Grimoire").objectReferenceValue = grimoire;
            wristSettings.FindProperty("m_Parchment").objectReferenceValue = parchment;
            wristSettings.FindProperty("m_Font").objectReferenceValue = font;
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

        // An open book lying flat, the spine along Z under the gutter: leather covers a little larger than
        // the pages, a rounded-off spine with a bronze band down it, a block of paper on each side and bronze
        // caps on the outer corners.
        static Mesh Grimoire()
        {
            var m = new MeshBuilder(5);
            const float cover = 0.004f;
            const float lip = 0.006f;
            const float cap = 0.016f;
            float w = k_Gutter + k_PageWidth;
            float h = k_PageHeight * 0.5f;

            Box(m, k_Leather, new Vector3(-w - lip, 0f, -h - lip), new Vector3(-k_Gutter * 0.5f, cover, h + lip));
            Box(m, k_Leather, new Vector3(k_Gutter * 0.5f, 0f, -h - lip), new Vector3(w + lip, cover, h + lip));
            Box(m, k_Leather, new Vector3(-0.008f, -0.005f, -h - lip), new Vector3(0.008f, cover * 0.5f, h + lip));
            Box(m, k_Bronze, new Vector3(-0.003f, cover * 0.5f, -h - lip - 0.001f), new Vector3(0.003f, cover * 0.5f + 0.002f, h + lip + 0.001f));

            Pages(m, -1f, cover, h);
            Pages(m, 1f, cover, h);

            foreach (float x in new[] { -1f, 1f })
            {
                foreach (float z in new[] { -1f, 1f })
                {
                    var outer = new Vector3(x * (w + lip + 0.001f), cover + 0.001f, z * (h + lip + 0.001f));
                    var inner = new Vector3(x * (w + lip - cap), -0.001f, z * (h + lip - cap));
                    Box(m, k_Bronze, Vector3.Min(outer, inner), Vector3.Max(outer, inner));
                }
            }
            return m.ToMesh();
        }

        // The paper on one side of the gutter. Flat where the WristDisplay writes, and dipping into the
        // gutter over the last bit of the page, like a book that doesn't quite lie flat. The top gets the whole
        // parchment texture, outer edge to gutter; the edges of the block only a plain bit of it.
        static void Pages(MeshBuilder m, float side, float bottom, float h)
        {
            const int steps = 6;
            const float curl = 0.012f;
            const float dip = 0.005f;
            var plain = new Vector2(0.5f, 0.5f);

            // d is the distance from the gutter.
            float Top(float d) => k_PageTop - dip * Mathf.Pow(1f - Mathf.Clamp01(d / curl), 2f);
            float X(float d) => side * (k_Gutter + d);
            float U(float d) => side > 0f ? d / k_PageWidth : 1f - d / k_PageWidth;

            var stops = new List<float>();
            for (int i = 0; i <= steps; i++)
                stops.Add(curl * i / steps);
            stops.Add(k_PageWidth);

            for (int i = 0; i + 1 < stops.Count; i++)
            {
                float d0 = stops[i], d1 = stops[i + 1];
                float x0 = X(d0), x1 = X(d1);
                float y0 = Top(d0), y1 = Top(d1);
                // Square to the slope, facing up.
                Vector3 up = new Vector3(-(y1 - y0) * side, d1 - d0, 0f).normalized;
                m.Quad(k_Parchment, new Vector3(x0, y0, -h), new Vector3(x1, y1, -h), new Vector3(x1, y1, h), new Vector3(x0, y0, h), up,
                       new Vector2(U(d0), 0f), new Vector2(U(d1), 0f), new Vector2(U(d1), 1f), new Vector2(U(d0), 1f));
                foreach (float z in new[] { -h, h })
                {
                    m.Quad(k_Parchment, new Vector3(x0, bottom, z), new Vector3(x1, bottom, z), new Vector3(x1, y1, z), new Vector3(x0, y0, z),
                           Vector3.forward * Mathf.Sign(z), plain, plain, plain, plain);
                }
            }

            float outer = X(k_PageWidth), gutter = X(0f);
            m.Quad(k_Parchment, new Vector3(outer, bottom, -h), new Vector3(outer, bottom, h), new Vector3(outer, k_PageTop, h), new Vector3(outer, k_PageTop, -h),
                   Vector3.right * side, plain, plain, plain, plain);
            m.Quad(k_Parchment, new Vector3(gutter, bottom, -h), new Vector3(gutter, bottom, h), new Vector3(gutter, Top(0f), h), new Vector3(gutter, Top(0f), -h),
                   Vector3.left * side, plain, plain, plain, plain);
        }

        // Leather band around the forearm, behind the hand and the grimoire, open underneath, with bronze
        // rims. The WristBars lie along its top, so keep their k_Bracer values the same.
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

        // The six faces of an axis-aligned box.
        static void Box(MeshBuilder m, int submesh, Vector3 min, Vector3 max)
        {
            Vector3 P(float x, float y, float z) => new Vector3(x == 0 ? min.x : max.x, y == 0 ? min.y : max.y, z == 0 ? min.z : max.z);
            var u0 = Vector2.zero;
            var u1 = Vector2.right;
            var u2 = Vector2.one;
            var u3 = Vector2.up;
            m.Quad(submesh, P(0, 1, 0), P(1, 1, 0), P(1, 1, 1), P(0, 1, 1), Vector3.up, u0, u1, u2, u3);
            m.Quad(submesh, P(0, 0, 0), P(1, 0, 0), P(1, 0, 1), P(0, 0, 1), Vector3.down, u0, u1, u2, u3);
            m.Quad(submesh, P(0, 0, 0), P(1, 0, 0), P(1, 1, 0), P(0, 1, 0), Vector3.back, u0, u1, u2, u3);
            m.Quad(submesh, P(0, 0, 1), P(1, 0, 1), P(1, 1, 1), P(0, 1, 1), Vector3.forward, u0, u1, u2, u3);
            m.Quad(submesh, P(0, 0, 0), P(0, 1, 0), P(0, 1, 1), P(0, 0, 1), Vector3.left, u0, u1, u2, u3);
            m.Quad(submesh, P(1, 0, 0), P(1, 1, 0), P(1, 1, 1), P(1, 0, 1), Vector3.right, u0, u1, u2, u3);
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
