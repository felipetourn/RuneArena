using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
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

        // Of the dragon export: about 2.5 m wide for the flying enemy, 5.5 m for the boss.
        const float k_DragonScale = 1.15f;
        const float k_BossScale = 2.5f;

        [MenuItem("RuneArena/Create Enemy Models")]
        static void Create()
        {
            Melee();
            Flying();
            DragonBoss();
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
            // Its death clip takes a couple of seconds to end up lying down.
            SetDeath(root, 3.6f, 2.5f, 0.8f * k_DragonScale);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        // The flying enemy, much bigger, closing the even stages (see Create Default Waves). Made once from the flying prefab; after that
        // only its body and the values below are reset, so any other tuning done on it stays.
        static void DragonBoss()
        {
            string source = $"{k_PrefabFolder}/Flying Enemy.prefab";
            string path = $"{k_PrefabFolder}/Dragon Boss.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                AssetDatabase.CopyAsset(source, path);

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            root.name = "Dragon Boss";
            Animator animator = SwapBody(root, "Dragon", AssetDatabase.LoadAssetAtPath<Material>($"{k_MaterialFolder}/Enemy Dragon.mat"), "Fly", k_BossScale);
            SetCollider(root, k_BossScale);

            var enemy = new SerializedObject(root.GetComponent<Enemy>());
            enemy.FindProperty("m_MaxHealth").floatValue = 450f;
            enemy.FindProperty("m_StunResistance").floatValue = 0.5f;
            enemy.FindProperty("m_KnockbackResistance").floatValue = 0.8f;
            enemy.FindProperty("m_BarScale").floatValue = 2.5f;
            enemy.FindProperty("m_AlwaysShowBar").boolValue = true;
            enemy.FindProperty("m_DisplayName").stringValue = "Dragón";
            enemy.FindProperty("m_Boss").boolValue = true;

            // Slower and higher, with a wider circle. It pulls up from the dive further out, or its
            // head would go through the player's.
            enemy.FindProperty("m_Speed").floatValue = 2.5f;
            enemy.FindProperty("m_Altitude").floatValue = 5f;
            enemy.FindProperty("m_OrbitRadius").floatValue = 10f;
            enemy.FindProperty("m_EntryHeight").floatValue = 2f;
            enemy.FindProperty("m_Damage").floatValue = 30f;
            enemy.FindProperty("m_DiveSpeed").floatValue = 7f;
            enemy.FindProperty("m_HitRadius").floatValue = 2f;
            enemy.FindProperty("m_WindUp").floatValue = 1.5f;
            enemy.FindProperty("m_Cooldown").floatValue = 5f;
            enemy.FindProperty("m_PillarMargin").floatValue = 2.5f;
            enemy.ApplyModifiedPropertiesWithoutUndo();

            var animation = new SerializedObject(root.GetComponent<EnemyAnimator>());
            animation.FindProperty("m_Animator").objectReferenceValue = animator;
            animation.FindProperty("m_Controller").objectReferenceValue = animator.runtimeAnimatorController;
            animation.FindProperty("m_AttackImpact").floatValue = 2.2f;
            animation.FindProperty("m_AttackLength").floatValue = 2.2f;
            animation.ApplyModifiedPropertiesWithoutUndo();
            SetDeath(root, 4.5f, 3.2f, 0.8f * k_BossScale);
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
            Transform old = root.transform.Find("Body");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);

            string modelPath = $"{k_ModelFolder}/{model}/{model}.fbx";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var body = (GameObject)PrefabUtility.InstantiatePrefab(asset, root.transform);
            body.name = "Body";
            // The exports face -Z.
            body.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            body.transform.localScale = Vector3.one * scale;
            foreach (Transform t in body.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = root.layer;

            var renderer = body.GetComponentInChildren<SkinnedMeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;

            var animator = body.GetComponent<Animator>();
            if (animator == null)
                animator = body.AddComponent<Animator>();
            animator.runtimeAnimatorController = Controller(model, modelPath, firstState);
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
        static AnimatorController Controller(string model, string modelPath, string firstState)
        {
            string path = $"{k_ModelFolder}/{model}/{model}.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            foreach (ChildAnimatorState child in machine.states)
                machine.RemoveState(child.state);
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(modelPath))
            {
                if (!(asset is AnimationClip clip) || clip.name.StartsWith("__preview__"))
                    continue;
                AnimatorState state = machine.AddState(clip.name);
                state.motion = clip;
                if (clip.name == firstState)
                    machine.defaultState = state;
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

            string folder = $"{k_ModelFolder}/{texture}/{texture}";
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

        // Clips that keep going until something else plays. The rest stop on their last pose.
        static readonly string[] k_Loops = { "Idle", "Walk", "Fly" };

        bool IsModel => assetPath.StartsWith(k_Folder) && assetPath.EndsWith(".fbx");

        void OnPreprocessModel()
        {
            if (!IsModel)
                return;
            var importer = (ModelImporter)assetImporter;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.bakeAxisConversion = true;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;
        }

        // Blender names the clips "Armature|Clip".
        void OnPreprocessAnimation()
        {
            if (!IsModel)
                return;
            var importer = (ModelImporter)assetImporter;
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                clip.name = clip.name.Substring(clip.name.LastIndexOf('|') + 1);
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
