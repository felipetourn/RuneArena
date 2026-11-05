using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace RuneArena.Enemies.Editor
{
    // Swaps the placeholder bodies of the melee and flying enemies for the models in Assets/Models/Enemies
    // (made from the Sketchfab downloads by tools/models), and makes the dragon boss from the flying one.
    // Makes their materials and animator controllers, and sets up the EnemyAnimator that plays them.
    // The rest of each prefab, tuning included, is kept.
    static class EnemyModels
    {
        const string k_ModelFolder = "Assets/Models/Enemies";
        const string k_PrefabFolder = "Assets/Prefabs/Enemies";
        const string k_MaterialFolder = "Assets/Materials/Enemies";

        // Of the dragon export: about 2.5 m wide for the flying enemy, 5.5 m for the boss. Gideon comes 2 m tall.
        const float k_DragonScale = 1.15f;
        const float k_BossScale = 2.5f;
        const float k_MageScale = 1.6f;
        // The ranged mage's height in meters. Its download comes tiny, so it is measured and scaled to this.
        const float k_RangedMageHeight = 1.85f;

        [MenuItem("RuneArena/Create Enemy Models")]
        static void Create()
        {
            Melee();
            Flying();
            BuildDragonBoss();
            Tillagemon();
            Demon();
            Mage();
            RangedMage();
            AssetDatabase.SaveAssets();
            Debug.Log("Enemy models set up in the melee, flying and dragon boss prefabs");
        }

        static void Melee()
        {
            // Emission on with no color, so the wind-up can light it up.
            Material material = Lit("Enemy Skeleton", "Skeleton", 0.25f, Color.black, true);
            string path = $"{k_PrefabFolder}/Melee Enemy.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            Animator animator = SwapBody(root, "Skeleton", material, "Idle");

            var enemy = new SerializedObject(root.GetComponent<Enemy>());
            enemy.FindProperty("m_FlashBrightness").floatValue = 2.5f;
            enemy.FindProperty("m_TelegraphGlow").floatValue = 0.6f;
            enemy.ApplyModifiedPropertiesWithoutUndo();

            // The sword comes down 0.38 s into the attack. The death is made from the big hit, up to where it lies.
            SetAnimator(root, animator, "Idle", "Walk", "Attack", "Hit", "Hit", "Death", 0.5f, 0.38f, 1.38f);
            SetDeath(root, 3f, 1.8f, 0.6f);
            // Bones and armor rattling, louder as it walks.
            var sound = new SerializedObject(root.GetComponent<EnemyAnimator>());
            sound.FindProperty("m_MoveLoop").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Sfx/Skeleton_Rattle.ogg");
            sound.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        static void Flying()
        {
            Material material = Lit("Enemy Dragon", "Dragon", 0.35f, Color.white * 1.5f, false);
            string path = $"{k_PrefabFolder}/Flying Enemy.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            Animator animator = SwapBody(root, "Dragon", material, "Fly", k_DragonScale);
            SetCollider(root, k_DragonScale);

            var enemy = new SerializedObject(root.GetComponent<Enemy>());
            enemy.FindProperty("m_FlashBrightness").floatValue = 2.5f;
            enemy.FindProperty("m_TelegraphGlow").floatValue = 1.5f;
            enemy.ApplyModifiedPropertiesWithoutUndo();

            // It only has one clip in the air, so it hovers with it, still or moving. The wind-up is the same
            // flying with the wings beating faster, and the dive has its own clip, made by tools/models.
            SetAnimator(root, animator, "Fly", "Fly", "Fly", "Hit", "Hit", "Death", 0f, 1.8f, 1.8f);
            var lunge = new SerializedObject(root.GetComponent<EnemyAnimator>());
            lunge.FindProperty("m_Lunge").stringValue = "Dive";
            lunge.ApplyModifiedPropertiesWithoutUndo();
            // Dead in the air, it falls lying down: its death clip from where it lies on the floor.
            SetDeath(root, 2.4f, 1.2f, 0.8f * k_DragonScale);
            var dead = new SerializedObject(root.GetComponent<EnemyAnimator>());
            dead.FindProperty("m_DeathStart").floatValue = 2.4f;
            dead.ApplyModifiedPropertiesWithoutUndo();
            // Hits measured from the middle of the body; its head and wings reach about this far.
            var flying = new SerializedObject(root.GetComponent<Enemy>());
            flying.FindProperty("m_HitRadius").floatValue = 0.9f;
            flying.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        // The flying enemy, much bigger, closing the even stages (see Create Default Waves). Made once from the flying prefab; after that
        // only its body and the values below are reset, so any other tuning done on it stays.
        static void BuildDragonBoss()
        {
            string source = $"{k_PrefabFolder}/Flying Enemy.prefab";
            string path = $"{k_PrefabFolder}/Dragon Boss.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                AssetDatabase.CopyAsset(source, path);

            // The flying enemy's script swapped for the boss's, which adds its own attacks: the tuning stays.
            SwapScript(path, "DragonBoss");
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            root.name = "Dragon Boss";
            Animator animator = SwapBody(root, "Dragon", AssetDatabase.LoadAssetAtPath<Material>($"{k_MaterialFolder}/Enemy Dragon.mat"), "Fly", k_BossScale);
            SetCollider(root, k_BossScale);



            var enemy = new SerializedObject(root.GetComponent<Enemy>());
            var projectile = AssetDatabase.LoadAssetAtPath<GameObject>($"{k_PrefabFolder}/Enemy Projectile.prefab");
            enemy.FindProperty("m_Projectile").objectReferenceValue = projectile != null ? projectile.GetComponent<EnemyProjectile>() : null;
            enemy.FindProperty("m_MaxHealth").floatValue = 450f;
            enemy.FindProperty("m_StunResistance").floatValue = 0.5f;
            enemy.FindProperty("m_KnockbackResistance").floatValue = 0.8f;
            enemy.FindProperty("m_BarScale").floatValue = 2.5f;
            enemy.FindProperty("m_AlwaysShowBar").boolValue = true;
            enemy.FindProperty("m_DisplayName").stringValue = "";
            // It appears in the arena, rears up and roars, then takes off.
            enemy.FindProperty("m_EntranceTime").floatValue = 3.9f;
            enemy.FindProperty("m_Boss").boolValue = true;

            // Slower and higher, with a wider circle. It pulls up from the dive further out, or its
            // head would go through the player's.
            enemy.FindProperty("m_Speed").floatValue = 2.5f;
            enemy.FindProperty("m_Altitude").floatValue = 5f;
            enemy.FindProperty("m_OrbitRadius").floatValue = 10f;
            enemy.FindProperty("m_EntryHeight").floatValue = 2f;
            enemy.FindProperty("m_Damage").floatValue = 30f;
            enemy.FindProperty("m_DiveSpeed").floatValue = 7f;
            enemy.FindProperty("m_HitRadius").floatValue = 2.2f;
            enemy.FindProperty("m_WindUp").floatValue = 1.5f;
            enemy.FindProperty("m_Cooldown").floatValue = 5f;
            enemy.FindProperty("m_PillarMargin").floatValue = 2.5f;
            enemy.ApplyModifiedPropertiesWithoutUndo();

            var animation = new SerializedObject(root.GetComponent<EnemyAnimator>());
            animation.FindProperty("m_Animator").objectReferenceValue = animator;
            animation.FindProperty("m_Controller").objectReferenceValue = animator.runtimeAnimatorController;
            animation.FindProperty("m_AttackImpact").floatValue = 2.2f;
            animation.FindProperty("m_AttackLength").floatValue = 2.2f;
            animation.FindProperty("m_DeathStart").floatValue = 2.4f;
            animation.FindProperty("m_Entrance").stringValue = "Entrance";
            animation.FindProperty("m_EmergeDepth").floatValue = 0f;
            animation.ApplyModifiedPropertiesWithoutUndo();
            SetDeath(root, 3f, 1.6f, 0.8f * k_BossScale);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        // Lies there after the death clip for a while, then sinks into the floor instead of shrinking.
        static void SetDeath(GameObject root, float deathTime, float sinkDelay, float sinkDepth)
        {
            var enemy = new SerializedObject(root.GetComponent<Enemy>());
            enemy.FindProperty("m_DeathTime").floatValue = deathTime;
            enemy.FindProperty("m_ShrinkTime").floatValue = 0f;
            enemy.ApplyModifiedPropertiesWithoutUndo();

            var animation = new SerializedObject(root.GetComponent<EnemyAnimator>());
            animation.FindProperty("m_SinkDelay").floatValue = sinkDelay;
            animation.FindProperty("m_SinkTime").floatValue = deathTime - sinkDelay - 0.1f;
            animation.FindProperty("m_SinkDepth").floatValue = sinkDepth;
            animation.ApplyModifiedPropertiesWithoutUndo();
        }

        // The stage 1 boss: the ground boss with Tillagemon's body. It rises out of the floor in front of the
        // player and brings its pitchfork down, which is also its slam.
        static void Tillagemon()
        {
            Material[] materials =
            {
                Lit("Enemy Tillagemon A", "Tillagemon/Tillagemon_A", 0.2f, Color.black, false),
                Lit("Enemy Tillagemon B", "Tillagemon/Tillagemon_B", 0.2f, Color.black, false),
            };
            string path = $"{k_PrefabFolder}/Boss Enemy.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            Animator animator = SwapBody(root, "Tillagemon", materials, "Idle");

            // About 3.6 m tall.
            var collider = root.GetComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, 1.6f, 0f);
            collider.height = 3.2f;
            collider.radius = 1.1f;
            var agent = root.GetComponent<NavMeshAgent>();
            agent.radius = 1.1f;
            agent.height = 3.2f;
            // What its walk clip covers, played at its own pace: any faster and the feet slide.
            agent.speed = 1f;
            // The shots come from the pitchfork's tips; the placeholder's sphere goes.
            Transform muzzle = root.transform.Find("Muzzle");
            if (muzzle != null)
            {
                Object.DestroyImmediate(muzzle.GetComponent<Renderer>());
                muzzle.localPosition = new Vector3(0f, 2.4f, 1.8f);
            }

            var enemy = new SerializedObject(root.GetComponent<Enemy>());
            enemy.FindProperty("m_DisplayName").stringValue = "";
            enemy.FindProperty("m_FlashBrightness").floatValue = 2.5f;
            enemy.FindProperty("m_TelegraphGlow").floatValue = 0.6f;
            // Slowly out of the middle of the floor, then the pitchfork comes down, slower than its slam.
            enemy.FindProperty("m_EntranceTime").floatValue = 7.6f;
            enemy.FindProperty("m_Projectile").objectReferenceValue = Pitchfork();
            enemy.ApplyModifiedPropertiesWithoutUndo();

            SetAnimator(root, animator, "Idle", "Walk", "Slam", "Hit", "Stun", "Death", 1f, 0.8f, 2.7f);
            var animation = new SerializedObject(root.GetComponent<EnemyAnimator>());
            animation.FindProperty("m_Run").stringValue = "Run";
            animation.FindProperty("m_RunAbove").floatValue = 4f;
            animation.FindProperty("m_RunSpeed").floatValue = 9f;
            SetAttacks(animation, ("Charge", "ChargeReady", 0.87f, 0.87f), ("Slam", "Slam", 0.8f, 2.7f),
                       ("Volley", "Volley", 0.6f, 1.67f), ("Summon", "Summon", 1.4f, 4.27f),
                       ("Strike1", "Strike1", 0.55f, 1.67f), ("Strike2", "Strike2", 0.47f, 1.67f), ("Strike3", "Strike3", 0.77f, 2f));
            animation.FindProperty("m_Entrance").stringValue = "Slam";
            animation.FindProperty("m_EmergeDepth").floatValue = 3.8f;
            animation.FindProperty("m_RiseTime").floatValue = 4f;
            animation.FindProperty("m_EntrancePace").floatValue = 0.75f;
            animation.ApplyModifiedPropertiesWithoutUndo();
            SetDeath(root, 4f, 2.8f, 1.4f);

            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        // The stage 3 boss: the reaper. Made once from the melee enemy, with its script swapped for the
        // boss's; after that only its body and the values below are reset.
        static void Demon()
        {
            string source = $"{k_PrefabFolder}/Melee Enemy.prefab";
            string path = $"{k_PrefabFolder}/Demon Boss.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                AssetDatabase.CopyAsset(source, path);

            Material[] materials =
            {
                Lit("Enemy Megademon Armor", "Megademon/Megademon_Armor", 0.3f, Color.black, false),
                Lit("Enemy Megademon Body", "Megademon/Megademon_Body", 0.2f, Color.black, false),
                Lit("Enemy Megademon Blade", "Megademon/Megademon_Blade", 0.6f, Color.black, false),
            };
            SwapScript(path, "DemonBoss");
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            root.name = "Demon Boss";
            Animator animator = SwapBody(root, "Megademon", materials, "Idle");

            var collider = root.GetComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, 1.4f, 0f);
            collider.height = 2.8f;
            collider.radius = 0.7f;
            var agent = root.GetComponent<NavMeshAgent>();
            agent.radius = 0.7f;
            agent.height = 2.8f;

            var enemy = new SerializedObject(root.GetComponent<Enemy>());
            enemy.FindProperty("m_MaxHealth").floatValue = 700f;
            enemy.FindProperty("m_StunResistance").floatValue = 0.6f;
            enemy.FindProperty("m_KnockbackResistance").floatValue = 0.9f;
            enemy.FindProperty("m_BarScale").floatValue = 2.5f;
            enemy.FindProperty("m_AlwaysShowBar").boolValue = true;
            enemy.FindProperty("m_DisplayName").stringValue = "";
            enemy.FindProperty("m_FlashBrightness").floatValue = 2.5f;
            enemy.FindProperty("m_TelegraphGlow").floatValue = 0.6f;
            // It comes up out of a heap of bones.
            enemy.FindProperty("m_EntranceTime").floatValue = 2.7f;
            var minion = AssetDatabase.LoadAssetAtPath<GameObject>(source);
            enemy.FindProperty("m_Minion").objectReferenceValue = minion != null ? minion.GetComponent<Enemy>() : null;
            enemy.ApplyModifiedPropertiesWithoutUndo();

            // It doesn't walk or flinch: it blinks, and the clips have neither.
            SetAnimator(root, animator, "Idle", "Idle", "Combo", "Idle", "Idle", "Collapse", 0f, 0.8f, 2.3f);
            var animation = new SerializedObject(root.GetComponent<EnemyAnimator>());
            animation.FindProperty("m_HitLength").floatValue = 0f;
            SetAttacks(animation, ("Combo", "Combo", 0.8f, 2.3f), ("Leap", "Leap", 2.15f, 3.3f),
                       ("Whirl", "Whirl", 0.7f, 1.6f), ("Cleave", "Cleave", 1.25f, 1.7f));
            animation.FindProperty("m_Entrance").stringValue = "Reform";
            animation.FindProperty("m_EmergeDepth").floatValue = 0f;
            animation.ApplyModifiedPropertiesWithoutUndo();
            SetDeath(root, 4.5f, 3f, 1.5f);

            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        // What the stage 1 boss throws in its volley: a pitchfork, flying tines first. Built from boxes and a
        // cylinder, like the placeholders; the projectile script is the plain one.
        static EnemyProjectile Pitchfork()
        {
            string path = $"{k_PrefabFolder}/Pitchfork Projectile.prefab";
            var source = AssetDatabase.LoadAssetAtPath<GameObject>($"{k_PrefabFolder}/Enemy Projectile.prefab");
            var root = new GameObject("Pitchfork Projectile");
            var projectile = root.AddComponent<EnemyProjectile>();
            if (source != null)
                EditorUtility.CopySerialized(source.GetComponent<EnemyProjectile>(), projectile);
            var so = new SerializedObject(projectile);
            so.FindProperty("m_HitRadius").floatValue = 0.35f;
            so.ApplyModifiedPropertiesWithoutUndo();

            string[] found = AssetDatabase.FindAssets("Arena Wood t:Material");
            Material wood = found.Length > 0 ? AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(found[0])) : null;
            Material iron = Plain("Enemy Pitchfork Iron", new Color(0.32f, 0.33f, 0.35f), 0.5f, 0.8f);

            Part(root, PrimitiveType.Cylinder, "Handle", wood, new Vector3(0f, 0f, -0.3f), new Vector3(0.05f, 0.7f, 0.05f), true);
            Part(root, PrimitiveType.Cube, "Crossbar", iron, new Vector3(0f, 0f, 0.42f), new Vector3(0.42f, 0.05f, 0.05f), false);
            for (int i = -1; i <= 1; i++)
                Part(root, PrimitiveType.Cube, "Tine", iron, new Vector3(i * 0.18f, 0f, 0.62f), new Vector3(0.035f, 0.035f, 0.4f), false);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<EnemyProjectile>();
        }

        static void Part(GameObject root, PrimitiveType type, string name, Material material, Vector3 position, Vector3 scale, bool alongZ)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.name = name;
            part.transform.SetParent(root.transform, false);
            part.transform.localPosition = position;
            // A cylinder stands along Y: laid down along the flight.
            if (alongZ)
                part.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            part.transform.localScale = scale;
            if (material != null)
                part.GetComponent<Renderer>().sharedMaterial = material;
        }

        static Material Plain(string name, Color color, float smoothness, float metallic)
        {
            EnsureFolder(k_MaterialFolder);
            string path = $"{k_MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(material);
            return material;
        }

        // The stage 2 boss: Gideon, the rift mage. Made once from the melee enemy, with its script swapped.
        static void Mage()
        {
            string source = $"{k_PrefabFolder}/Melee Enemy.prefab";
            string path = $"{k_PrefabFolder}/Mage Boss.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                AssetDatabase.CopyAsset(source, path);
            SwapScript(path, "MageBoss");

            Material material = Lit("Enemy Gideon", "Gideon", 0.25f, Color.white * 1.5f, true);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            root.name = "Mage Boss";
            // About 3.2 m tall, over the player like the other bosses.
            Animator animator = SwapBody(root, "Gideon", material, "Idle", k_MageScale);
            var collider = root.GetComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, 1.6f, 0f);
            collider.height = 3.2f;
            collider.radius = 0.6f;
            var agent = root.GetComponent<NavMeshAgent>();
            agent.radius = 0.6f;
            agent.height = 3.2f;
            agent.speed = 3.5f;

            var enemy = new SerializedObject(root.GetComponent<Enemy>());
            enemy.FindProperty("m_MaxHealth").floatValue = 550f;
            enemy.FindProperty("m_StunResistance").floatValue = 0.5f;
            enemy.FindProperty("m_KnockbackResistance").floatValue = 0.8f;
            enemy.FindProperty("m_BarScale").floatValue = 2f;
            enemy.FindProperty("m_AlwaysShowBar").boolValue = true;
            enemy.FindProperty("m_DisplayName").stringValue = "";
            enemy.FindProperty("m_FlashBrightness").floatValue = 2.5f;
            enemy.FindProperty("m_TelegraphGlow").floatValue = 1.5f;
            // It drops out of a portal in the middle of the arena.
            enemy.FindProperty("m_EntranceTime").floatValue = 3.4f;
            enemy.FindProperty("m_Bolt").objectReferenceValue = Shot("Rift Bolt", 0.25f, new Color(0.5f, 0.3f, 1f), 0.3f);
            enemy.FindProperty("m_Orb").objectReferenceValue = Shot("Rift Orb", 0.6f, new Color(0.6f, 0.35f, 1f), 0.6f);
            enemy.FindProperty("m_Hands").vector3Value = new Vector3(0f, 1.4f, 0.6f) * k_MageScale;
            enemy.ApplyModifiedPropertiesWithoutUndo();

            // Its run covers about 3.5 m/s at its own size, more at this one.
            SetAnimator(root, animator, "Idle", "Run", "Bolt1", "Hit", "Hit", "Death", 3.5f * k_MageScale, 0.35f, 1.2f);
            var animation = new SerializedObject(root.GetComponent<EnemyAnimator>());
            animation.FindProperty("m_MoveLoop").objectReferenceValue = null;
            SetAttacks(animation, ("Bolt1", "Bolt1", 0.35f, 1.2f), ("Bolt2", "Bolt2", 0.35f, 1.2f), ("Bolt3", "Bolt3", 0.35f, 1.2f),
                       ("Rift", "Rift", 0.6f, 1.84f), ("Orb", "Cast", 0.5f, 0.96f));
            animation.FindProperty("m_Entrance").stringValue = "Entrance";
            animation.FindProperty("m_EntrancePace").floatValue = 0.8f;
            animation.FindProperty("m_EmergeDepth").floatValue = 0f;
            animation.ApplyModifiedPropertiesWithoutUndo();
            SetDeath(root, 3.5f, 2.4f, 1f);

            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        [MenuItem("RuneArena/Create Ranged Mage Model")]
        static void CreateRangedMage()
        {
            RangedMage();
            AssetDatabase.SaveAssets();
            Debug.Log("Skeleton mage set up in the ranged enemy prefab");
        }

        // The ranged enemy: a skeleton mage from Mixamo, half of them throwing fire and half ice. Its body
        // comes with only the cast; the rest of its clips are Mixamo downloads in its Animations folder.
        static void RangedMage()
        {
            const string model = "SkeletonMage";
            string folder = $"{k_ModelFolder}/{model}";
            string modelPath = $"{folder}/{model}.fbx";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(modelPath) == null)
            {
                Debug.LogWarning($"No {modelPath}: the ranged enemy keeps its placeholder body.");
                return;
            }

            // Imported again so the humanoid settings below (EnemyModelImport) are on, the model first:
            // the clips are matched to its skeleton.
            string[] clipFiles = AssetDatabase.FindAssets("t:Model", new[] { $"{folder}/Animations" });
            for (int i = 0; i < clipFiles.Length; i++)
                clipFiles[i] = AssetDatabase.GUIDToAssetPath(clipFiles[i]);
            AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceUpdate);
            foreach (string clipFile in clipFiles)
                AssetDatabase.ImportAsset(clipFile, ImportAssetOptions.ForceUpdate);
            if (clipFiles.Length == 0)
                Debug.LogWarning($"No clips in {folder}/Animations: the ranged mage only has its cast.");

            // Emission on with no color, so the element and the wind-up can light it up.
            Material material = Lit("Enemy Skeleton Mage", model, 0.25f, Color.black, false);
            string path = $"{k_PrefabFolder}/Ranged Enemy.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            // Mixamo exports face +Z, like the prefab: no turn.
            Animator animator = SwapBody(root, model, new[] { material }, "Idle", 1f, 0f, clipFiles);
            Transform body = animator.transform;
            body.localScale = Vector3.one * (k_RangedMageHeight / Height(body));

            // The shot comes out from between its hands, where they meet at the end of the cast.
            Transform muzzle = root.transform.Find("Muzzle");
            if (muzzle != null)
            {
                Object.DestroyImmediate(muzzle.GetComponent<Renderer>());
                Object.DestroyImmediate(muzzle.GetComponent<MeshFilter>());
                muzzle.localPosition = new Vector3(0f, 0.64f, 0.45f) * k_RangedMageHeight;
            }

            var enemy = new SerializedObject(root.GetComponent<RangedEnemy>());
            enemy.FindProperty("m_FlashBrightness").floatValue = 2.5f;
            enemy.FindProperty("m_TelegraphGlow").floatValue = 0.6f;
            enemy.FindProperty("m_FireProjectile").objectReferenceValue = ElementShot("Mage Fire Bolt", ProjectileLook.Fire,
                new Color(1f, 0.45f, 0.1f), 0.35f, 0.3f, 1f, 0f);
            enemy.FindProperty("m_IceProjectile").objectReferenceValue = ElementShot("Mage Ice Shard", ProjectileLook.Ice,
                new Color(0.6f, 0.9f, 1f), 0.7f, 0.25f, 0f, 0.4f);
            enemy.ApplyModifiedPropertiesWithoutUndo();

            // The hands come together and push forward 1.9 s into the cast; the rest of it holds the pose.
            // A Mixamo walk covers about 1.4 m/s at this size.
            SetAnimator(root, animator, "Idle", "Walk", "Cast", "Hit", "Hit", "Death", 1.4f, 1.9f, 2.6f);
            var sound = new SerializedObject(root.GetComponent<EnemyAnimator>());
            sound.FindProperty("m_MoveLoop").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Sfx/Skeleton_Rattle.ogg");
            sound.ApplyModifiedPropertiesWithoutUndo();
            SetDeath(root, 3.5f, 2.6f, 0.5f);

            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        // From the feet to the top of the head, at the model's own scale: from its head bone if it has one.
        static float Height(Transform body)
        {
            foreach (Transform t in body.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.EndsWith("HeadTop_End"))
                {
                    float height = t.position.y - body.position.y;
                    if (height > 0f)
                        return height;
                }
            }
            return Mathf.Max(body.GetComponentInChildren<Renderer>().bounds.size.y, 1e-4f);
        }

        // A ranged mage's elemental shot: the plain enemy projectile, looking like the player's spell of that
        // element (EnemyProjectile dresses it at launch). Its own glowing ball is kept for when the effects
        // library is missing.
        static EnemyProjectile ElementShot(string name, ProjectileLook look, Color color, float size, float hitRadius,
                                           float explosionRadius, float slow)
        {
            EnemyProjectile shot = Shot(name, size, color, hitRadius);
            GameObject root = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(shot));
            var so = new SerializedObject(root.GetComponent<EnemyProjectile>());
            so.FindProperty("m_Look").enumValueIndex = (int)look;
            so.FindProperty("m_Color").colorValue = color;
            so.FindProperty("m_ExplosionRadius").floatValue = explosionRadius;
            so.FindProperty("m_Slow").floatValue = slow;
            so.FindProperty("m_SlowTime").floatValue = 2f;
            so.ApplyModifiedPropertiesWithoutUndo();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, AssetDatabase.GetAssetPath(shot));
            PrefabUtility.UnloadPrefabContents(root);
            return prefab.GetComponent<EnemyProjectile>();
        }

        // A glowing ball for a magic shot: the plain enemy projectile, with its own look and hit size.
        static EnemyProjectile Shot(string name, float size, Color color, float hitRadius)
        {
            string path = $"{k_PrefabFolder}/{name}.prefab";
            var source = AssetDatabase.LoadAssetAtPath<GameObject>($"{k_PrefabFolder}/Enemy Projectile.prefab");
            GameObject root = Object.Instantiate(source);
            root.name = name;
            root.transform.localScale = Vector3.one * size;
            Material glow = Plain($"Enemy {name}", color * 0.4f, 0.6f, 0f);
            glow.SetColor("_EmissionColor", color * 2.5f);
            glow.EnableKeyword("_EMISSION");
            root.GetComponent<Renderer>().sharedMaterial = glow;
            var so = new SerializedObject(root.GetComponent<EnemyProjectile>());
            so.FindProperty("m_HitRadius").floatValue = hitRadius;
            so.ApplyModifiedPropertiesWithoutUndo();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<EnemyProjectile>();
        }

        // Points the prefab's enemy component at another script, keeping every value both share. Saved
        // apart, so the prefab is loaded again with the new script's fields in it.
        static void SwapScript(string path, string script)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            Enemy current = root.GetComponent<Enemy>();
            if (current.GetType().Name != script)
            {
                string[] found = AssetDatabase.FindAssets($"{script} t:MonoScript");
                var asset = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(found[0]));
                var swap = new SerializedObject(current);
                swap.FindProperty("m_Script").objectReferenceValue = asset;
                swap.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            PrefabUtility.UnloadPrefabContents(root);
        }

        static void SetAttacks(SerializedObject animation, params (string name, string state, float impact, float length)[] attacks)
        {
            SerializedProperty list = animation.FindProperty("m_Attacks");
            list.arraySize = attacks.Length;
            for (int i = 0; i < attacks.Length; i++)
            {
                SerializedProperty item = list.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("Name").stringValue = attacks[i].name;
                item.FindPropertyRelative("State").stringValue = attacks[i].state;
                item.FindPropertyRelative("Impact").floatValue = attacks[i].impact;
                item.FindPropertyRelative("Length").floatValue = attacks[i].length;
            }
        }

        // Sized to the body: the export is about 2.2 m wide, with the body a bit above the pivot.
        static void SetCollider(GameObject root, float scale)
        {
            var collider = root.GetComponent<SphereCollider>();
            if (collider == null)
                return;
            collider.radius = 0.6f * scale;
            collider.center = new Vector3(0f, 0.4f * scale, 0f);
        }

        static Animator SwapBody(GameObject root, string model, Material material, string firstState, float scale = 1f)
        {
            return SwapBody(root, model, new[] { material }, firstState, scale);
        }

        static Animator SwapBody(GameObject root, string model, Material[] materials, string firstState, float scale = 1f,
                                 float turn = 180f, string[] clipFiles = null)
        {
            Transform old = root.transform.Find("Body");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);

            string modelPath = $"{k_ModelFolder}/{model}/{model}.fbx";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var body = (GameObject)PrefabUtility.InstantiatePrefab(asset, root.transform);
            body.name = "Body";
            // The Blender exports face -Z, so they are turned around by default.
            body.transform.localRotation = Quaternion.Euler(0f, turn, 0f);
            body.transform.localScale = Vector3.one * scale;
            foreach (Transform t in body.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = root.layer;

            var renderer = body.GetComponentInChildren<SkinnedMeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.On;

            var animator = body.GetComponent<Animator>();
            if (animator == null)
                animator = body.AddComponent<Animator>();
            animator.runtimeAnimatorController = Controller(model, modelPath, firstState, clipFiles);
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            var enemy = new SerializedObject(root.GetComponent<Enemy>());
            enemy.FindProperty("m_Body").objectReferenceValue = renderer;
            enemy.ApplyModifiedPropertiesWithoutUndo();
            return animator;
        }

        static void SetAnimator(GameObject root, Animator animator, string idle, string move, string attack, string hit,
                                string stunned, string death, float moveSpeed, float attackImpact, float attackLength)
        {
            var component = root.GetComponent<EnemyAnimator>();
            if (component == null)
                component = root.AddComponent<EnemyAnimator>();
            var so = new SerializedObject(component);
            so.FindProperty("m_Animator").objectReferenceValue = animator;
            so.FindProperty("m_Controller").objectReferenceValue = animator.runtimeAnimatorController;
            so.FindProperty("m_Idle").stringValue = idle;
            so.FindProperty("m_Move").stringValue = move;
            so.FindProperty("m_Attack").stringValue = attack;
            so.FindProperty("m_Hit").stringValue = hit;
            so.FindProperty("m_Stunned").stringValue = stunned;
            so.FindProperty("m_Death").stringValue = death;
            so.FindProperty("m_MoveSpeed").floatValue = moveSpeed;
            so.FindProperty("m_AttackImpact").floatValue = attackImpact;
            so.FindProperty("m_AttackLength").floatValue = attackLength;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // A state per clip of the model, named like the clip, with no transitions: EnemyAnimator picks them.
        // The asset is reused, emptied and refilled, so prefabs that already point at it (the flying enemy
        // and the dragon boss share one) keep working.
        // clipFiles are more models to take clips from, like animations downloaded on their own.
        static AnimatorController Controller(string model, string modelPath, string firstState, string[] clipFiles = null)
        {
            string path = $"{k_ModelFolder}/{model}/{model}.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            foreach (ChildAnimatorState child in machine.states)
                machine.RemoveState(child.state);
            var sources = new List<string> { modelPath };
            if (clipFiles != null)
                sources.AddRange(clipFiles);
            foreach (string source in sources)
            {
                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(source))
                {
                    if (!(asset is AnimationClip clip) || clip.name.StartsWith("__preview__"))
                        continue;
                    AnimatorState state = machine.AddState(clip.name);
                    state.motion = clip;
                    if (clip.name == firstState)
                        machine.defaultState = state;
                }
            }
            return controller;
        }

        // Two-sided, for the torn cloth and the wings. texture is the name before _Albedo, _Normal and _Emission.
        static Material Lit(string name, string texture, float smoothness, Color emission, bool cutout)
        {
            EnsureFolder(k_MaterialFolder);
            string path = $"{k_MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }

            // "Model" for a model with one set of textures, "Model/Set" for one with several.
            string folder = texture.Contains("/") ? $"{k_ModelFolder}/{texture}" : $"{k_ModelFolder}/{texture}/{texture}";
            material.color = Color.white;
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "_Albedo.png"));
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "_Normal.png"));
            material.EnableKeyword("_NORMALMAP");
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Cull", (float)CullMode.Off);

            var glow = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "_Emission.png");
            material.SetTexture("_EmissionMap", glow);
            material.SetColor("_EmissionColor", emission);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;

            if (cutout)
            {
                material.SetFloat("_AlphaClip", 1f);
                material.SetFloat("_Cutoff", 0.5f);
                material.EnableKeyword("_ALPHATEST_ON");
                material.renderQueue = (int)RenderQueue.AlphaTest;
                material.SetOverrideTag("RenderType", "TransparentCutout");
            }
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

    // Import settings for the enemy models and their textures.
    class EnemyModelImport : AssetPostprocessor
    {
        const string k_Folder = "Assets/Models/Enemies/";
        // Mixamo models: humanoid, so the clips downloaded on their own fit the body whatever its scale.
        const string k_HumanoidFolder = k_Folder + "SkeletonMage/";
        const string k_HumanoidModel = "SkeletonMage";

        // Clips that keep going until something else plays. The rest stop on their last pose.
        static readonly string[] k_Loops = { "Idle", "Walk", "Fly", "Run", "Stun" };

        bool IsModel => assetPath.StartsWith(k_Folder) && assetPath.EndsWith(".fbx");
        bool IsHumanoid => assetPath.StartsWith(k_HumanoidFolder);

        void OnPreprocessModel()
        {
            if (!IsModel)
                return;
            var importer = (ModelImporter)assetImporter;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.bakeAxisConversion = true;
            importer.animationType = IsHumanoid ? ModelImporterAnimationType.Human : ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;
        }

        // Blender names the clips "Armature|Clip". Mixamo calls every clip "mixamo.com", so those are named
        // after their file instead, and the one in the model itself is its cast.
        void OnPreprocessAnimation()
        {
            if (!IsModel)
                return;
            var importer = (ModelImporter)assetImporter;
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                if (IsHumanoid)
                {
                    string file = Path.GetFileNameWithoutExtension(assetPath);
                    clip.name = file == k_HumanoidModel ? "Cast" : file;
                    // Played where it stands, as the source moves it: the enemy's own code moves it around.
                    clip.lockRootRotation = true;
                    clip.lockRootHeightY = true;
                    clip.lockRootPositionXZ = true;
                    clip.keepOriginalOrientation = true;
                    clip.keepOriginalPositionY = true;
                    clip.keepOriginalPositionXZ = true;
                }
                else
                {
                    clip.name = clip.name.Substring(clip.name.LastIndexOf('|') + 1);
                }
                clip.loopTime = System.Array.IndexOf(k_Loops, clip.name) >= 0;
            }
            importer.clipAnimations = clips;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(k_Folder))
                return;
            var importer = (TextureImporter)assetImporter;
            if (!importer.importSettingsMissing)
                return;
            importer.maxTextureSize = 1024;
            importer.anisoLevel = 2;
            if (Path.GetFileNameWithoutExtension(assetPath).EndsWith("_Normal"))
                importer.textureType = TextureImporterType.NormalMap;
        }
    }
}
