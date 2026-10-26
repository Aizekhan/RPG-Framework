using System.Collections.Generic;
using System.IO;
using RPGFramework.ActionRpg;
using RPGFramework.Cameras;
using RPGFramework.Items;
using RPGFramework.Quests;
using RPGFramework.Tools.ItemDatabaseManager;
using RPGFramework.Tools.LootTableDesigner;
using RPGFramework.Tools.QuestEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ReferenceGame.Editor
{
    /// <summary>
    /// Generates Goblin Valley: placeholder art (materials; Hero, Goblin and Brute prefabs), the content assets
    /// (characters, items, loot, quests, the Game Setup) and the scene layout. Prefabs are rebuilt every run;
    /// materials and content are created only when missing, so tuning done in the inspector survives. Swap the
    /// placeholder models for real ones by editing the prefabs (or dragging other prefabs into the characters) and
    /// stop rebuilding.
    /// Batch mode: <c>Unity -batchmode -quit -projectPath . -executeMethod ReferenceGame.Editor.GoblinValleyBuilder.Rebuild</c>
    /// </summary>
    public static class GoblinValleyBuilder
    {
        private const string Root = "Assets/ReferenceGame";
        private const string MaterialsFolder = Root + "/Art/Materials";
        private const string PrefabsFolder = Root + "/Prefabs";
        private const string ContentFolder = Root + "/Content";
        private const string ScenePath = Root + "/Scenes/GoblinValley.unity";

        public const string HeroPrefabPath = PrefabsFolder + "/Hero.prefab";
        public const string GoblinPrefabPath = PrefabsFolder + "/Goblin.prefab";
        public const string BrutePrefabPath = PrefabsFolder + "/GoblinBrute.prefab";
        public const string SetupPath = ContentFolder + "/GoblinValleySetup.asset";

        [MenuItem("RPG Framework/Reference Game/Rebuild Goblin Valley")]
        public static void Rebuild()
        {
            EnsureFolder(MaterialsFolder);
            EnsureFolder(PrefabsFolder);
            foreach (var folder in new[] { "Characters", "Items", "Loot", "Quests" })
                EnsureFolder($"{ContentFolder}/{folder}");

            var hero = BuildHero();
            var goblin = BuildGoblin("Goblin", GoblinPrefabPath, 1f, "Goblin", new Color(0.38f, 0.62f, 0.2f), "Rags", new Color(0.45f, 0.3f, 0.2f));
            var brute = BuildGoblin("GoblinBrute", BrutePrefabPath, 1.45f, "BruteSkin", new Color(0.3f, 0.48f, 0.18f), "BruteArmor", new Color(0.3f, 0.3f, 0.34f));
            LoadOrCreateContent(hero, goblin, brute);
            AssetDatabase.SaveAssets();

            BuildScene();
            AssetDatabase.SaveAssets();
            Debug.Log("Goblin Valley rebuilt");
        }

        private static GameObject BuildHero()
        {
            var root = new GameObject("Hero");
            var body = root.AddComponent<CharacterController>();
            body.center = new Vector3(0f, 1f, 0f);
            body.height = 2f;
            body.radius = 0.45f;

            var model = Child(root.transform, "Model", Vector3.zero);
            var torso = Part(model, PrimitiveType.Capsule, "Body", new Vector3(0f, 0.95f, 0f), new Vector3(0.9f, 0.95f, 0.9f),
                Material("Hero", new Color(0.2f, 0.42f, 0.85f)));
            var head = Part(model, PrimitiveType.Sphere, "Head", new Vector3(0f, 2.05f, 0f), Vector3.one * 0.55f,
                Material("Skin", new Color(0.96f, 0.8f, 0.65f)));
            Part(model, PrimitiveType.Cube, "Sword", new Vector3(0.55f, 1.05f, 0.55f), new Vector3(0.1f, 0.1f, 1.2f),
                Material("Steel", new Color(0.78f, 0.8f, 0.85f), 0.8f));
            Part(model, PrimitiveType.Cube, "Shield", new Vector3(-0.55f, 1.05f, 0.15f), new Vector3(0.1f, 0.75f, 0.6f),
                Material("Wood", new Color(0.55f, 0.36f, 0.18f)));

            Set(root.AddComponent<StrikeAnimator>(), "_model", model);
            AddHealthBar(root, 2.7f, torso, head);
            return Save(root, HeroPrefabPath);
        }

        private static GameObject BuildGoblin(string name, string path, float size, string skinMaterial, Color skinColor,
            string clothMaterial, Color clothColor)
        {
            var root = new GameObject(name);
            var model = Child(root.transform, "Model", Vector3.zero);
            model.localScale = Vector3.one * size;
            var skin = Material(skinMaterial, skinColor);
            var torso = Part(model, PrimitiveType.Capsule, "Body", new Vector3(0f, 0.62f, 0f), new Vector3(0.7f, 0.62f, 0.7f),
                Material(clothMaterial, clothColor));
            var head = Part(model, PrimitiveType.Sphere, "Head", new Vector3(0f, 1.42f, 0.05f), Vector3.one * 0.62f, skin);
            var leftEar = Part(model, PrimitiveType.Cube, "EarL", new Vector3(-0.38f, 1.5f, 0f), new Vector3(0.36f, 0.08f, 0.14f), skin);
            var rightEar = Part(model, PrimitiveType.Cube, "EarR", new Vector3(0.38f, 1.5f, 0f), new Vector3(0.36f, 0.08f, 0.14f), skin);
            Part(model, PrimitiveType.Sphere, "EyeL", new Vector3(-0.13f, 1.48f, 0.3f), Vector3.one * 0.1f,
                Material("Eyes", new Color(1f, 0.85f, 0.1f)));
            Part(model, PrimitiveType.Sphere, "EyeR", new Vector3(0.13f, 1.48f, 0.3f), Vector3.one * 0.1f,
                Material("Eyes", new Color(1f, 0.85f, 0.1f)));
            Part(model, PrimitiveType.Cylinder, "Club", new Vector3(0.45f, 0.75f, 0.4f), new Vector3(0.12f, 0.45f, 0.12f),
                Material("Wood", new Color(0.55f, 0.36f, 0.18f))).localRotation = Quaternion.Euler(60f, 0f, 0f);

            Set(root.AddComponent<StrikeAnimator>(), "_model", model);
            AddHealthBar(root, 2.1f * size, torso, head, leftEar, rightEar);
            return Save(root, path);
        }

        private static void AddHealthBar(GameObject root, float height, params Transform[] body)
        {
            var bar = Child(root.transform, "HealthBar", new Vector3(0f, height, 0f));
            Part(bar, PrimitiveType.Cube, "Back", Vector3.zero, new Vector3(1.2f, 0.16f, 0.02f),
                Material("BarBack", new Color(0.08f, 0.08f, 0.08f)), shadows: false);
            // White fill: the bar tints it with the player or enemy color
            var fill = Part(bar, PrimitiveType.Cube, "Fill", new Vector3(0f, 0f, -0.02f), new Vector3(1.16f, 0.12f, 0.02f),
                Material("BarFill", Color.white), shadows: false);

            var view = root.AddComponent<HealthBarView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("_bar").objectReferenceValue = bar;
            serialized.FindProperty("_fill").objectReferenceValue = fill;
            var renderers = serialized.FindProperty("_body");
            renderers.arraySize = body.Length;
            for (int i = 0; i < body.Length; i++)
                renderers.GetArrayElementAtIndex(i).objectReferenceValue = body[i].GetComponent<Renderer>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void LoadOrCreateContent(GameObject heroPrefab, GameObject goblinPrefab, GameObject brutePrefab)
        {
            var ear = LoadOrCreate($"{ContentFolder}/Items/GoblinEar.asset",
                () => Item("goblin_ear", "Goblin Ear", "Proof of a slain goblin", ItemType.Crafting, ItemRarity.Common, 99));
            var dagger = LoadOrCreate($"{ContentFolder}/Items/RustyDagger.asset",
                () => Item("rusty_dagger", "Rusty Dagger", "Better than nothing", ItemType.Equipment, ItemRarity.Common, 1));
            var tusk = LoadOrCreate($"{ContentFolder}/Items/BruteTusk.asset",
                () => Item("brute_tusk", "Brute Tusk", "Heavy, and still warm", ItemType.Crafting, ItemRarity.Uncommon, 20));
            var medal = LoadOrCreate($"{ContentFolder}/Items/ValleyMedal.asset",
                () => Item("valley_medal", "Valley Medal", "Awarded for clearing Goblin Valley", ItemType.Quest, ItemRarity.Rare, 1));

            var goblinLoot = LoadOrCreate($"{ContentFolder}/Loot/GoblinLoot.asset",
                () => Loot("goblin", "Goblin", ear, 0, 1, (dagger, 0.25f)));
            var bruteLoot = LoadOrCreate($"{ContentFolder}/Loot/BruteLoot.asset",
                () => Loot("goblin_brute", "Goblin Brute", tusk, 1, 1, (dagger, 0.5f)));

            var hero = LoadOrCreate($"{ContentFolder}/Characters/Hero.asset", () =>
            {
                var character = CharacterDefinition.Create("hero", "Hero", 100f, 12f, 5f);
                character.SetGrowth(20f, 3f, 2f);
                character.SetMovement(6f, 2.4f, 0.4f);
                return character;
            });
            var goblin = LoadOrCreate($"{ContentFolder}/Characters/Goblin.asset", () =>
            {
                var character = CharacterDefinition.Create("goblin", "Goblin", 30f, 6f, 2f, 40, loot: goblinLoot);
                character.SetMovement(3.2f, 1.6f, 1.2f);
                return character;
            });
            var brute = LoadOrCreate($"{ContentFolder}/Characters/GoblinBrute.asset", () =>
            {
                var character = CharacterDefinition.Create("goblin_brute", "Goblin Brute", 70f, 11f, 4f, 90, loot: bruteLoot);
                character.SetMovement(2.4f, 2f, 1.8f);
                return character;
            });
            Set(hero, "_prefab", heroPrefab);
            Set(goblin, "_prefab", goblinPrefab);
            Set(brute, "_prefab", brutePrefab);

            var clear = LoadOrCreate($"{ContentFolder}/Quests/ClearTheValley.asset", () => KillQuest("clear_the_valley",
                "Clear the Valley", "Goblins overran the valley. Slay five of them.", goblin.Id, 5,
                new QuestRewardEntry { type = RewardType.Experience, rewardId = "xp", amount = 150 },
                new QuestRewardEntry { type = RewardType.Item, rewardId = medal.itemId, amount = 1 }));
            var bruteHunt = LoadOrCreate($"{ContentFolder}/Quests/BruteForce.asset", () => KillQuest("brute_force",
                "Brute Force", "The brutes lead the raids. Bring two of them down.", brute.Id, 2,
                new QuestRewardEntry { type = RewardType.Experience, rewardId = "xp", amount = 250 }));

            LoadOrCreate(SetupPath, () =>
            {
                var setup = GameSetup.Create(hero, new[] { goblin, brute }, new WaveSettings
                {
                    FirstWaveDelay = 1f, DelayBetweenWaves = 3f, FirstWaveSize = 2, AddedPerWave = 1, MaxWaveSize = 8
                });
                setup.Items.AddRange(new[] { ear, dagger, tusk, medal });
                setup.StartingQuests.Add(clear);
                setup.StartingQuests.Add(bruteHunt);
                var serialized = new SerializedObject(setup);
                serialized.FindProperty("_cameraOffset").vector3Value = new Vector3(0f, 12f, -9f);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                return setup;
            });
        }

        private static ItemAsset Item(string id, string name, string description, ItemType type, ItemRarity rarity, int stack)
        {
            var item = ScriptableObject.CreateInstance<ItemAsset>();
            item.itemId = id;
            item.itemName = name;
            item.description = description;
            item.itemType = type;
            item.rarity = rarity;
            item.maxStackSize = stack;
            return item;
        }

        private static LootTableAsset Loot(string id, string name, ItemAsset guaranteed, int minDrops, int maxDrops,
            params (ItemAsset Item, float Chance)[] entries)
        {
            var loot = ScriptableObject.CreateInstance<LootTableAsset>();
            loot.tableId = id;
            loot.tableName = name;
            loot.minDrops = minDrops;
            loot.maxDrops = maxDrops;
            loot.guaranteedDrops = new List<string> { guaranteed.itemId };
            loot.entries = new List<LootEntryData>();
            foreach (var (item, chance) in entries)
                loot.entries.Add(new LootEntryData { itemId = item.itemId, weight = 100, dropChance = chance });
            return loot;
        }

        private static QuestAsset KillQuest(string id, string title, string description, string target, int count,
            params QuestRewardEntry[] rewards)
        {
            var quest = ScriptableObject.CreateInstance<QuestAsset>();
            quest.questId = id;
            quest.title = title;
            quest.description = description;
            quest.objectives = new[]
            {
                new QuestObjectiveEntry
                {
                    objectiveId = "slay", type = ObjectiveType.Kill, targetId = target, requiredCount = count,
                    description = description
                }
            };
            quest.rewards = rewards;
            return quest;
        }

        private static void BuildScene()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            // Opening a scene unloads unused assets; load the setup after it
            var setup = AssetDatabase.LoadAssetAtPath<GameSetup>(SetupPath);

            foreach (var old in scene.GetRootGameObjects())
            {
                if (old.name == "Environment")
                    Object.DestroyImmediate(old);
            }
            BuildEnvironment();

            var camera = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
            if (camera == null)
            {
                camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
                camera.tag = "MainCamera";
            }
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 50f;
            var offset = setup.CameraOffset;
            camera.transform.SetPositionAndRotation(offset, Quaternion.Euler(53f, 0f, 0f));
            var follow = camera.GetComponent<CameraFollow>();
            if (follow == null)
                follow = camera.gameObject.AddComponent<CameraFollow>();
            follow.Offset = offset;
            EditorUtility.SetDirty(follow);

            var sun = Object.FindFirstObjectByType<Light>();
            if (sun != null)
            {
                sun.type = LightType.Directional;
                sun.shadows = LightShadows.Soft;
                sun.intensity = 1.1f;
                sun.color = new Color(1f, 0.95f, 0.85f);
                sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.7f, 0.82f);
            RenderSettings.ambientEquatorColor = new Color(0.45f, 0.5f, 0.42f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.24f, 0.2f);

            var host = Object.FindFirstObjectByType<GoblinValleyHost>();
            if (host == null)
                host = new GameObject("GoblinValleyHost").AddComponent<GoblinValleyHost>();
            var serialized = new SerializedObject(host);
            serialized.FindProperty("_setup").objectReferenceValue = setup;
            serialized.FindProperty("_camera").objectReferenceValue = follow;
            serialized.FindProperty("_saveProgress").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void BuildEnvironment()
        {
            var environment = new GameObject("Environment").transform;
            var grass = Material("Grass", new Color(0.36f, 0.55f, 0.24f));
            var ground = Part(environment, PrimitiveType.Plane, "Ground", Vector3.zero, new Vector3(8f, 1f, 8f), grass,
                keepCollider: true);
            ground.gameObject.isStatic = true;

            // A ring of trees and rocks closes the valley; fixed seed so the layout is the same every rebuild
            var random = new System.Random(7);
            var bark = Material("Bark", new Color(0.4f, 0.27f, 0.15f));
            var leaves = Material("Leaves", new Color(0.18f, 0.4f, 0.16f));
            var stone = Material("Stone", new Color(0.5f, 0.5f, 0.52f));
            for (int i = 0; i < 48; i++)
            {
                float angle = i / 48f * Mathf.PI * 2f + (float)random.NextDouble() * 0.1f;
                float radius = 22f + (float)random.NextDouble() * 12f;
                var position = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                float size = 0.8f + (float)random.NextDouble() * 0.7f;

                var tree = Child(environment, $"Tree{i:00}", position);
                tree.gameObject.isStatic = true;
                Part(tree, PrimitiveType.Cylinder, "Trunk", new Vector3(0f, 1.2f * size, 0f), new Vector3(0.4f, 1.2f, 0.4f) * size, bark);
                Part(tree, PrimitiveType.Sphere, "Crown", new Vector3(0f, 3.2f * size, 0f), new Vector3(2.6f, 2.8f, 2.6f) * size, leaves);

                if (i % 3 == 0)
                {
                    var rock = Part(environment, PrimitiveType.Sphere, $"Rock{i:00}",
                        position * 0.8f + new Vector3(0f, 0.2f, 0f), new Vector3(1.6f, 0.9f, 1.3f) * size, stone);
                    rock.localRotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
                    rock.gameObject.isStatic = true;
                }
            }
        }

        private static T LoadOrCreate<T>(string path, System.Func<T> create) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
            asset = create();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static GameObject Save(GameObject root, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static Transform Child(Transform parent, string name, Vector3 localPosition)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            child.localPosition = localPosition;
            return child;
        }

        private static Transform Part(Transform parent, PrimitiveType shape, string name, Vector3 localPosition,
            Vector3 localScale, Material material, bool shadows = true, bool keepCollider = false)
        {
            var part = GameObject.CreatePrimitive(shape);
            part.name = name;
            if (!keepCollider)
                Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            var renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            if (!shadows)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            return part.transform;
        }

        private static Material Material(string name, Color color, float smoothness = 0.15f)
        {
            string path = $"{MaterialsFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            material = new Material(Shader.Find("Standard")) { name = name, color = color };
            material.SetFloat("_Glossiness", smoothness);
            if (smoothness > 0.5f)
                material.SetFloat("_Metallic", 0.7f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void Set(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
