using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace RuneArena.Arena.Editor
{
    [CustomEditor(typeof(ColosseumBuilder))]
    class ColosseumBuilderEditor : UnityEditor.Editor
    {
        const string k_GeneratedFolder = "Assets/Arena/Generated";
        const string k_MaterialsFolder = "Assets/Materials";
        const float k_GateDepth = 0.2f;
        const float k_OutsideFloorThickness = 0.5f;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            if (GUILayout.Button("Build", GUILayout.Height(28)))
                Build((ColosseumBuilder)target);
        }

        static void Build(ColosseumBuilder builder)
        {
            Transform root = builder.transform;
            for (int i = root.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(root.GetChild(i).gameObject);

            if (builder.WallMaterial == null)
                builder.WallMaterial = FindOrCreateMaterial("Arena Wall", new Color(0.62f, 0.58f, 0.52f));
            if (builder.GateMaterial == null)
                builder.GateMaterial = FindOrCreateMaterial("Arena Gate", new Color(0.25f, 0.25f, 0.27f));
            EditorUtility.SetDirty(builder);

            int wallLayer = Mathf.Max(LayerMask.NameToLayer("Wall"), 0);
            int floorLayer = Mathf.Max(LayerMask.NameToLayer("Floor"), 0);

            var wall = new GameObject("Wall");
            Undo.RegisterCreatedObjectUndo(wall, "Build Colosseum");
            wall.transform.SetParent(root, false);
            wall.layer = wallLayer;
            wall.isStatic = true;

            Mesh mesh = SaveMesh(ColosseumMesh.Build(builder), $"{k_GeneratedFolder}/{root.gameObject.scene.name} {root.name} Wall.asset");
            wall.AddComponent<MeshFilter>().sharedMesh = mesh;
            wall.AddComponent<MeshRenderer>().sharedMaterial = builder.WallMaterial;
            wall.AddComponent<MeshCollider>().sharedMesh = mesh;

            var doors = new GameObject("Doors");
            Undo.RegisterCreatedObjectUndo(doors, "Build Colosseum");
            doors.transform.SetParent(root, false);

            float middle = builder.Radius - builder.Thickness * 0.5f;
            float outsideCenter = builder.Thickness * 0.5f + builder.OutsideDepth * 0.5f;

            for (int i = 0; i < builder.DoorCount; i++)
            {
                float angle = (builder.DoorAngle + i * 360f / builder.DoorCount) * Mathf.Deg2Rad;
                var outward = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));

                var door = new GameObject($"Door {i}");
                door.transform.SetParent(doors.transform, false);
                door.transform.localPosition = outward * middle;
                door.transform.localRotation = Quaternion.LookRotation(-outward);

                GameObject gate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                gate.name = "Gate";
                gate.layer = wallLayer;
                gate.transform.SetParent(door.transform, false);
                gate.transform.localPosition = new Vector3(0f, builder.DoorHeight * 0.5f, 0f);
                gate.transform.localScale = new Vector3(builder.DoorWidth, builder.DoorHeight, k_GateDepth);
                gate.GetComponent<MeshRenderer>().sharedMaterial = builder.GateMaterial;

                // Left out of the NavMesh bake; it blocks paths through its obstacle only while closed.
                gate.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
                var obstacle = gate.AddComponent<NavMeshObstacle>();
                obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.size = Vector3.one;
                obstacle.carving = true;
                obstacle.carveOnlyStationary = false;

                if (builder.OutsideDepth > 0f)
                {
                    GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    floor.name = "Outside Floor";
                    floor.layer = floorLayer;
                    floor.isStatic = true;
                    floor.transform.SetParent(door.transform, false);
                    floor.transform.localPosition = new Vector3(0f, -k_OutsideFloorThickness * 0.5f, -outsideCenter);
                    floor.transform.localScale = new Vector3(builder.DoorWidth + 1f, k_OutsideFloorThickness, builder.OutsideDepth);
                    floor.GetComponent<MeshRenderer>().sharedMaterial = builder.WallMaterial;
                }

                var spawn = new GameObject("SpawnPoint");
                spawn.transform.SetParent(door.transform, false);
                spawn.transform.localPosition = new Vector3(0f, 0f, -outsideCenter);

                door.AddComponent<ArenaGate>().Setup(gate.transform, spawn.transform);
            }

            BuildPillars(builder, root, wallLayer);

            EditorUtility.SetDirty(root.gameObject);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        }

        // Round pillars with a wider base and cap. They give cover: in the Wall layer, they stop enemy shots
        // and spells, and the NavMesh goes around them.
        static void BuildPillars(ColosseumBuilder builder, Transform root, int layer)
        {
            if (builder.PillarCount == 0)
                return;

            var pillars = new GameObject("Pillars");
            Undo.RegisterCreatedObjectUndo(pillars, "Build Colosseum");
            pillars.transform.SetParent(root, false);

            float w = builder.PillarWidth;
            float h = builder.PillarHeight;
            float trim = Mathf.Min(0.3f, h * 0.15f);

            for (int i = 0; i < builder.PillarCount; i++)
            {
                float angle = (builder.PillarAngle + i * 360f / builder.PillarCount) * Mathf.Deg2Rad;
                var outward = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));

                var pillar = new GameObject($"Pillar {i}");
                pillar.transform.SetParent(pillars.transform, false);
                pillar.transform.localPosition = outward * builder.PillarDistance;
                pillar.transform.localRotation = Quaternion.LookRotation(-outward);

                PillarPart(pillar.transform, "Base", new Vector3(w * 1.3f, trim, w * 1.3f), trim * 0.5f, layer, builder.WallMaterial);
                PillarPart(pillar.transform, "Shaft", new Vector3(w, h - 2f * trim, w), h * 0.5f, layer, builder.WallMaterial);
                PillarPart(pillar.transform, "Cap", new Vector3(w * 1.3f, trim, w * 1.3f), h - trim * 0.5f, layer, builder.WallMaterial);
            }
        }

        // size is diameter, height, diameter.
        static void PillarPart(Transform parent, string name, Vector3 size, float y, int layer, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            part.name = name;
            part.layer = layer;
            part.isStatic = true;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = new Vector3(0f, y, 0f);
            // The built-in cylinder is 2 units tall.
            part.transform.localScale = new Vector3(size.x, size.y * 0.5f, size.z);
            part.GetComponent<MeshRenderer>().sharedMaterial = material;

            // Its default capsule collider rounds off the flat top and bottom.
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.AddComponent<MeshCollider>().convex = true;
        }

        static Mesh SaveMesh(Mesh built, string path)
        {
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                built.name = Path.GetFileNameWithoutExtension(path);
                AssetDatabase.CreateAsset(built, path);
                return built;
            }

            // Overwrite in place so existing references keep working.
            mesh.Clear();
            mesh.SetVertices(built.vertices);
            mesh.SetNormals(built.normals);
            mesh.SetUVs(0, built.uv);
            mesh.SetTriangles(built.triangles, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            AssetDatabase.SaveAssets();
            Object.DestroyImmediate(built);
            return mesh;
        }

        static Material FindOrCreateMaterial(string name, Color color)
        {
            string path = $"{k_MaterialsFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            EnsureFolder(k_MaterialsFolder);
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }

    // Ring wall with arched openings cut through it. The wall is split into one section per door
    // (or a single section without doors), and each section into columns along the wall.
    static class ColosseumMesh
    {
        readonly struct Column
        {
            public readonly float U;
            public readonly float Bottom;

            public Column(float u, float bottom)
            {
                U = u;
                Bottom = bottom;
            }
        }

        static readonly List<Vector3> s_Vertices = new List<Vector3>();
        static readonly List<Vector3> s_Normals = new List<Vector3>();
        static readonly List<Vector2> s_Uvs = new List<Vector2>();
        static readonly List<int> s_Triangles = new List<int>();

        public static Mesh Build(ColosseumBuilder b)
        {
            s_Vertices.Clear();
            s_Normals.Clear();
            s_Uvs.Clear();
            s_Triangles.Clear();

            float outer = b.Radius;
            float inner = b.Radius - b.Thickness;
            float middle = (outer + inner) * 0.5f;
            int sections = Mathf.Max(b.DoorCount, 1);
            float sectionLength = 2f * Mathf.PI * middle / sections;
            float maxColumn = 2f * Mathf.PI * middle / b.Segments;
            float start = b.DoorAngle * Mathf.Deg2Rad * middle;

            for (int s = 0; s < sections; s++)
            {
                float center = start + s * sectionLength;
                List<Column> columns = SectionColumns(b, sectionLength, maxColumn);

                for (int c = 0; c < columns.Count - 1; c++)
                {
                    Column c0 = columns[c];
                    Column c1 = columns[c + 1];
                    if (Mathf.Approximately(c0.U, c1.U))
                        continue;

                    float u0 = center + c0.U;
                    float u1 = center + c1.U;
                    Face(inner, u0, c0.Bottom, u1, c1.Bottom, b.Height, middle, -1f);
                    Face(outer, u0, c0.Bottom, u1, c1.Bottom, b.Height, middle, 1f);
                    Top(inner, outer, u0, u1, b.Height, middle);
                    if (c0.Bottom > 0f || c1.Bottom > 0f)
                        Intrados(inner, outer, center, c0, c1, b, middle);
                }

                if (b.DoorCount > 0)
                {
                    float half = b.DoorWidth * 0.5f;
                    float spring = b.DoorHeight - half;
                    Jamb(inner, outer, center - half, spring, middle, 1f);
                    Jamb(inner, outer, center + half, spring, middle, -1f);
                }
            }

            var mesh = new Mesh();
            mesh.SetVertices(s_Vertices);
            mesh.SetNormals(s_Normals);
            mesh.SetUVs(0, s_Uvs);
            mesh.SetTriangles(s_Triangles, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        // Column boundaries across one section, centered on its door: plain wall, the arch, plain wall.
        static List<Column> SectionColumns(ColosseumBuilder b, float sectionLength, float maxColumn)
        {
            var columns = new List<Column>();
            float halfSection = sectionLength * 0.5f;

            if (b.DoorCount == 0)
            {
                int n = Mathf.CeilToInt(sectionLength / maxColumn);
                for (int i = 0; i <= n; i++)
                    columns.Add(new Column(-halfSection + sectionLength * i / n, 0f));
                return columns;
            }

            float half = b.DoorWidth * 0.5f;
            float spring = b.DoorHeight - half;
            float side = halfSection - half;
            int sideColumns = Mathf.Max(1, Mathf.CeilToInt(side / maxColumn));

            for (int i = 0; i <= sideColumns; i++)
                columns.Add(new Column(-halfSection + side * i / sideColumns, 0f));

            for (int k = 0; k <= b.ArchSegments; k++)
            {
                float a = Mathf.PI * k / b.ArchSegments;
                columns.Add(new Column(-half * Mathf.Cos(a), spring + half * Mathf.Sin(a)));
            }

            for (int i = 0; i <= sideColumns; i++)
                columns.Add(new Column(half + side * i / sideColumns, 0f));

            return columns;
        }

        static Vector3 Point(float radius, float u, float y, float middle)
        {
            float angle = u / middle;
            return new Vector3(Mathf.Sin(angle) * radius, y, Mathf.Cos(angle) * radius);
        }

        static Vector3 Radial(float u, float middle)
        {
            float angle = u / middle;
            return new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
        }

        static Vector3 Tangent(float u, float middle)
        {
            float angle = u / middle;
            return new Vector3(Mathf.Cos(angle), 0f, -Mathf.Sin(angle));
        }

        static void Face(float radius, float u0, float b0, float u1, float b1, float top, float middle, float side)
        {
            Vector3 n0 = Radial(u0, middle) * side;
            Vector3 n1 = Radial(u1, middle) * side;
            Quad(Point(radius, u0, b0, middle), Point(radius, u1, b1, middle),
                 Point(radius, u1, top, middle), Point(radius, u0, top, middle),
                 n0, n1, n1, n0,
                 new Vector2(u0, b0), new Vector2(u1, b1), new Vector2(u1, top), new Vector2(u0, top));
        }

        static void Top(float inner, float outer, float u0, float u1, float top, float middle)
        {
            Quad(Point(inner, u0, top, middle), Point(outer, u0, top, middle),
                 Point(outer, u1, top, middle), Point(inner, u1, top, middle),
                 Vector3.up, Vector3.up, Vector3.up, Vector3.up,
                 new Vector2(u0, inner), new Vector2(u0, outer), new Vector2(u1, outer), new Vector2(u1, inner));
        }

        // Underside of the arch. Normals point at the arch center.
        static void Intrados(float inner, float outer, float center, Column c0, Column c1, ColosseumBuilder b, float middle)
        {
            float spring = b.DoorHeight - b.DoorWidth * 0.5f;
            float u0 = center + c0.U;
            float u1 = center + c1.U;
            Vector3 n0 = (Tangent(u0, middle) * -c0.U + Vector3.up * (spring - c0.Bottom)).normalized;
            Vector3 n1 = (Tangent(u1, middle) * -c1.U + Vector3.up * (spring - c1.Bottom)).normalized;
            Quad(Point(inner, u0, c0.Bottom, middle), Point(outer, u0, c0.Bottom, middle),
                 Point(outer, u1, c1.Bottom, middle), Point(inner, u1, c1.Bottom, middle),
                 n0, n0, n1, n1,
                 new Vector2(u0, inner), new Vector2(u0, outer), new Vector2(u1, outer), new Vector2(u1, inner));
        }

        // Straight side of the opening, below the arch. side = +1 faces along +u (into the opening from the left).
        static void Jamb(float inner, float outer, float u, float spring, float middle, float side)
        {
            Vector3 n = Tangent(u, middle) * side;
            Quad(Point(inner, u, 0f, middle), Point(outer, u, 0f, middle),
                 Point(outer, u, spring, middle), Point(inner, u, spring, middle),
                 n, n, n, n,
                 new Vector2(inner, 0f), new Vector2(outer, 0f), new Vector2(outer, spring), new Vector2(inner, spring));
        }

        // Adds a quad and flips its winding if needed so the face points along its normals.
        static void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d,
                         Vector3 na, Vector3 nb, Vector3 nc, Vector3 nd,
                         Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            int i = s_Vertices.Count;
            s_Vertices.Add(a); s_Vertices.Add(b); s_Vertices.Add(c); s_Vertices.Add(d);
            s_Normals.Add(na); s_Normals.Add(nb); s_Normals.Add(nc); s_Normals.Add(nd);
            s_Uvs.Add(ua); s_Uvs.Add(ub); s_Uvs.Add(uc); s_Uvs.Add(ud);

            Vector3 face = Vector3.Cross(b - a, c - a) + Vector3.Cross(c - a, d - a);
            if (Vector3.Dot(face, na + nb + nc + nd) >= 0f)
            {
                s_Triangles.Add(i); s_Triangles.Add(i + 1); s_Triangles.Add(i + 2);
                s_Triangles.Add(i); s_Triangles.Add(i + 2); s_Triangles.Add(i + 3);
            }
            else
            {
                s_Triangles.Add(i); s_Triangles.Add(i + 2); s_Triangles.Add(i + 1);
                s_Triangles.Add(i); s_Triangles.Add(i + 3); s_Triangles.Add(i + 2);
            }
        }
    }
}
