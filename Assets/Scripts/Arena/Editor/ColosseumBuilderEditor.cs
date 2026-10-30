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
        const float k_GateDepth = 0.2f;
        const float k_OutsideFloorThickness = 0.5f;
        const int k_FloorSegments = 128;
        const int k_ScenerySlices = 8;

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

            ArenaMaterials.Setup(builder);
            EditorUtility.SetDirty(builder);

            int wallLayer = Mathf.Max(LayerMask.NameToLayer("Wall"), 0);
            int floorLayer = Mathf.Max(LayerMask.NameToLayer("Floor"), 0);
            string prefix = $"{k_GeneratedFolder}/{root.gameObject.scene.name} {root.name}";

            var wall = new GameObject("Wall");
            Undo.RegisterCreatedObjectUndo(wall, "Build Colosseum");
            wall.transform.SetParent(root, false);
            wall.layer = wallLayer;
            wall.isStatic = true;

            var walls = new MeshBuilder();
            RingWall.Build(walls, 0, new Arcade
            {
                Inner = builder.InnerRadius,
                Outer = builder.Radius,
                Bottom = 0f,
                Top = builder.Height,
                Arches = builder.DoorCount,
                ArchWidth = builder.DoorWidth,
                ArchHeight = builder.DoorHeight,
                StartAngle = builder.DoorAngle,
                ArchSegments = builder.ArchSegments,
                Segments = builder.Segments,
                Cap = true,
            });
            Mesh mesh = SaveMesh(walls.ToMesh(), $"{prefix} Wall.asset");
            wall.AddComponent<MeshFilter>().sharedMesh = mesh;
            wall.AddComponent<MeshRenderer>().sharedMaterial = builder.WallMaterial;
            wall.AddComponent<MeshCollider>().sharedMesh = mesh;

            BuildFloor(builder, root, floorLayer, prefix);

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
                    floor.GetComponent<MeshRenderer>().sharedMaterial = builder.PlinthMaterial;
                }

                var spawn = new GameObject("SpawnPoint");
                spawn.transform.SetParent(door.transform, false);
                spawn.transform.localPosition = new Vector3(0f, 0f, -outsideCenter);

                door.AddComponent<ArenaGate>().Setup(gate.transform, spawn.transform);
            }

            BuildPillars(builder, root, wallLayer, prefix);
            BuildScenery(builder, root, prefix);

            EditorUtility.SetDirty(root.gameObject);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        }

        // Disc under the whole arena, out to the outside of the wall. Its UVs go from 0 to 1 across it,
        // which is what the floor layout texture expects.
        static void BuildFloor(ColosseumBuilder builder, Transform root, int layer, string prefix)
        {
            float radius = builder.Radius;
            var m = new MeshBuilder();
            int rings = Mathf.CeilToInt(radius / 2f);
            for (int ring = 0; ring < rings; ring++)
            {
                float r0 = radius * ring / rings, r1 = radius * (ring + 1) / rings;
                for (int i = 0; i < k_FloorSegments; i++)
                {
                    float a0 = 2f * Mathf.PI * i / k_FloorSegments, a1 = 2f * Mathf.PI * (i + 1) / k_FloorSegments;
                    Vector3 p0 = FloorPoint(r0, a0), p1 = FloorPoint(r0, a1), p2 = FloorPoint(r1, a1), p3 = FloorPoint(r1, a0);
                    m.Quad(0, p0, p1, p2, p3, Vector3.up, FloorUv(p0, radius), FloorUv(p1, radius), FloorUv(p2, radius), FloorUv(p3, radius));
                }
            }

            var floor = new GameObject("Floor");
            Undo.RegisterCreatedObjectUndo(floor, "Build Colosseum");
            floor.transform.SetParent(root, false);
            floor.layer = layer;
            floor.isStatic = true;
            Mesh mesh = SaveMesh(m.ToMesh(), $"{prefix} Floor.asset");
            floor.AddComponent<MeshFilter>().sharedMesh = mesh;
            floor.AddComponent<MeshRenderer>().sharedMaterial = builder.FloorMaterial;
            floor.AddComponent<MeshCollider>().sharedMesh = mesh;

            ArenaMaterials.SetupFloor(builder, $"{prefix} Floor Layout.png");
        }

        static Vector3 FloorPoint(float r, float angle)
        {
            return new Vector3(Mathf.Sin(angle) * r, 0f, Mathf.Cos(angle) * r);
        }

        static Vector2 FloorUv(Vector3 p, float radius)
        {
            return new Vector2(p.x / (2f * radius) + 0.5f, p.z / (2f * radius) + 0.5f);
        }

        // Pillars give cover: in the Wall layer, they stop enemy shots and spells, and the NavMesh goes
        // around them. Each one has its own style; the collider is the convex hull of what's seen.
        static void BuildPillars(ColosseumBuilder builder, Transform root, int layer, string prefix)
        {
            if (builder.PillarCount == 0)
                return;

            var pillars = new GameObject("Pillars");
            Undo.RegisterCreatedObjectUndo(pillars, "Build Colosseum");
            pillars.transform.SetParent(root, false);

            // Older builds had one column mesh for all of them.
            AssetDatabase.DeleteAsset($"{prefix} Column.asset");

            var materials = new Material[PillarMesh.SubmeshCount];
            materials[PillarMesh.Stone] = builder.WallMaterial;
            materials[PillarMesh.Plinth] = builder.PlinthMaterial;
            materials[PillarMesh.Trim] = builder.TrimMaterial;
            materials[PillarMesh.Bronze] = builder.BronzeMaterial;
            materials[PillarMesh.Glow] = builder.GlowMaterial;

            ColosseumBuilder.PillarStyle[] styles = builder.PillarStyles;
            var meshes = new Dictionary<ColosseumBuilder.PillarStyle, Mesh>();
            for (int i = 0; i < builder.PillarCount; i++)
            {
                ColosseumBuilder.PillarStyle style = styles != null && styles.Length > 0
                    ? styles[i % styles.Length]
                    : ColosseumBuilder.PillarStyle.Column;
                if (!meshes.TryGetValue(style, out Mesh mesh))
                {
                    mesh = SaveMesh(PillarMesh.Build(style, builder.PillarWidth, builder.PillarHeight), $"{prefix} Pillar {style}.asset");
                    meshes[style] = mesh;
                }

                float angle = (builder.PillarAngle + i * 360f / builder.PillarCount) * Mathf.Deg2Rad;
                var outward = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));

                var pillar = new GameObject($"Pillar {i}");
                pillar.transform.SetParent(pillars.transform, false);
                pillar.transform.localPosition = outward * builder.PillarDistance;
                pillar.transform.localRotation = Quaternion.LookRotation(-outward);
                pillar.layer = layer;
                pillar.isStatic = true;
                pillar.AddComponent<MeshFilter>().sharedMesh = mesh;
                pillar.AddComponent<MeshRenderer>().sharedMaterials = materials;
                var collider = pillar.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
                collider.convex = true;
            }
        }

        // Everything that is only there to be looked at: no colliders, and kept out of the NavMesh.
        // In slices around the arena, each one a separate object.
        static void BuildScenery(ColosseumBuilder builder, Transform root, string prefix)
        {
            var scenery = new GameObject("Scenery");
            Undo.RegisterCreatedObjectUndo(scenery, "Build Colosseum");
            scenery.transform.SetParent(root, false);
            scenery.isStatic = true;
            scenery.AddComponent<NavMeshModifier>().ignoreFromBuild = true;

            var materials = new Material[ColosseumDecor.SubmeshCount];
            materials[ColosseumDecor.Stone] = builder.WallMaterial;
            materials[ColosseumDecor.Trim] = builder.TrimMaterial;
            materials[ColosseumDecor.Plinth] = builder.PlinthMaterial;
            materials[ColosseumDecor.Seats] = builder.SeatMaterial;
            materials[ColosseumDecor.Dark] = builder.DarkMaterial;
            materials[ColosseumDecor.Wood] = builder.WoodMaterial;
            materials[ColosseumDecor.Banner] = builder.BannerMaterial;
            materials[ColosseumDecor.Shade] = builder.TunnelMaterial;

            // Older builds kept it all in one mesh.
            AssetDatabase.DeleteAsset($"{prefix} Scenery.asset");

            List<MeshBuilder> slices = ColosseumDecor.Build(builder).SplitByAngle(k_ScenerySlices, builder.DoorAngle);
            for (int i = 0; i < slices.Count; i++)
            {
                var slice = new GameObject($"Slice {i}");
                slice.transform.SetParent(scenery.transform, false);
                slice.isStatic = true;
                slice.AddComponent<MeshFilter>().sharedMesh = SaveMesh(slices[i].ToMesh(), $"{prefix} Scenery {i}.asset");
                slice.AddComponent<MeshRenderer>().sharedMaterials = materials;
            }

            BuildBanners(builder, root, prefix);
        }

        // A child per rune, which RuneBanners shows once the player can cast that rune.
        static void BuildBanners(ColosseumBuilder builder, Transform root, string prefix)
        {
            var banners = new GameObject("Banners");
            Undo.RegisterCreatedObjectUndo(banners, "Build Colosseum");
            banners.transform.SetParent(root, false);
            banners.AddComponent<RuneBanners>();

            string[] labels = RuneBanners.Labels;
            MeshBuilder[] cloths = ColosseumDecor.BannerCloths(builder, labels.Length);
            for (int i = 0; i < labels.Length; i++)
            {
                var banner = new GameObject(RuneBanners.Prefix + labels[i]);
                banner.transform.SetParent(banners.transform, false);
                banner.AddComponent<MeshFilter>().sharedMesh = SaveMesh(cloths[i].ToMesh(), $"{prefix} Banner {labels[i]}.asset");
                var renderer = banner.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = ArenaMaterials.Banner(labels[i]);
            }
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
            mesh.indexFormat = built.indexFormat;
            mesh.SetVertices(built.vertices);
            mesh.SetNormals(built.normals);
            mesh.SetUVs(0, built.uv);
            mesh.subMeshCount = built.subMeshCount;
            for (int i = 0; i < built.subMeshCount; i++)
                mesh.SetTriangles(built.GetTriangles(i), i);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            AssetDatabase.SaveAssets();
            Object.DestroyImmediate(built);
            return mesh;
        }

        public static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
