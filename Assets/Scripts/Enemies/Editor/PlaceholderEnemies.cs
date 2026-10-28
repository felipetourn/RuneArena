using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace RuneArena.Enemies.Editor
{
    // Builds simple primitive prefabs for each enemy so the logic can be tested before there are real models.
    static class PlaceholderEnemies
    {
        const string k_PrefabFolder = "Assets/Prefabs/Enemies";
        const string k_MaterialFolder = "Assets/Materials/Enemies";

        [MenuItem("RuneArena/Create Placeholder Enemies")]
        static void Create()
        {
            EnsureFolder(k_PrefabFolder);
            EnsureFolder(k_MaterialFolder);

            int enemyLayer = Mathf.Max(LayerMask.NameToLayer("Enemy"), 0);
            int blockers = LayerMask.GetMask("Wall", "Floor");

            GameObject projectile = BuildProjectile(blockers);
            BuildMelee(enemyLayer);
            BuildRanged(enemyLayer, projectile.GetComponent<EnemyProjectile>(), LayerMask.GetMask("Wall"));
            BuildFlying(enemyLayer);

            AssetDatabase.SaveAssets();
            Debug.Log($"Placeholder enemies created in {k_PrefabFolder}");
        }

        static GameObject BuildProjectile(int blockMask)
        {
            GameObject go = Primitive(PrimitiveType.Sphere, "Enemy Projectile", GetMaterial("Enemy Projectile", new Color(1f, 0.5f, 0.1f)));
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.localScale = Vector3.one * 0.25f;

            var so = new SerializedObject(go.AddComponent<EnemyProjectile>());
            so.FindProperty("m_BlockMask").intValue = blockMask;
            so.ApplyModifiedPropertiesWithoutUndo();
            return Save(go);
        }

        static void BuildMelee(int layer)
        {
            GameObject root = GroundRoot("Melee Enemy", layer, 2f, out Renderer body,
                GetMaterial("Enemy Melee", new Color(0.85f, 0.2f, 0.2f)));
            SetBody(root.AddComponent<MeleeEnemy>(), body);
            Save(root);
        }

        static void BuildRanged(int layer, EnemyProjectile projectile, int sightMask)
        {
            Material material = GetMaterial("Enemy Ranged", new Color(0.2f, 0.45f, 0.9f));
            GameObject root = GroundRoot("Ranged Enemy", layer, 1.8f, out Renderer body, material);

            GameObject muzzle = Primitive(PrimitiveType.Cube, "Muzzle", material);
            Object.DestroyImmediate(muzzle.GetComponent<Collider>());
            muzzle.layer = layer;
            muzzle.transform.SetParent(root.transform, false);
            muzzle.transform.localPosition = new Vector3(0f, 1.4f, 0.45f);
            muzzle.transform.localScale = new Vector3(0.15f, 0.15f, 0.4f);

            var enemy = root.AddComponent<RangedEnemy>();
            var so = new SerializedObject(enemy);
            so.FindProperty("m_Body").objectReferenceValue = body;
            so.FindProperty("m_Projectile").objectReferenceValue = projectile;
            so.FindProperty("m_Muzzle").objectReferenceValue = muzzle.transform;
            so.FindProperty("m_SightMask").intValue = sightMask;
            so.ApplyModifiedPropertiesWithoutUndo();
            Save(root);
        }

        static void BuildFlying(int layer)
        {
            var root = new GameObject("Flying Enemy") { layer = layer };
            var collider = root.AddComponent<SphereCollider>();
            collider.radius = 0.45f;

            GameObject body = Primitive(PrimitiveType.Sphere, "Body", GetMaterial("Enemy Flying", new Color(0.6f, 0.25f, 0.85f)));
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.layer = layer;
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = Vector3.one * 0.8f;

            SetBody(root.AddComponent<FlyingEnemy>(), body.GetComponent<Renderer>());
            Save(root);
        }

        // Root at the feet with the agent and collider, and a capsule as the body.
        static GameObject GroundRoot(string name, int layer, float speed, out Renderer body, Material material)
        {
            var root = new GameObject(name) { layer = layer };

            var collider = root.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, 0.9f, 0f);
            collider.height = 1.8f;
            collider.radius = 0.4f;

            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.4f;
            agent.height = 1.8f;
            agent.speed = speed;
            agent.angularSpeed = 360f;
            agent.acceleration = 8f;
            agent.stoppingDistance = 0.3f;

            GameObject capsule = Primitive(PrimitiveType.Capsule, "Body", material);
            Object.DestroyImmediate(capsule.GetComponent<Collider>());
            capsule.layer = layer;
            capsule.transform.SetParent(root.transform, false);
            capsule.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            capsule.transform.localScale = new Vector3(0.8f, 0.9f, 0.8f);

            body = capsule.GetComponent<Renderer>();
            return root;
        }

        static void SetBody(Enemy enemy, Renderer body)
        {
            var so = new SerializedObject(enemy);
            so.FindProperty("m_Body").objectReferenceValue = body;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static GameObject Primitive(PrimitiveType type, string name, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        static GameObject Save(GameObject go)
        {
            string path = $"{k_PrefabFolder}/{go.name}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static Material GetMaterial(string name, Color color)
        {
            string path = $"{k_MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            EditorUtility.SetDirty(material);
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
}
