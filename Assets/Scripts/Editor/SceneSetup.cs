using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if MIRROR
using Mirror;
#endif

public static class SceneSetup
{
    static readonly Vector3 DefaultSpawnPos = new Vector3(0, 1.1f, -5f);

    [MenuItem("Tools/Fix NetworkManager PlayerPrefab")]
    static void FixNetworkManagerPlayerPrefab()
    {
#if MIRROR
        var nm = Object.FindAnyObjectByType<GameNetworkManager>();
        if (nm == null) { Debug.LogError("[FixNM] No GameNetworkManager found in scene."); return; }

        var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
        if (playerPrefab == null) { Debug.LogError("[FixNM] Player prefab not found at Assets/Prefabs/Player.prefab."); return; }

        var so = new SerializedObject(nm);
        so.FindProperty("playerPrefab").objectReferenceValue = playerPrefab;
        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(nm.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[FixNM] PlayerPrefab assigned and scene saved.");
#else
        Debug.LogWarning("[FixNM] Mirror not installed.");
#endif
    }

    [MenuItem("Tools/Setup All")]
    static void SetupAll()
    {
        SetupScene();
        FactionSetup.Run();
        ConversationSetup.Run();
        RegisterSpawnablePrefab("Assets/Prefabs/Enemy.prefab");
        RegisterSpawnablePrefab("Assets/Prefabs/PlayerCorpse.prefab");
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[SetupAll] Scene saved.");
    }

    [MenuItem("Tools/Setup Player Scene")]
    static void SetupPlayerScene() => SetupScene();

    // Adds any missing components to the existing Player prefab instance without touching tuned values.
    // After running: drag the Player from Hierarchy into Assets/Prefabs/Player.prefab to overwrite.
    [MenuItem("Tools/Fix Duplicate Components (Player)")]
    static void FixDuplicateComponentsPlayer()
    {
        const string PrefabPath = "Assets/Prefabs/Player.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
        {
            Debug.LogError("[SceneSetup] Player prefab not found at Assets/Prefabs/Player.prefab.");
            return;
        }

        var prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);

        System.Type[] types =
        {
            typeof(CharacterController),
            typeof(Health),
            typeof(CombatState),
            typeof(CharacterStats),
            typeof(PlayerFactionScores),
            typeof(PlayerInventory),
            typeof(PlayerEquipment),
            typeof(CombatLog),
            typeof(PlayerAutoAttack),
            typeof(PlayerExperience),
            typeof(PlayerMana),
            typeof(PlayerRegen),
            typeof(PlayerSitting),
            typeof(PlayerAbilities),
            typeof(CharacterPersistence),
            typeof(NetworkedPlayer),
        };

        int removed = 0;
        foreach (var type in types)
        {
            var all = prefabRoot.GetComponents(type);
            for (int i = 1; i < all.Length; i++)
            {
                Object.DestroyImmediate(all[i]);
                removed++;
                Debug.Log($"[SceneSetup] Removed duplicate {type.Name}");
            }
        }

        PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
        PrefabUtility.UnloadPrefabContents(prefabRoot);

        if (removed == 0)
            Debug.Log("[SceneSetup] No duplicates found on Player prefab.");
        else
            Debug.Log($"[SceneSetup] Removed {removed} duplicate component(s) and saved prefab.");
    }

    [MenuItem("Tools/Patch Player Prefab")]
    static void PatchPlayerPrefab()
    {
        const string PrefabPath = "Assets/Prefabs/Player.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
        {
            Debug.LogError("[SceneSetup] Player prefab not found at Assets/Prefabs/Player.prefab. Run Tools/Rebuild Player Prefab (Fresh) first.");
            return;
        }

        var prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);

        PatchComponents(prefabRoot, new System.Type[]
        {
            typeof(CharacterController),
            typeof(Health),
            typeof(CombatState),
            typeof(CharacterStats),
            typeof(PlayerFactionScores),
            typeof(PlayerInventory),
            typeof(PlayerEquipment),
            typeof(CombatLog),
            typeof(PlayerAutoAttack),
            typeof(PlayerExperience),
            typeof(PlayerMana),
            typeof(PlayerRegen),
            typeof(PlayerSitting),
            typeof(PlayerAbilities),
            typeof(CharacterPersistence),
            typeof(PlayerModel),
            typeof(NetworkedPlayer),
        });

        // Wire PlayerModel's locomotion controller (3.1.4) so the runtime-built body animates without a
        // manual assignment. The static Synty child must be DELETED from the prefab by hand — PlayerModel
        // now instantiates the (gender,race,class) body itself, and a leftover child would double up.
        var model = prefabRoot.GetComponent<PlayerModel>();
        if (model != null)
        {
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/Animations/PlayerLocomotion.controller");
            if (controller != null)
            {
                var mSO = new SerializedObject(model);
                mSO.FindProperty("locomotionController").objectReferenceValue = controller;
                mSO.ApplyModifiedProperties();
                Debug.Log("[SceneSetup] Wired PlayerLocomotion onto PlayerModel.");
            }
            else
                Debug.LogWarning("[SceneSetup] Assets/Animations/PlayerLocomotion.controller not found — " +
                                 "run Tools/Build Player Locomotion Controller, then re-patch.");
        }

        // Wire playerCorpsePrefab on NetworkedPlayer
        var np = prefabRoot.GetComponent<NetworkedPlayer>();
        if (np != null)
        {
            var corpsePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerCorpse.prefab");
            if (corpsePrefab != null)
            {
                var npSO = new SerializedObject(np);
                npSO.FindProperty("playerCorpsePrefab").objectReferenceValue = corpsePrefab;
                npSO.ApplyModifiedProperties();
                Debug.Log("[SceneSetup] Wired PlayerCorpse prefab onto NetworkedPlayer.");
            }
            else
                Debug.LogWarning("[SceneSetup] Assets/Prefabs/PlayerCorpse.prefab not found — run Tools/Rebuild PlayerCorpse Prefab (Fresh) first.");
        }

        // Ensure camera is set up
        if (prefabRoot.GetComponentInChildren<Camera>(true) == null)
        {
            SetupCamera(prefabRoot);
            Debug.Log("[SceneSetup] Camera was missing — added.");
        }

        PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
        PrefabUtility.UnloadPrefabContents(prefabRoot);

        Debug.Log("[SceneSetup] Player prefab patched and saved.");
    }

    // Adds any missing components to the existing Enemy prefab instance without touching tuned values.
    // After running: drag the Enemy from Hierarchy into Assets/Prefabs/Enemy.prefab to overwrite.
    [MenuItem("Tools/Patch Enemy Prefab")]
    static void PatchEnemyPrefab()
    {
        const string PrefabPath = "Assets/Prefabs/Enemy.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
        {
            Debug.LogError("[SceneSetup] Enemy prefab not found at Assets/Prefabs/Enemy.prefab. Run Tools/Rebuild Enemy Prefab (Fresh) first.");
            return;
        }

        var prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);

        PatchComponents(prefabRoot, new System.Type[]
        {
            typeof(Targetable),
            typeof(NetworkIdentity),
            typeof(Health),
            typeof(NpcEventDispatcher),
            typeof(NpcFaction),
            typeof(EnemyAI),
            typeof(Corpse),
            typeof(MobKillReward),
            typeof(CombatLog),
            typeof(Enemy),
            typeof(NpcConversation),        // keyword listener (drives vendor "wares")
            typeof(VendorApplicator),       // inert unless the mob's definition sets a vendorId
            typeof(KeywordRewardApplicator),// 3.2 — inert unless a keyword carries a quest transaction bundle
            typeof(MobModel),               // 3.1.10 — runtime body from Resources/MobModels/<modelId>
        });

        // Server-authoritative movement sync — without this, mobs move only on the server and look
        // frozen on remote (non-host) clients. Missing on the original prefab; masked by host-only testing.
        AddEnemyNetworkTransform(prefabRoot);

        WireMobModel(prefabRoot); // 3.1.10 — assign the shared locomotion controller onto MobModel

        PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
        PrefabUtility.UnloadPrefabContents(prefabRoot);

        Debug.Log("[SceneSetup] Enemy prefab patched and saved.");
    }

    // Full destroy-and-recreate — only use when starting from scratch.
    [MenuItem("Tools/Rebuild Player Prefab (Fresh)")]
    static void RebuildPlayerPrefab()
    {
        var existing = GameObject.Find("Player");
        if (existing != null) Object.DestroyImmediate(existing);

        var player = CreatePlayer();
        SetupCamera(player);

        Selection.activeGameObject = player;
        if (SceneView.lastActiveSceneView != null)
            SceneView.lastActiveSceneView.FrameSelected();

        EditorSceneManager.MarkSceneDirty(player.scene);
        Debug.Log("[SceneSetup] Player rebuilt from scratch. Drag it into Assets/Prefabs/Player.prefab to overwrite.");
    }

    // Full destroy-and-recreate — only use when starting from scratch.
    [MenuItem("Tools/Rebuild Enemy Prefab (Fresh)")]
    static void RebuildEnemyPrefab()
    {
        var existing = GameObject.Find("Enemy");
        if (existing != null) Object.DestroyImmediate(existing);

        var enemy = CreateEnemy();

        Selection.activeGameObject = enemy;
        if (SceneView.lastActiveSceneView != null)
            SceneView.lastActiveSceneView.FrameSelected();

        EditorSceneManager.MarkSceneDirty(enemy.scene);
        Debug.Log("[SceneSetup] Enemy rebuilt from scratch. Drag it into Assets/Prefabs/Enemy.prefab to overwrite.");
    }

    static void PatchComponents(GameObject go, System.Type[] required)
    {
        // Remove duplicates first, then add any missing.
        int removed = 0;
        foreach (var type in required)
        {
            var all = go.GetComponents(type);
            for (int i = 1; i < all.Length; i++)
            {
                Object.DestroyImmediate(all[i]);
                removed++;
                Debug.Log($"[SceneSetup] Removed duplicate {type.Name}");
            }
        }

        int added = 0;
        foreach (var type in required)
        {
            if (go.GetComponent(type) == null)
            {
                go.AddComponent(type);
                added++;
                Debug.Log($"[SceneSetup] Added missing component: {type.Name}");
            }
        }

        if (removed == 0 && added == 0)
            Debug.Log("[SceneSetup] All required components already present — nothing added.");
    }

    // ── Scene bootstrap (no Player, no Enemy) ────────────────────────────────

    static void SetupScene()
    {
        // Only the NetworkManager is rebuilt each run (re-wires transport / authenticator / UI).
        // Terrain is intentionally NOT touched here — the world is a hand-placed Synty map and
        // Setup All must never clobber it. The old primitive Ground + Obstacles scaffolding now
        // lives behind Tools/Create Prototype Terrain for fresh/empty scenes only.
        var oldNm = GameObject.Find("NetworkManager");
        if (oldNm != null) Object.DestroyImmediate(oldNm);

        CreateNetworkManager();
        CreateChatManager();
        CreateItemRegistry();
        CreateAbilityRegistry();
        EnsureDirectionalLight();

        // 1.7: the HUD/menu canvases now live in the additive UI.unity scene (Tools/Build UI Scene).
        // Strip any canvases this scene built before the refactor, and add the loader that pulls in
        // the UI layer additively at runtime.
        RemoveStaleHudCanvases();
        CreateUIManager();

#if MIRROR
        Debug.Log("[SceneSetup] Scene setup complete.");
#else
        Debug.LogWarning("[SceneSetup] Scene setup complete (no Mirror). Install Mirror from the Asset Store, then re-run.");
#endif
    }

    // ── Additive UI scene (1.7) ──────────────────────────────────────────────────

    const string UIScenePath = "Assets/Scenes/UI.unity";

    // Build the HUD/menu canvases into their own scene, loaded additively at runtime by UIManager.
    // Re-runnable: regenerates UI.unity from scratch each time.
    [MenuItem("Tools/Build UI Scene")]
    static void BuildUIScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string prevPath = EditorSceneManager.GetActiveScene().path;

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // These build into the now-active (empty) UI scene.
        CreateChatUI();
        CreateHUDFrames();
        CreateInventoryUI();
        CreateEquipmentUI();
        CreateVendorUI();
        CreateLootUI();
        CreateHotbarUI();
        EnsureEventSystem();

        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            AssetDatabase.CreateFolder("Assets", "Scenes");

        var ui = EditorSceneManager.GetActiveScene();
        EditorSceneManager.SaveScene(ui, UIScenePath);
        AddSceneToBuildSettings(UIScenePath);

        if (!string.IsNullOrEmpty(prevPath))
            EditorSceneManager.OpenScene(prevPath);

        Debug.Log($"[SceneSetup] Built {UIScenePath}.");
        EditorUtility.DisplayDialog("Build UI Scene",
            $"Built {UIScenePath} and added it to Build Settings.\n\n" +
            "Now re-run Tools/Setup All so the gameplay scene drops its old HUD canvases and gets the " +
            "UIManager loader.", "OK");
    }

    static void AddSceneToBuildSettings(string path)
    {
        var existing = EditorBuildSettings.scenes;
        foreach (var s in existing)
            if (s.path == path) return; // already present

        var updated = new EditorBuildSettingsScene[existing.Length + 1];
        System.Array.Copy(existing, updated, existing.Length);
        updated[existing.Length] = new EditorBuildSettingsScene(path, true);
        EditorBuildSettings.scenes = updated;
    }

    // Remove the pre-1.7 HUD canvases (+ EventSystem) from the gameplay scene — they live in UI.unity now.
    static void RemoveStaleHudCanvases()
    {
        string[] names =
        {
            "ChatCanvas", "HUDCanvas", "InventoryCanvas", "EquipmentCanvas",
            "VendorCanvas", "LootCanvas", "HotbarCanvas", "EventSystem",
        };
        foreach (var n in names)
        {
            var go = GameObject.Find(n);
            if (go != null) Object.DestroyImmediate(go);
        }
    }

    static void CreateUIManager()
    {
        var existing = GameObject.Find("UIManager");
        if (existing != null) Object.DestroyImmediate(existing);
        var go = new GameObject("UIManager");
        go.AddComponent<UIManager>();
    }

    // ── Ground / Obstacles ────────────────────────────────────────────────────

    // Prototype primitive terrain (grey Ground plane + Obstacle cubes). Decoupled from Setup All
    // so it never clobbers the hand-placed Synty map — only for bootstrapping a fresh/empty scene.
    [MenuItem("Tools/Create Prototype Terrain")]
    static void CreatePrototypeTerrain()
    {
        foreach (string n in new[] { "Ground", "Obstacles" })
        {
            var existing = GameObject.Find(n);
            if (existing != null) Object.DestroyImmediate(existing);
        }
        CreateGround();
        CreateObstacles();
        Debug.Log("[SceneSetup] Created prototype Ground + Obstacles.");
    }

    static void CreateGround()
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(10, 1, 10);

        var surfaceType = FindType("Unity.AI.Navigation.NavMeshSurface");
        if (surfaceType != null)
            ground.AddComponent(surfaceType);
        else
            Debug.LogWarning("[SceneSetup] AI Navigation package not installed — install it from Package Manager, then add NavMeshSurface to Ground and Bake.");
    }

    static void CreateObstacles()
    {
        var parent = new GameObject("Obstacles");

        (Vector3 pos, Vector3 scale)[] layout =
        {
            (new Vector3( 8, 2,  8), new Vector3(1, 4, 1)),
            (new Vector3(-8, 2,  8), new Vector3(1, 4, 1)),
            (new Vector3( 8, 2, -8), new Vector3(1, 4, 1)),
            (new Vector3(-8, 2, -8), new Vector3(1, 4, 1)),
            (new Vector3(12, 0.5f,  0), new Vector3(4, 1, 4)),
            (new Vector3(-12, 1f,   0), new Vector3(4, 2, 4)),
            (new Vector3(  0, 0.5f, 12), new Vector3(6, 1, 3)),
            (new Vector3(-6, 0.25f, -12), new Vector3(3, 0.5f, 3)),
            (new Vector3(-9, 0.75f, -12), new Vector3(3, 1.5f, 3)),
            (new Vector3( 0, 1.5f, -18), new Vector3(24, 3, 1)),
        };

        foreach (var (pos, scale) in layout)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(parent.transform);
            cube.transform.position = pos;
            cube.transform.localScale = scale;
        }
    }

    // ── Player ────────────────────────────────────────────────────────────────

    static GameObject CreatePlayer()
    {
        var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "Player";
        player.transform.position = DefaultSpawnPos;
        player.tag = "Player";

        Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());

        var cc = player.AddComponent<CharacterController>();
        cc.height = 2f;
        cc.radius = 0.5f;
        cc.center = Vector3.zero;

#if MIRROR
        player.AddComponent<NetworkIdentity>();

        // 4.2 — reverted to the stock Mirror component: the owner reconciles its own transform again
        // (NetworkedPlayer's prediction/reconciliation), so the 4.1-era ServerAuthoritativeTransform
        // subclass (which made the owner apply incoming server snapshots too) is no longer needed and has
        // been deleted.
        var ntType = FindType("Mirror.NetworkTransformReliable") ?? FindType("Mirror.NetworkTransform");
        if (ntType != null)
        {
            var nt = player.AddComponent(ntType);
            var syncRotField = ntType.GetField("syncRotation");
            syncRotField?.SetValue(nt, false);
        }
        else Debug.LogError("Could not find a NetworkTransform type — check your Mirror version.");

        player.AddComponent<Health>();
        player.AddComponent<CombatState>();
        player.AddComponent<CharacterStats>();
        player.AddComponent<PlayerFactionScores>();
        player.AddComponent<PlayerInventory>();
        player.AddComponent<PlayerEquipment>();
        player.AddComponent<CombatLog>();
        player.AddComponent<PlayerAutoAttack>();
        player.AddComponent<PlayerExperience>();
        player.AddComponent<PlayerMana>();
        player.AddComponent<PlayerRegen>();
        player.AddComponent<PlayerSitting>();
        player.AddComponent<PlayerAbilities>();
        player.AddComponent<CharacterPersistence>();
        var np = player.AddComponent<NetworkedPlayer>();

        var so = new SerializedObject(np);
        so.FindProperty("cameraHolder").objectReferenceValue = CreateCameraHolder(player);
        so.ApplyModifiedPropertiesWithoutUndo();
#else
        var pc = player.AddComponent<PlayerController>();

        var so = new SerializedObject(pc);
        so.FindProperty("cameraHolder").objectReferenceValue = CreateCameraHolder(player);
        so.ApplyModifiedPropertiesWithoutUndo();
#endif

        return player;
    }

    static Transform CreateCameraHolder(GameObject player)
    {
        var camHolder = new GameObject("CameraHolder");
        camHolder.transform.SetParent(player.transform);
        camHolder.transform.localPosition = new Vector3(0, 0.65f, 0);
        return camHolder.transform;
    }

    static void SetupCamera(GameObject player)
    {
        var camHolder = player.transform.Find("CameraHolder");
        if (camHolder == null) { Debug.LogWarning("[SceneSetup] CameraHolder not found on player."); return; }

        Camera mainCam = Camera.main;
        GameObject camObj = mainCam != null ? mainCam.gameObject : new GameObject("Main Camera");
        if (camObj.GetComponent<Camera>() == null) camObj.AddComponent<Camera>();
        camObj.tag = "MainCamera";

#if MIRROR
        camObj.SetActive(false);
#endif

        camObj.transform.SetParent(camHolder);
        camObj.transform.localPosition = Vector3.zero;
        camObj.transform.localRotation = Quaternion.identity;
    }

    // ── Enemy ─────────────────────────────────────────────────────────────────

    static GameObject CreateEnemy()
    {
        var enemy = GameObject.CreatePrimitive(PrimitiveType.Cube);
        enemy.name = "Enemy";
        enemy.transform.position = new Vector3(0f, 0.5f, 8f);
        enemy.transform.localScale = new Vector3(1f, 1f, 1f);

        enemy.AddComponent<Targetable>();

#if MIRROR
        enemy.AddComponent<NetworkIdentity>();
        AddEnemyNetworkTransform(enemy);
        enemy.AddComponent<Health>();
        enemy.AddComponent<NpcEventDispatcher>();
        enemy.AddComponent<NpcFaction>();
        enemy.AddComponent<EnemyAI>();
        enemy.AddComponent<Corpse>();
        enemy.AddComponent<MobKillReward>();
        enemy.AddComponent<CombatLog>();
        enemy.AddComponent<Enemy>();
        enemy.AddComponent<NpcEventLogger>();
        enemy.AddComponent<MobModel>();   // 3.1.10 — runtime body from Resources/MobModels/<modelId>

        Debug.LogWarning("[SceneSetup] Enemy rebuilt — bake the NavMesh before hitting Play (Window > AI > Navigation > Bake).");

        // Conversations are DB-authored (M2.4) and resolved at runtime via the mob's
        // conversationSetId (ConversationRegistry); no SO keyword set is wired here anymore.
        enemy.AddComponent<NpcConversation>();
        enemy.AddComponent<VendorApplicator>();        // inert unless the mob's definition sets a vendorId
        enemy.AddComponent<KeywordRewardApplicator>(); // 3.2 — inert unless a keyword carries a transaction

        WireMobModel(enemy);
#endif

        return enemy;
    }

    // Wire the shared locomotion controller onto MobModel (3.1.10) so runtime-built Synty bodies animate
    // (Humanoid mob bodies retarget PlayerLocomotion; movement → the blend tree via PlayerAnimator).
    static void WireMobModel(GameObject root)
    {
#if MIRROR
        var mobModel = root.GetComponent<MobModel>();
        if (mobModel == null) return;

        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/Animations/PlayerLocomotion.controller");
        if (controller == null)
        {
            Debug.LogWarning("[SceneSetup] PlayerLocomotion.controller not found — mob bodies won't animate " +
                             "until you run Tools/Build Player Locomotion Controller, then re-patch the Enemy.");
            return;
        }

        var so = new SerializedObject(mobModel);
        so.FindProperty("locomotionController").objectReferenceValue = controller;
        so.ApplyModifiedPropertiesWithoutUndo();
        Debug.Log("[SceneSetup] Wired PlayerLocomotion onto MobModel.");
#endif
    }

    // Mobs move server-side (EnemyAI drives a NavMeshAgent on the server). That movement only reaches
    // remote clients through a NetworkTransform — without one, non-host clients see the mob frozen at its
    // spawn while the server AI actually chases/attacks (host-only testing hid this). Server-authoritative
    // (syncDirection stays ServerToClient), syncRotation on so remote clients see mobs turn toward their
    // target (EnemyAI drives facing server-side; also forced on at runtime in EnemyAI.Awake), and
    // coordinateSpace = World so mobs in offset zone scenes (3.0) sync at their true world position.
    static void AddEnemyNetworkTransform(GameObject enemy)
    {
#if MIRROR
        var ntType = FindType("Mirror.NetworkTransformReliable") ?? FindType("Mirror.NetworkTransform");
        if (ntType == null) { Debug.LogError("[SceneSetup] No NetworkTransform type found — check your Mirror version."); return; }

        var nt = enemy.GetComponent(ntType) ?? enemy.AddComponent(ntType);

        var so = new SerializedObject(nt);
        so.FindProperty("syncPosition").boolValue = true;
        so.FindProperty("syncRotation").boolValue = true;
        var cs = so.FindProperty("coordinateSpace");
        if (cs != null) cs.enumValueIndex = 1; // Mirror.CoordinateSpace.World
        so.ApplyModifiedPropertiesWithoutUndo();
#endif
    }

    // ── NetworkManager ────────────────────────────────────────────────────────

    static void CreateNetworkManager()
    {
#if MIRROR
        var nmObj = new GameObject("NetworkManager");
        var nm = nmObj.AddComponent<GameNetworkManager>();
        nmObj.AddComponent<GameNetworkHUD>();
        nmObj.AddComponent<LoginUI>();
        var auth = nmObj.AddComponent<AccountAuthenticator>();
        nmObj.AddComponent<CharacterSelectController>(); // 1.5 — server-side select/create handlers
        nmObj.AddComponent<CharacterSelectUI>();         // 1.5 — pre-spawn client select/create panel
        nmObj.AddComponent<CampController>();            // 1.6.1 — client-side camp countdown

        string[] transportCandidates = {
            "Mirror.KcpTransport",
            "kcp2k.KcpTransport",
            "Mirror.TelepathyTransport",
        };
        Component transport = null;
        foreach (var typeName in transportCandidates)
        {
            var t = FindType(typeName);
            if (t != null) { transport = nmObj.AddComponent(t); break; }
        }

        var nmSo = new SerializedObject(nm);

        if (transport != null)
            nmSo.FindProperty("transport").objectReferenceValue = transport;
        else
            Debug.LogError("No Mirror transport found. Add KcpTransport to the NetworkManager manually.");

        // Gate the world behind the login handshake (1.4).
        nmSo.FindProperty("authenticator").objectReferenceValue = auth;

        // 1.5: don't auto-spawn the player on connect — CharacterSelectController spawns it once a
        // character is created/selected (via AddPlayerForConnection).
        nmSo.FindProperty("autoCreatePlayer").boolValue = false;

        var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
        if (playerPrefab != null)
            nmSo.FindProperty("playerPrefab").objectReferenceValue = playerPrefab;
        else
            Debug.LogWarning("Player prefab not found at Assets/Prefabs/Player.prefab — assign it manually on NetworkManager.");

        nmSo.ApplyModifiedPropertiesWithoutUndo();

        var spawnPoint = new GameObject("SpawnPoint");
        spawnPoint.transform.position = DefaultSpawnPos;
        spawnPoint.AddComponent<NetworkStartPosition>();
#endif
    }

    // ── Chat ──────────────────────────────────────────────────────────────────

    static void CreateChatManager()
    {
        var existing = GameObject.Find("ChatManager");
        if (existing != null) Object.DestroyImmediate(existing);

#if MIRROR
        var go = new GameObject("ChatManager");
        go.AddComponent<NetworkIdentity>();
        go.AddComponent<ChatManager>();
#endif
    }

    static void CreateChatUI()
    {
        var existing = GameObject.Find("ChatCanvas");
        if (existing != null) Object.DestroyImmediate(existing);

        var canvasObj = new GameObject("ChatCanvas");
        var canvas    = canvasObj.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasObj.AddComponent<GraphicRaycaster>();

        // ── Panel ─────────────────────────────────────────────────────────────
        var panelObj  = new GameObject("ChatPanel");
        panelObj.transform.SetParent(canvasObj.transform, false);
        var panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin        = Vector2.zero;
        panelRect.anchorMax        = Vector2.zero;
        panelRect.pivot            = Vector2.zero;
        panelRect.anchoredPosition = new Vector2(10, 10);
        panelRect.sizeDelta        = new Vector2(500, 220);
        var panelImg  = panelObj.AddComponent<Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.45f);

        // ── Drag bar (top strip — move the window) ────────────────────────────
        const float DragBarH  = 22f;
        const float InputRowH = 36f;
        const float ResizeW   =  8f;

        var dragBarObj  = new GameObject("DragBar");
        dragBarObj.transform.SetParent(panelObj.transform, false);
        var dragBarRect = dragBarObj.AddComponent<RectTransform>();
        dragBarRect.anchorMin        = new Vector2(0, 1);
        dragBarRect.anchorMax        = new Vector2(1, 1);
        dragBarRect.pivot            = new Vector2(0.5f, 1);
        dragBarRect.anchoredPosition = Vector2.zero;
        dragBarRect.sizeDelta        = new Vector2(0, DragBarH);
        var dragBarImg  = dragBarObj.AddComponent<Image>();
        dragBarImg.color = new Color(0f, 0f, 0f, 0.65f);
        var dragComp = dragBarObj.AddComponent<UIDrag>();
        var dragSO   = new SerializedObject(dragComp);
        dragSO.FindProperty("target").objectReferenceValue = panelRect;
        dragSO.ApplyModifiedPropertiesWithoutUndo();

        var dragLabelObj  = new GameObject("DragBarLabel");
        dragLabelObj.transform.SetParent(dragBarObj.transform, false);
        var dragLabelRect = dragLabelObj.AddComponent<RectTransform>();
        dragLabelRect.anchorMin = Vector2.zero;
        dragLabelRect.anchorMax = Vector2.one;
        dragLabelRect.offsetMin = new Vector2(6, 0);
        dragLabelRect.offsetMax = Vector2.zero;
        var dragLabelTMP = dragLabelObj.AddComponent<TextMeshProUGUI>();
        dragLabelTMP.text      = "Chat";
        dragLabelTMP.fontSize  = 12;
        dragLabelTMP.color     = new Color(0.8f, 0.8f, 0.8f, 1f);
        dragLabelTMP.alignment = TextAlignmentOptions.MidlineLeft;
        dragLabelTMP.raycastTarget = false;

        // ── Input row (bottom strip) ──────────────────────────────────────────
        var inputRowObj  = new GameObject("InputRow");
        inputRowObj.transform.SetParent(panelObj.transform, false);
        var inputRowRect = inputRowObj.AddComponent<RectTransform>();
        inputRowRect.anchorMin        = new Vector2(0, 0);
        inputRowRect.anchorMax        = new Vector2(1, 0);
        inputRowRect.pivot            = new Vector2(0, 0);
        inputRowRect.anchoredPosition = Vector2.zero;
        inputRowRect.sizeDelta        = new Vector2(0, InputRowH);
        var inputRowImg  = inputRowObj.AddComponent<Image>();
        inputRowImg.color = new Color(0f, 0f, 0f, 0.65f);
        inputRowObj.SetActive(false);

        var resources    = new TMP_DefaultControls.Resources();
        var inputFieldGO = TMP_DefaultControls.CreateInputField(resources);
        inputFieldGO.transform.SetParent(inputRowObj.transform, false);
        var inputFieldRect = inputFieldGO.GetComponent<RectTransform>();
        inputFieldRect.anchorMin        = Vector2.zero;
        inputFieldRect.anchorMax        = Vector2.one;
        inputFieldRect.sizeDelta        = Vector2.zero;
        inputFieldRect.anchoredPosition = Vector2.zero;
        var inputField = inputFieldGO.GetComponent<TMP_InputField>();
        inputField.pointSize = 14;

        // ── ScrollView (fills between drag bar and input row) ─────────────────
        var scrollViewObj  = new GameObject("ScrollView");
        scrollViewObj.transform.SetParent(panelObj.transform, false);
        var scrollViewRect = scrollViewObj.AddComponent<RectTransform>();
        scrollViewRect.anchorMin = Vector2.zero;
        scrollViewRect.anchorMax = Vector2.one;
        scrollViewRect.offsetMin = new Vector2(0, InputRowH);
        scrollViewRect.offsetMax = new Vector2(0, -DragBarH);
        var scrollRect = scrollViewObj.AddComponent<ScrollRect>();
        scrollRect.horizontal    = false;
        scrollRect.vertical      = true;
        scrollRect.movementType  = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 30f;

        // Viewport — clips content via Mask
        var viewportObj  = new GameObject("Viewport");
        viewportObj.transform.SetParent(scrollViewObj.transform, false);
        var viewportRect = viewportObj.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.sizeDelta = Vector2.zero;
        viewportRect.anchoredPosition = Vector2.zero;
        var viewportImg  = viewportObj.AddComponent<Image>();
        viewportImg.color        = Color.white;
        viewportImg.raycastTarget = false;
        var viewportMask = viewportObj.AddComponent<Mask>();
        viewportMask.showMaskGraphic = false;

        // Content — VerticalLayoutGroup queries TMP_Text for preferred height;
        // ContentSizeFitter reads that from the VLG and sets sizeDelta accordingly.
        // Without VLG, ContentSizeFitter has no ILayoutElement on Content and stays at height 0.
        var contentObj  = new GameObject("Content");
        contentObj.transform.SetParent(viewportObj.transform, false);
        var contentRect = contentObj.AddComponent<RectTransform>();
        contentRect.anchorMin        = new Vector2(0, 1);
        contentRect.anchorMax        = new Vector2(1, 1);
        contentRect.pivot            = new Vector2(0, 1);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta        = new Vector2(0, 0);
        var vlg = contentObj.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = true;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.padding = new RectOffset(6, 6, 4, 4);
        var fitter = contentObj.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Log text — VLG controls size; no manual anchor offsets needed
        var logObj  = new GameObject("LogText");
        logObj.transform.SetParent(contentObj.transform, false);
        logObj.AddComponent<RectTransform>();
        var logText = logObj.AddComponent<TextMeshProUGUI>();
        logText.enableAutoSizing   = false;
        logText.fontSize           = 14;
        logText.color              = Color.white;
        logText.alignment          = TextAlignmentOptions.TopLeft;
        logText.overflowMode       = TextOverflowModes.Overflow;
        logText.textWrappingMode   = TMPro.TextWrappingModes.Normal;

        scrollRect.viewport = viewportRect;
        scrollRect.content  = contentRect;

        // ── Resize handles (later children = higher event priority) ───────────
        // Top edge — resize height
        AddResizeHandle("ResizeTop", panelObj.transform, panelRect,
            anchorMin: new Vector2(0, 1), anchorMax: new Vector2(1, 1),
            pivot: new Vector2(0.5f, 1),
            anchoredPos: Vector2.zero, size: new Vector2(0, ResizeW),
            edges: ResizeEdge.Top);

        // Right edge — resize width
        AddResizeHandle("ResizeRight", panelObj.transform, panelRect,
            anchorMin: new Vector2(1, 0), anchorMax: new Vector2(1, 1),
            pivot: new Vector2(1, 0.5f),
            anchoredPos: Vector2.zero, size: new Vector2(ResizeW, 0),
            edges: ResizeEdge.Right);

        // Top-right corner — resize both
        AddResizeHandle("ResizeTopRight", panelObj.transform, panelRect,
            anchorMin: new Vector2(1, 1), anchorMax: new Vector2(1, 1),
            pivot: new Vector2(1, 1),
            anchoredPos: Vector2.zero, size: new Vector2(ResizeW, ResizeW),
            edges: ResizeEdge.Top | ResizeEdge.Right);

        // ── Wire ChatUI ───────────────────────────────────────────────────────
        var chatUI = panelObj.AddComponent<ChatUI>();
        var so     = new SerializedObject(chatUI);
        so.FindProperty("log").objectReferenceValue        = logText;
        so.FindProperty("scrollRect").objectReferenceValue = scrollRect;
        so.FindProperty("inputRow").objectReferenceValue   = inputRowObj;
        so.FindProperty("inputField").objectReferenceValue = inputField;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void AddResizeHandle(string objName, Transform parent, RectTransform panel,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 size, ResizeEdge edges)
    {
        var obj  = new GameObject(objName);
        obj.transform.SetParent(parent, false);
        var rect = obj.AddComponent<RectTransform>();
        rect.anchorMin        = anchorMin;
        rect.anchorMax        = anchorMax;
        rect.pivot            = pivot;
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta        = size;
        var img  = obj.AddComponent<Image>();
        img.color         = new Color(0f, 0f, 0f, 0f);
        img.raycastTarget = true;
        var handle = obj.AddComponent<UIResizeHandle>();
        var so     = new SerializedObject(handle);
        so.FindProperty("panel").objectReferenceValue = panel;
        so.FindProperty("edges").intValue             = (int)edges;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ── HUD (player frame + target frame) ─────────────────────────────────────

    static void CreateHUDFrames()
    {
        var existing = GameObject.Find("HUDCanvas");
        if (existing != null) Object.DestroyImmediate(existing);

        var canvasObj = new GameObject("HUDCanvas");
        var canvas    = canvasObj.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 15;
        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasObj.AddComponent<GraphicRaycaster>();

        // Player frame — always visible, green health bar
        var (pPanel, pName, pFill, pHpText) = CreateFramePanel(
            "PlayerFramePanel", canvasObj.transform,
            new Vector2(10, -10), new Vector2(220, 52),
            new Color(0.2f, 0.75f, 0.2f));

        // Combat indicator — a red rim behind the player frame, pulsed by PlayerFrame while in combat (1.6.1).
        var borderObj  = new GameObject("CombatBorder");
        borderObj.transform.SetParent(pPanel.transform, false);
        var borderRect = borderObj.AddComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.offsetMin = new Vector2(-3, -3);
        borderRect.offsetMax = new Vector2(3, 3);
        var borderImg  = borderObj.AddComponent<Image>();
        borderImg.color        = new Color(1f, 0.1f, 0.1f, 0f); // red, hidden until in combat
        borderImg.raycastTarget = false;
        borderObj.transform.SetSiblingIndex(0); // render behind the panel background → shows as a rim

        var playerFrame = canvasObj.AddComponent<PlayerFrame>();
        var pfSO = new SerializedObject(playerFrame);
        pfSO.FindProperty("nameLabel").objectReferenceValue    = pName;
        pfSO.FindProperty("healthFill").objectReferenceValue   = pFill;
        pfSO.FindProperty("healthText").objectReferenceValue   = pHpText;
        pfSO.FindProperty("combatBorder").objectReferenceValue = borderImg;
        pfSO.ApplyModifiedPropertiesWithoutUndo();

        // Target frame — hidden until a target is selected, red health bar
        var (tPanel, tName, tFill, tHpText) = CreateFramePanel(
            "TargetFramePanel", canvasObj.transform,
            new Vector2(10, -72), new Vector2(220, 52),
            new Color(0.8f, 0.2f, 0.2f));
        tPanel.SetActive(false);

        var targetFrame = canvasObj.AddComponent<TargetFrame>();
        var tfSO = new SerializedObject(targetFrame);
        tfSO.FindProperty("panel").objectReferenceValue      = tPanel;
        tfSO.FindProperty("nameLabel").objectReferenceValue  = tName;
        tfSO.FindProperty("healthFill").objectReferenceValue = tFill;
        tfSO.FindProperty("healthText").objectReferenceValue = tHpText;
        tfSO.ApplyModifiedPropertiesWithoutUndo();
    }

    static (GameObject panel, TextMeshProUGUI name, Image fill, TextMeshProUGUI hpText)
        CreateFramePanel(string panelName, Transform parent, Vector2 pos, Vector2 size, Color barColor)
    {
        // Background panel
        var panelObj  = new GameObject(panelName);
        panelObj.transform.SetParent(parent, false);
        var panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin        = new Vector2(0, 1);
        panelRect.anchorMax        = new Vector2(0, 1);
        panelRect.pivot            = new Vector2(0, 1);
        panelRect.anchoredPosition = pos;
        panelRect.sizeDelta        = size;
        var panelImg  = panelObj.AddComponent<Image>();
        panelImg.color        = new Color(0.05f, 0.05f, 0.05f, 0.82f);
        panelImg.raycastTarget = true;
        var frameDrag = panelObj.AddComponent<UIDrag>();
        frameDrag.Init(panelRect);

        // Name label — top row
        var nameObj  = new GameObject("Name");
        nameObj.transform.SetParent(panelObj.transform, false);
        var nameRect = nameObj.AddComponent<RectTransform>();
        nameRect.anchorMin        = new Vector2(0, 1);
        nameRect.anchorMax        = new Vector2(1, 1);
        nameRect.pivot            = new Vector2(0.5f, 1);
        nameRect.anchoredPosition = new Vector2(0, -5);
        nameRect.sizeDelta        = new Vector2(-12, 18);
        var nameTMP = nameObj.AddComponent<TextMeshProUGUI>();
        nameTMP.fontSize      = 13;
        nameTMP.color         = Color.white;
        nameTMP.alignment     = TextAlignmentOptions.MidlineLeft;
        nameTMP.text          = "---";
        nameTMP.raycastTarget = false;

        // Health bar background — bottom row
        var bgObj  = new GameObject("HealthBarBG");
        bgObj.transform.SetParent(panelObj.transform, false);
        var bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin        = new Vector2(0, 0);
        bgRect.anchorMax        = new Vector2(1, 0);
        bgRect.pivot            = new Vector2(0.5f, 0);
        bgRect.anchoredPosition = new Vector2(0, 7);
        bgRect.sizeDelta        = new Vector2(-12, 16);
        var bgImg  = bgObj.AddComponent<Image>();
        bgImg.color        = new Color(0.12f, 0.12f, 0.12f);
        bgImg.raycastTarget = false;

        // Health bar fill
        var fillObj  = new GameObject("HealthBarFill");
        fillObj.transform.SetParent(bgObj.transform, false);
        var fillRect = fillObj.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;
        var fillImg  = fillObj.AddComponent<Image>();
        fillImg.color        = barColor;
        fillImg.raycastTarget = false;

        // HP text centered over bar
        var hpObj  = new GameObject("HealthText");
        hpObj.transform.SetParent(bgObj.transform, false);
        var hpRect = hpObj.AddComponent<RectTransform>();
        hpRect.anchorMin = Vector2.zero;
        hpRect.anchorMax = Vector2.one;
        hpRect.sizeDelta = Vector2.zero;
        var hpTMP  = hpObj.AddComponent<TextMeshProUGUI>();
        hpTMP.fontSize      = 11;
        hpTMP.color         = Color.white;
        hpTMP.alignment     = TextAlignmentOptions.Center;
        hpTMP.text          = "---";
        hpTMP.raycastTarget = false;

        return (panelObj, nameTMP, fillImg, hpTMP);
    }

    // ── Inventory ─────────────────────────────────────────────────────────────

    static void CreateItemRegistry()
    {
        var existing = GameObject.Find("ItemRegistry");
        if (existing != null) Object.DestroyImmediate(existing);

        var go = new GameObject("ItemRegistry");
        go.AddComponent<ItemRegistry>();
    }

    static void CreateInventoryUI()
    {
        var existing = GameObject.Find("InventoryCanvas");
        if (existing != null) Object.DestroyImmediate(existing);

        // Layout constants
        const float TitleH    = 30f;
        const float TileW     = 120f;
        const float TileH     = 52f;
        const float TileGapX  = 4f;
        const float TileGapY  = 4f;
        const int   Cols      = 2;
        const float PadX      = 8f;
        const float PadY      = 8f;
        const float CurrencyH = 24f;
        const float CurrPadB  = 8f;

        int   rows   = PlayerInventory.SlotCount / Cols;
        float panelW = PadX * 2 + Cols * TileW + (Cols - 1) * TileGapX;
        float panelH = TitleH + PadY + rows * TileH + (rows - 1) * TileGapY + PadY + CurrencyH + CurrPadB;

        var canvasObj = new GameObject("InventoryCanvas");
        var canvas    = canvasObj.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasObj.AddComponent<GraphicRaycaster>();

        // ── Panel ──────────────────────────────────────────────────────────────
        var panelObj  = new GameObject("InventoryPanel");
        panelObj.transform.SetParent(canvasObj.transform, false);
        var panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin        = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax        = new Vector2(0.5f, 0.5f);
        panelRect.pivot            = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta        = new Vector2(panelW, panelH);
        panelObj.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.85f);
        panelObj.SetActive(false);

        // ── Title drag bar ─────────────────────────────────────────────────────
        var titleObj  = new GameObject("Title");
        titleObj.transform.SetParent(panelObj.transform, false);
        var titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin        = new Vector2(0, 1);
        titleRect.anchorMax        = new Vector2(1, 1);
        titleRect.pivot            = new Vector2(0.5f, 1);
        titleRect.anchoredPosition = Vector2.zero;
        titleRect.sizeDelta        = new Vector2(0, TitleH);
        titleObj.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        titleObj.AddComponent<UIDrag>().Init(panelRect);

        var tlLabelObj  = new GameObject("TitleLabel");
        tlLabelObj.transform.SetParent(titleObj.transform, false);
        var tlRect = tlLabelObj.AddComponent<RectTransform>();
        tlRect.anchorMin = Vector2.zero;
        tlRect.anchorMax = Vector2.one;
        tlRect.sizeDelta = Vector2.zero;
        var titleTMP = tlLabelObj.AddComponent<TextMeshProUGUI>();
        titleTMP.text          = "Inventory";
        titleTMP.fontSize      = 16;
        titleTMP.color         = Color.white;
        titleTMP.alignment     = TextAlignmentOptions.Center;
        titleTMP.raycastTarget = false;

        // ── Slot tiles (2 cols × 4 rows) ───────────────────────────────────────
        var slotTiles = new InventorySlotUI[PlayerInventory.SlotCount];
        for (int i = 0; i < PlayerInventory.SlotCount; i++)
        {
            int   col  = i % Cols;
            int   row  = i / Cols;
            float xPos = PadX + col * (TileW + TileGapX);
            float yPos = -(TitleH + PadY + row * (TileH + TileGapY));

            var tileObj  = new GameObject($"Slot{i + 1}");
            tileObj.transform.SetParent(panelObj.transform, false);
            var tileRect = tileObj.AddComponent<RectTransform>();
            tileRect.anchorMin        = new Vector2(0, 1);
            tileRect.anchorMax        = new Vector2(0, 1);
            tileRect.pivot            = new Vector2(0, 1);
            tileRect.anchoredPosition = new Vector2(xPos, yPos);
            tileRect.sizeDelta        = new Vector2(TileW, TileH);
            var tileBG = tileObj.AddComponent<Image>();
            tileBG.color        = new Color(0.15f, 0.15f, 0.15f);
            tileBG.raycastTarget = true;

            // Item name — upper 60% of tile
            var nameObj  = new GameObject("Name");
            nameObj.transform.SetParent(tileObj.transform, false);
            var nameRect = nameObj.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0, 0.38f);
            nameRect.anchorMax = Vector2.one;
            nameRect.offsetMin = new Vector2(5, 0);
            nameRect.offsetMax = new Vector2(-5, -4);
            var nameTMP = nameObj.AddComponent<TextMeshProUGUI>();
            nameTMP.fontSize      = 13;
            nameTMP.color         = Color.white;
            nameTMP.alignment     = TextAlignmentOptions.MidlineLeft;
            nameTMP.overflowMode  = TextOverflowModes.Ellipsis;
            nameTMP.raycastTarget = false;

            // Quantity — lower 38%, right-aligned
            var qtyObj  = new GameObject("Qty");
            qtyObj.transform.SetParent(tileObj.transform, false);
            var qtyRect = qtyObj.AddComponent<RectTransform>();
            qtyRect.anchorMin = new Vector2(0, 0);
            qtyRect.anchorMax = new Vector2(1, 0.38f);
            qtyRect.offsetMin = new Vector2(5, 2);
            qtyRect.offsetMax = new Vector2(-5, 0);
            var qtyTMP = qtyObj.AddComponent<TextMeshProUGUI>();
            qtyTMP.fontSize      = 11;
            qtyTMP.color         = new Color(0.7f, 0.7f, 0.7f);
            qtyTMP.alignment     = TextAlignmentOptions.MidlineRight;
            qtyTMP.raycastTarget = false;

            var slotUI = tileObj.AddComponent<InventorySlotUI>();
            var slotSO = new SerializedObject(slotUI);
            slotSO.FindProperty("background").objectReferenceValue   = tileBG;
            slotSO.FindProperty("nameText").objectReferenceValue     = nameTMP;
            slotSO.FindProperty("quantityText").objectReferenceValue = qtyTMP;
            slotSO.FindProperty("slotIndex").intValue                = i;
            slotSO.ApplyModifiedPropertiesWithoutUndo();

            slotTiles[i] = slotUI;
        }

        // ── Currency label ─────────────────────────────────────────────────────
        var currObj  = new GameObject("Currency");
        currObj.transform.SetParent(panelObj.transform, false);
        var currRect = currObj.AddComponent<RectTransform>();
        currRect.anchorMin        = new Vector2(0, 0);
        currRect.anchorMax        = new Vector2(1, 0);
        currRect.pivot            = new Vector2(0.5f, 0);
        currRect.anchoredPosition = new Vector2(0, CurrPadB);
        currRect.sizeDelta        = new Vector2(-16, CurrencyH);
        var currTMP = currObj.AddComponent<TextMeshProUGUI>();
        currTMP.fontSize  = 12;
        currTMP.color     = new Color(1f, 0.85f, 0.3f);
        currTMP.alignment = TextAlignmentOptions.Center;

        // ── Cursor item — follows mouse while player is holding an item ─────────
        // Parented to canvas root; lives above panel in draw order
        var cursorObj  = new GameObject("CursorItem");
        cursorObj.transform.SetParent(canvasObj.transform, false);
        var cursorRect = cursorObj.AddComponent<RectTransform>();
        cursorRect.anchorMin = Vector2.zero;
        cursorRect.anchorMax = Vector2.zero;
        cursorRect.pivot     = new Vector2(0.5f, 0.5f);
        cursorRect.sizeDelta = new Vector2(TileW, TileH);
        var cursorImg = cursorObj.AddComponent<Image>();
        cursorImg.color         = new Color(0.40f, 0.36f, 0.10f, 0.92f);
        cursorImg.raycastTarget = false;
        cursorObj.SetActive(false);

        var ctObj  = new GameObject("CursorText");
        ctObj.transform.SetParent(cursorObj.transform, false);
        var ctRect = ctObj.AddComponent<RectTransform>();
        ctRect.anchorMin = Vector2.zero;
        ctRect.anchorMax = Vector2.one;
        ctRect.offsetMin = new Vector2(5, 4);
        ctRect.offsetMax = new Vector2(-5, -4);
        var cursorTMP = ctObj.AddComponent<TextMeshProUGUI>();
        cursorTMP.fontSize      = 13;
        cursorTMP.color         = Color.white;
        cursorTMP.alignment     = TextAlignmentOptions.Center;
        cursorTMP.raycastTarget = false;

        // ── Wire InventoryUI ───────────────────────────────────────────────────
        var invUI = canvasObj.AddComponent<InventoryUI>();
        var so    = new SerializedObject(invUI);
        so.FindProperty("panel").objectReferenceValue          = panelObj;
        so.FindProperty("panelRect").objectReferenceValue      = panelRect;
        so.FindProperty("currencyLabel").objectReferenceValue  = currTMP;
        so.FindProperty("cursorItemRect").objectReferenceValue = cursorRect;
        so.FindProperty("cursorItemText").objectReferenceValue = cursorTMP;

        var slotsProp = so.FindProperty("slotTiles");
        slotsProp.arraySize = slotTiles.Length;
        for (int i = 0; i < slotTiles.Length; i++)
            slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = slotTiles[i];

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void CreateEquipmentUI()
    {
        var existing = GameObject.Find("EquipmentCanvas");
        if (existing != null) Object.DestroyImmediate(existing);

        const float TitleH  = 30f;
        const float RowH    = 26f;
        const float RowGap  = 2f;
        const float PadX    = 8f;
        const float PadY    = 8f;
        const float LabelW  = 72f;
        const float BtnW    = 65f;
        const int   RowCount = EquipSlotUtil.Count;

        float panelW = PadX * 2 + LabelW + 84f + BtnW + 8f;
        float panelH = TitleH + PadY + RowCount * RowH + (RowCount - 1) * RowGap + PadY;

        var canvasObj = new GameObject("EquipmentCanvas");
        var canvas    = canvasObj.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 25;
        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasObj.AddComponent<GraphicRaycaster>();

        var panelObj  = new GameObject("EquipmentPanel");
        panelObj.transform.SetParent(canvasObj.transform, false);
        var panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin        = new Vector2(1, 0.5f);
        panelRect.anchorMax        = new Vector2(1, 0.5f);
        panelRect.pivot            = new Vector2(1, 0.5f);
        panelRect.anchoredPosition = new Vector2(-10, 0);
        panelRect.sizeDelta        = new Vector2(panelW, panelH);
        panelObj.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.92f);
        panelObj.SetActive(false);

        // Title / drag bar
        var titleObj  = new GameObject("Title");
        titleObj.transform.SetParent(panelObj.transform, false);
        var titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin        = new Vector2(0, 1);
        titleRect.anchorMax        = new Vector2(1, 1);
        titleRect.pivot            = new Vector2(0.5f, 1);
        titleRect.anchoredPosition = Vector2.zero;
        titleRect.sizeDelta        = new Vector2(0, TitleH);
        titleObj.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        titleObj.AddComponent<UIDrag>().Init(panelRect);

        var tlLabelObj = new GameObject("TitleLabel");
        tlLabelObj.transform.SetParent(titleObj.transform, false);
        var tlRect  = tlLabelObj.AddComponent<RectTransform>();
        tlRect.anchorMin = Vector2.zero;
        tlRect.anchorMax = Vector2.one;
        tlRect.sizeDelta = Vector2.zero;
        var titleTMP = tlLabelObj.AddComponent<TextMeshProUGUI>();
        titleTMP.text          = "Equipment";
        titleTMP.fontSize      = 16;
        titleTMP.color         = Color.white;
        titleTMP.alignment     = TextAlignmentOptions.Center;
        titleTMP.raycastTarget = false;

        var rowComponents = new EquipSlotRowUI[RowCount];

        for (int i = 0; i < RowCount; i++)
        {
            var slot   = (EquipSlot)i;
            float yPos = -(TitleH + PadY + i * (RowH + RowGap));

            var rowObj  = new GameObject(slot.ToString());
            rowObj.transform.SetParent(panelObj.transform, false);
            var rowRect = rowObj.AddComponent<RectTransform>();
            rowRect.anchorMin        = new Vector2(0, 1);
            rowRect.anchorMax        = new Vector2(1, 1);
            rowRect.pivot            = new Vector2(0, 1);
            rowRect.anchoredPosition = new Vector2(PadX, yPos);
            rowRect.sizeDelta        = new Vector2(-PadX * 2, RowH);
            var rowHL = rowObj.AddComponent<HorizontalLayoutGroup>();
            rowHL.childAlignment         = TextAnchor.MiddleLeft;
            rowHL.spacing                = 4;
            rowHL.childControlWidth      = false;
            rowHL.childForceExpandWidth  = false;
            rowHL.childControlHeight     = false;
            rowHL.childForceExpandHeight = false;

            // Slot label
            var slotLblObj = new GameObject("SlotLabel");
            slotLblObj.transform.SetParent(rowObj.transform, false);
            slotLblObj.AddComponent<RectTransform>().sizeDelta = new Vector2(LabelW, RowH);
            var slotLE = slotLblObj.AddComponent<LayoutElement>();
            slotLE.preferredWidth  = LabelW;
            slotLE.preferredHeight = RowH;
            var slotLblTMP = slotLblObj.AddComponent<TextMeshProUGUI>();
            slotLblTMP.text          = slot.DisplayName();
            slotLblTMP.fontSize      = 12;
            slotLblTMP.color         = new Color(0.7f, 0.7f, 0.7f);
            slotLblTMP.alignment     = TextAlignmentOptions.MidlineLeft;
            slotLblTMP.raycastTarget = false;

            // Item label (flexible)
            var itemLblObj = new GameObject("ItemLabel");
            itemLblObj.transform.SetParent(rowObj.transform, false);
            itemLblObj.AddComponent<RectTransform>().sizeDelta = new Vector2(80, RowH);
            var itemLE = itemLblObj.AddComponent<LayoutElement>();
            itemLE.flexibleWidth   = 1;
            itemLE.preferredHeight = RowH;
            var itemLblTMP = itemLblObj.AddComponent<TextMeshProUGUI>();
            itemLblTMP.text          = "---";
            itemLblTMP.fontSize      = 12;
            itemLblTMP.color         = Color.white;
            itemLblTMP.alignment     = TextAlignmentOptions.MidlineLeft;
            itemLblTMP.overflowMode  = TextOverflowModes.Ellipsis;
            itemLblTMP.raycastTarget = false;

            // Unequip button
            var btnObj  = new GameObject("UnequipBtn");
            btnObj.transform.SetParent(rowObj.transform, false);
            btnObj.AddComponent<RectTransform>().sizeDelta = new Vector2(BtnW, RowH);
            var btnLE = btnObj.AddComponent<LayoutElement>();
            btnLE.preferredWidth  = BtnW;
            btnLE.preferredHeight = RowH;
            var btnImg  = btnObj.AddComponent<Image>();
            btnImg.color = new Color(0.35f, 0.2f, 0.2f);
            var btnComp = btnObj.AddComponent<Button>();
            btnComp.targetGraphic = btnImg;

            var btnTxtObj  = new GameObject("Text");
            btnTxtObj.transform.SetParent(btnObj.transform, false);
            var btnTxtRect = btnTxtObj.AddComponent<RectTransform>();
            btnTxtRect.anchorMin = Vector2.zero;
            btnTxtRect.anchorMax = Vector2.one;
            btnTxtRect.sizeDelta = Vector2.zero;
            var btnTMP = btnTxtObj.AddComponent<TextMeshProUGUI>();
            btnTMP.text      = "Unequip";
            btnTMP.fontSize  = 11;
            btnTMP.color     = Color.white;
            btnTMP.alignment = TextAlignmentOptions.Center;

            var rowUI = rowObj.AddComponent<EquipSlotRowUI>();
            var rowSO = new SerializedObject(rowUI);
            rowSO.FindProperty("slotLabel").objectReferenceValue  = slotLblTMP;
            rowSO.FindProperty("itemLabel").objectReferenceValue  = itemLblTMP;
            rowSO.FindProperty("unequipBtn").objectReferenceValue = btnComp;
            rowSO.FindProperty("slotIndex").intValue              = i;
            rowSO.ApplyModifiedPropertiesWithoutUndo();

            rowComponents[i] = rowUI;
        }

        var equipUI = canvasObj.AddComponent<EquipmentUI>();
        var so      = new SerializedObject(equipUI);
        so.FindProperty("panel").objectReferenceValue = panelObj;
        var rowsProp = so.FindProperty("rows");
        rowsProp.arraySize = rowComponents.Length;
        for (int i = 0; i < rowComponents.Length; i++)
            rowsProp.GetArrayElementAtIndex(i).objectReferenceValue = rowComponents[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void CreateLootUI()
    {
        var existing = GameObject.Find("LootCanvas");
        if (existing != null) Object.DestroyImmediate(existing);

        var canvasObj = new GameObject("LootCanvas");
        var canvas    = canvasObj.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30;
        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasObj.AddComponent<GraphicRaycaster>();

        // Panel
        var panelObj  = new GameObject("LootPanel");
        panelObj.transform.SetParent(canvasObj.transform, false);
        var panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin        = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax        = new Vector2(0.5f, 0.5f);
        panelRect.pivot            = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta        = new Vector2(280, 210);
        panelObj.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.92f);
        panelObj.SetActive(false);

        // Title — drag bar moves the panel; label in child to avoid UIDrag + TMP on same object
        var titleObj  = new GameObject("Title");
        titleObj.transform.SetParent(panelObj.transform, false);
        var titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin        = new Vector2(0, 1);
        titleRect.anchorMax        = new Vector2(1, 1);
        titleRect.pivot            = new Vector2(0.5f, 1);
        titleRect.anchoredPosition = Vector2.zero;
        titleRect.sizeDelta        = new Vector2(0, 30);
        titleObj.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        titleObj.AddComponent<UIDrag>().Init(panelRect);

        var titleLabelObj  = new GameObject("TitleLabel");
        titleLabelObj.transform.SetParent(titleObj.transform, false);
        var titleLabelRect = titleLabelObj.AddComponent<RectTransform>();
        titleLabelRect.anchorMin = Vector2.zero;
        titleLabelRect.anchorMax = Vector2.one;
        titleLabelRect.sizeDelta = Vector2.zero;
        var titleTMP = titleLabelObj.AddComponent<TextMeshProUGUI>();
        titleTMP.text          = "Loot";
        titleTMP.fontSize      = 16;
        titleTMP.color         = Color.white;
        titleTMP.alignment     = TextAlignmentOptions.Center;
        titleTMP.raycastTarget = false;

        // Slots container — VerticalLayoutGroup, auto-height via ContentSizeFitter
        var slotsObj  = new GameObject("SlotsContainer");
        slotsObj.transform.SetParent(panelObj.transform, false);
        var slotsRect = slotsObj.AddComponent<RectTransform>();
        slotsRect.anchorMin        = new Vector2(0, 1);
        slotsRect.anchorMax        = new Vector2(1, 1);
        slotsRect.pivot            = new Vector2(0.5f, 1);
        slotsRect.anchoredPosition = new Vector2(0, -34);
        slotsRect.sizeDelta        = new Vector2(-12, 0);
        var slotsVL = slotsObj.AddComponent<VerticalLayoutGroup>();
        slotsVL.spacing              = 3;
        slotsVL.childControlWidth    = true;
        slotsVL.childControlHeight   = true;
        slotsVL.childForceExpandWidth = true;
        var slotsFitter = slotsObj.AddComponent<ContentSizeFitter>();
        slotsFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Coin row
        var coinRowObj  = new GameObject("CoinRow");
        coinRowObj.transform.SetParent(panelObj.transform, false);
        var coinRowRect = coinRowObj.AddComponent<RectTransform>();
        coinRowRect.anchorMin        = new Vector2(0, 0);
        coinRowRect.anchorMax        = new Vector2(1, 0);
        coinRowRect.pivot            = new Vector2(0.5f, 0);
        coinRowRect.anchoredPosition = new Vector2(0, 44);
        coinRowRect.sizeDelta        = new Vector2(-12, 26);
        var coinRowHL = coinRowObj.AddComponent<HorizontalLayoutGroup>();
        coinRowHL.childAlignment         = TextAnchor.MiddleLeft;
        coinRowHL.spacing                = 6;
        coinRowHL.childControlHeight     = false;
        coinRowHL.childForceExpandHeight = false;

        var coinLabelObj = new GameObject("CoinLabel");
        coinLabelObj.transform.SetParent(coinRowObj.transform, false);
        coinLabelObj.AddComponent<RectTransform>().sizeDelta = new Vector2(0, 22);
        var coinLE = coinLabelObj.AddComponent<LayoutElement>();
        coinLE.flexibleWidth   = 1;
        coinLE.preferredHeight = 22;
        var coinTMP = coinLabelObj.AddComponent<TextMeshProUGUI>();
        coinTMP.text              = "No coin";
        coinTMP.fontSize          = 13;
        coinTMP.color             = new Color(1f, 0.85f, 0.3f);
        coinTMP.verticalAlignment = VerticalAlignmentOptions.Middle;

        var takeCoinBtn = MakePanelButton("TakeCoin", "Take Coin", coinRowObj.transform, 80, 22);

        // Loot All button
        var lootAllBtn      = MakePanelButton("LootAll", "Loot All", panelObj.transform, 110, 28);
        var lootAllBtnRect  = lootAllBtn.GetComponent<RectTransform>();
        lootAllBtnRect.anchorMin        = new Vector2(0.5f, 0);
        lootAllBtnRect.anchorMax        = new Vector2(0.5f, 0);
        lootAllBtnRect.pivot            = new Vector2(0.5f, 0);
        lootAllBtnRect.anchoredPosition = new Vector2(0, 8);
        lootAllBtnRect.sizeDelta        = new Vector2(110, 28);

        var lootUI = canvasObj.AddComponent<LootUI>();
        var so     = new SerializedObject(lootUI);
        so.FindProperty("panel").objectReferenceValue          = panelObj;
        so.FindProperty("titleLabel").objectReferenceValue     = titleTMP;
        so.FindProperty("slotsContainer").objectReferenceValue = slotsObj.transform;
        so.FindProperty("coinLabel").objectReferenceValue      = coinTMP;
        so.FindProperty("takeCoinButton").objectReferenceValue = takeCoinBtn.GetComponent<Button>();
        so.FindProperty("lootAllButton").objectReferenceValue  = lootAllBtn.GetComponent<Button>();
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static GameObject MakePanelButton(string objName, string label, Transform parent, float w, float h)
    {
        var btnObj  = new GameObject(objName);
        btnObj.transform.SetParent(parent, false);
        var rect    = btnObj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(w, h);
        var img     = btnObj.AddComponent<Image>();
        img.color   = new Color(0.2f, 0.35f, 0.2f);
        var btn     = btnObj.AddComponent<Button>();
        btn.targetGraphic = img;

        var le = btnObj.AddComponent<LayoutElement>();
        le.preferredWidth  = w;
        le.preferredHeight = h;

        var textObj  = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);
        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        var tmp      = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = 13;
        tmp.color     = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;

        return btnObj;
    }

    // ── PlayerCorpse prefab ───────────────────────────────────────────────────

    [MenuItem("Tools/Rebuild PlayerCorpse Prefab (Fresh)")]
    static void RebuildPlayerCorpsePrefab()
    {
#if MIRROR
        var existing = GameObject.Find("PlayerCorpse");
        if (existing != null) Object.DestroyImmediate(existing);

        var corpse = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        corpse.name = "PlayerCorpse";
        corpse.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);

        corpse.AddComponent<NetworkIdentity>();
        corpse.AddComponent<Nameplate>();
        corpse.AddComponent<PlayerCorpse>();

        Selection.activeGameObject = corpse;
        EditorSceneManager.MarkSceneDirty(corpse.scene);
        Debug.Log("[SceneSetup] PlayerCorpse rebuilt. Drag it into Assets/Prefabs/PlayerCorpse.prefab, then run Tools/Patch Player Prefab and Tools/Setup All.");
#endif
    }

    // Registers a prefab asset with the scene's NetworkManager spawnable prefabs list.
    static void RegisterSpawnablePrefab(string assetPath)
    {
#if MIRROR
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[SceneSetup] {assetPath} not found — skipping spawnable registration.");
            return;
        }

        var nm = Object.FindAnyObjectByType<Mirror.NetworkManager>();
        if (nm == null) { Debug.LogWarning("[SceneSetup] No NetworkManager in scene."); return; }

        var so          = new SerializedObject(nm);
        var spawnPrefabs = so.FindProperty("spawnPrefabs");

        for (int i = 0; i < spawnPrefabs.arraySize; i++)
            if (spawnPrefabs.GetArrayElementAtIndex(i).objectReferenceValue == prefab) return;

        spawnPrefabs.arraySize++;
        spawnPrefabs.GetArrayElementAtIndex(spawnPrefabs.arraySize - 1).objectReferenceValue = prefab;
        so.ApplyModifiedProperties();
        Debug.Log($"[SceneSetup] Registered {prefab.name} as spawnable prefab.");
#endif
    }

    // ── Ability registry ──────────────────────────────────────────────────────

    static void CreateAbilityRegistry()
    {
        var existing = GameObject.Find("AbilityRegistry");
        if (existing != null) Object.DestroyImmediate(existing);

        var go = new GameObject("AbilityRegistry");
        go.AddComponent<AbilityRegistry>();
    }

    // ── Hotbar UI ─────────────────────────────────────────────────────────────

    static void CreateHotbarUI()
    {
        var existing = GameObject.Find("HotbarCanvas");
        if (existing != null) Object.DestroyImmediate(existing);

        const float SlotW   = 80f;
        const float SlotH   = 60f;
        const float SlotGap = 4f;
        const float PadX    = 8f;
        const float PadY    = 8f;
        int count = PlayerAbilities.HotbarSize;

        float panelW = PadX * 2 + count * SlotW + (count - 1) * SlotGap;
        float panelH = PadY * 2 + SlotH;

        var canvasObj = new GameObject("HotbarCanvas");
        var canvas    = canvasObj.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 12;
        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasObj.AddComponent<GraphicRaycaster>();

        // Container panel — bottom-center
        var panelObj  = new GameObject("HotbarPanel");
        panelObj.transform.SetParent(canvasObj.transform, false);
        var panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin        = new Vector2(0.5f, 0f);
        panelRect.anchorMax        = new Vector2(0.5f, 0f);
        panelRect.pivot            = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, PadY);
        panelRect.sizeDelta        = new Vector2(panelW, panelH);
        panelObj.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.35f);

        var slotUIs = new HotbarSlotUI[count];

        for (int i = 0; i < count; i++)
        {
            float xPos = PadX + i * (SlotW + SlotGap);

            var slotObj  = new GameObject($"Slot{i + 1}");
            slotObj.transform.SetParent(panelObj.transform, false);
            var slotRect = slotObj.AddComponent<RectTransform>();
            slotRect.anchorMin        = new Vector2(0f, 0f);
            slotRect.anchorMax        = new Vector2(0f, 0f);
            slotRect.pivot            = new Vector2(0f, 0f);
            slotRect.anchoredPosition = new Vector2(xPos, PadY);
            slotRect.sizeDelta        = new Vector2(SlotW, SlotH);
            slotObj.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f);

            // Cooldown overlay — sits over the entire slot
            var cdOverlayObj  = new GameObject("CDOverlay");
            cdOverlayObj.transform.SetParent(slotObj.transform, false);
            var cdOverlayRect = cdOverlayObj.AddComponent<RectTransform>();
            cdOverlayRect.anchorMin = Vector2.zero;
            cdOverlayRect.anchorMax = Vector2.one;
            cdOverlayRect.sizeDelta = Vector2.zero;
            var cdOverlayImg  = cdOverlayObj.AddComponent<Image>();
            cdOverlayImg.color        = new Color(0f, 0f, 0f, 0f);
            cdOverlayImg.raycastTarget = false;

            // Ability name — upper area
            var nameObj  = new GameObject("Name");
            nameObj.transform.SetParent(slotObj.transform, false);
            var nameRect = nameObj.AddComponent<RectTransform>();
            nameRect.anchorMin        = new Vector2(0f, 0.4f);
            nameRect.anchorMax        = new Vector2(1f, 1f);
            nameRect.offsetMin        = new Vector2(4f, 0f);
            nameRect.offsetMax        = new Vector2(-4f, -4f);
            var nameTMP = nameObj.AddComponent<TextMeshProUGUI>();
            nameTMP.fontSize      = 12;
            nameTMP.color         = Color.white;
            nameTMP.alignment     = TextAlignmentOptions.Center;
            nameTMP.overflowMode  = TextOverflowModes.Ellipsis;
            nameTMP.raycastTarget = false;

            // Cooldown countdown — lower area
            var cdObj  = new GameObject("CDText");
            cdObj.transform.SetParent(slotObj.transform, false);
            var cdRect = cdObj.AddComponent<RectTransform>();
            cdRect.anchorMin        = new Vector2(0f, 0f);
            cdRect.anchorMax        = new Vector2(1f, 0.4f);
            cdRect.offsetMin        = new Vector2(4f, 2f);
            cdRect.offsetMax        = new Vector2(-4f, 0f);
            var cdTMP = cdObj.AddComponent<TextMeshProUGUI>();
            cdTMP.fontSize      = 11;
            cdTMP.color         = new Color(1f, 0.85f, 0.3f);
            cdTMP.alignment     = TextAlignmentOptions.Center;
            cdTMP.raycastTarget = false;

            var slotUI = slotObj.AddComponent<HotbarSlotUI>();
            slotUI.Init(nameTMP, cdTMP, cdOverlayImg); // direct wiring — robust vs. SerializedObject-in-loop

            slotUIs[i] = slotUI;
        }

        var hotbarUI = canvasObj.AddComponent<HotbarUI>();
        var so       = new SerializedObject(hotbarUI);
        var slotsProp = so.FindProperty("slots");
        slotsProp.arraySize = slotUIs.Length;
        for (int i = 0; i < slotUIs.Length; i++)
            slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = slotUIs[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ── Vendor UI ─────────────────────────────────────────────────────────────

    static void CreateVendorUI()
    {
        var existing = GameObject.Find("VendorCanvas");
        if (existing != null) Object.DestroyImmediate(existing);

        const float TitleH   = 30f;
        const float TabH     = 28f;
        const float CurrencyH = 22f;
        const float PadB      = 8f;
        const float PanelW   = 320f;
        const float PanelH   = 420f;
        const float ScrollH  = PanelH - TitleH - TabH - 4f - CurrencyH - PadB * 2f;

        var canvasObj = new GameObject("VendorCanvas");
        var canvas    = canvasObj.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30;
        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasObj.AddComponent<GraphicRaycaster>();

        // Panel
        var panelObj  = new GameObject("VendorPanel");
        panelObj.transform.SetParent(canvasObj.transform, false);
        var panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin        = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax        = new Vector2(0.5f, 0.5f);
        panelRect.pivot            = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta        = new Vector2(PanelW, PanelH);
        panelObj.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.92f);
        panelObj.SetActive(false);

        // Title drag bar
        var titleObj  = new GameObject("Title");
        titleObj.transform.SetParent(panelObj.transform, false);
        var titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin        = new Vector2(0, 1);
        titleRect.anchorMax        = new Vector2(1, 1);
        titleRect.pivot            = new Vector2(0.5f, 1);
        titleRect.anchoredPosition = Vector2.zero;
        titleRect.sizeDelta        = new Vector2(0, TitleH);
        titleObj.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        titleObj.AddComponent<UIDrag>().Init(panelRect);

        var tlLblObj = new GameObject("TitleLabel");
        tlLblObj.transform.SetParent(titleObj.transform, false);
        var tlRect = tlLblObj.AddComponent<RectTransform>();
        tlRect.anchorMin = Vector2.zero;
        tlRect.anchorMax = Vector2.one;
        tlRect.sizeDelta = Vector2.zero;
        var titleTMP = tlLblObj.AddComponent<TextMeshProUGUI>();
        titleTMP.text          = "Vendor";
        titleTMP.fontSize      = 16;
        titleTMP.color         = Color.white;
        titleTMP.alignment     = TextAlignmentOptions.Center;
        titleTMP.raycastTarget = false;

        // Tab bar
        var tabBarObj  = new GameObject("TabBar");
        tabBarObj.transform.SetParent(panelObj.transform, false);
        var tabBarRect = tabBarObj.AddComponent<RectTransform>();
        tabBarRect.anchorMin        = new Vector2(0, 1);
        tabBarRect.anchorMax        = new Vector2(1, 1);
        tabBarRect.pivot            = new Vector2(0.5f, 1);
        tabBarRect.anchoredPosition = new Vector2(0, -TitleH);
        tabBarRect.sizeDelta        = new Vector2(0, TabH);
        var tabHLG = tabBarObj.AddComponent<HorizontalLayoutGroup>();
        tabHLG.childControlWidth      = true;
        tabHLG.childForceExpandWidth  = true;
        tabHLG.childControlHeight     = true;
        tabHLG.childForceExpandHeight = true;
        tabHLG.spacing                = 2;

        var buyTabBtn  = MakeTabButton(tabBarObj.transform, "Wares", new Color(0.18f, 0.38f, 0.18f));
        var sellTabBtn = MakeTabButton(tabBarObj.transform, "Sell",  new Color(0.2f, 0.2f, 0.2f));

        // Scroll rect
        var scrollObj  = new GameObject("Scroll");
        scrollObj.transform.SetParent(panelObj.transform, false);
        var scrollRect = scrollObj.AddComponent<RectTransform>();
        scrollRect.anchorMin        = new Vector2(0, 1);
        scrollRect.anchorMax        = new Vector2(1, 1);
        scrollRect.pivot            = new Vector2(0.5f, 1);
        scrollRect.anchoredPosition = new Vector2(0, -(TitleH + TabH + 4f));
        scrollRect.sizeDelta        = new Vector2(-8, ScrollH);
        scrollObj.AddComponent<Image>().color = new Color(0.07f, 0.07f, 0.07f);
        var sr = scrollObj.AddComponent<ScrollRect>();
        sr.horizontal = false;

        var viewportObj  = new GameObject("Viewport");
        viewportObj.transform.SetParent(scrollObj.transform, false);
        var viewportRect = viewportObj.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.sizeDelta = Vector2.zero;
        var mask = viewportObj.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        viewportObj.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f);

        var contentObj  = new GameObject("Content");
        contentObj.transform.SetParent(viewportObj.transform, false);
        var contentRect = contentObj.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot     = new Vector2(0.5f, 1);
        contentRect.sizeDelta = new Vector2(0, 0);
        var vlg = contentObj.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth      = true;
        vlg.childForceExpandWidth  = true;
        vlg.childControlHeight     = false;
        vlg.childForceExpandHeight = false;
        vlg.spacing                = 2;
        vlg.padding                = new RectOffset(4, 4, 4, 4);
        var csf = contentObj.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        sr.content  = contentRect;
        sr.viewport = viewportRect;

        // Currency label
        var currObj  = new GameObject("Currency");
        currObj.transform.SetParent(panelObj.transform, false);
        var currRect = currObj.AddComponent<RectTransform>();
        currRect.anchorMin        = new Vector2(0, 0);
        currRect.anchorMax        = new Vector2(1, 0);
        currRect.pivot            = new Vector2(0.5f, 0);
        currRect.anchoredPosition = new Vector2(0, PadB);
        currRect.sizeDelta        = new Vector2(-16, CurrencyH);
        var currTMP = currObj.AddComponent<TextMeshProUGUI>();
        currTMP.fontSize  = 12;
        currTMP.color     = new Color(1f, 0.85f, 0.3f);
        currTMP.alignment = TextAlignmentOptions.Center;

        // Wire VendorUI
        var vendorUI = canvasObj.AddComponent<VendorUI>();
        var so       = new SerializedObject(vendorUI);
        so.FindProperty("panel").objectReferenceValue         = panelObj;
        so.FindProperty("content").objectReferenceValue       = contentRect;
        so.FindProperty("currencyLabel").objectReferenceValue = currTMP;
        so.FindProperty("buyTabBtn").objectReferenceValue     = buyTabBtn;
        so.FindProperty("sellTabBtn").objectReferenceValue    = sellTabBtn;
        so.ApplyModifiedPropertiesWithoutUndo();

        buyTabBtn.onClick.AddListener(vendorUI.OnBuyTabClicked);
        sellTabBtn.onClick.AddListener(vendorUI.OnSellTabClicked);
    }

    static Button MakeTabButton(Transform parent, string label, Color bg)
    {
        var obj = new GameObject(label + "Tab");
        obj.transform.SetParent(parent, false);
        var img = obj.AddComponent<Image>();
        img.color = bg;
        var btn = obj.AddComponent<Button>();
        btn.targetGraphic = img;
        var txtObj  = new GameObject("Text");
        txtObj.transform.SetParent(obj.transform, false);
        var txtRect = txtObj.AddComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.sizeDelta = Vector2.zero;
        var tmp = txtObj.AddComponent<TextMeshProUGUI>();
        tmp.text          = label;
        tmp.fontSize      = 13;
        tmp.color         = Color.white;
        tmp.alignment     = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        return btn;
    }

    // ── Vendor Inventory asset helper ─────────────────────────────────────────

    [MenuItem("Tools/Editor/Create Vendor Inventory")]
    static void CreateVendorInventoryAsset()
    {
        EnsureVendorResourcesFolder();
        var inv  = ScriptableObject.CreateInstance<VendorInventory>();
        var path = AssetDatabase.GenerateUniqueAssetPath("Assets/Resources/Vendors/New Vendor Inventory.asset");
        AssetDatabase.CreateAsset(inv, path);
        AssetDatabase.SaveAssets();
        Selection.activeObject = inv;
        EditorGUIUtility.PingObject(inv);
        Debug.Log($"[SceneSetup] Created vendor inventory at {path}");
    }

    static void EnsureVendorResourcesFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Vendors"))
            AssetDatabase.CreateFolder("Assets/Resources", "Vendors");
    }

    // ── Utilities ─────────────────────────────────────────────────────────────

    static void EnsureEventSystem()
    {
#if UNITY_2023_1_OR_NEWER
        if (Object.FindAnyObjectByType<EventSystem>() != null) return;
#else
        if (Object.FindObjectOfType<EventSystem>() != null) return;
#endif
        var esObj = new GameObject("EventSystem");
        esObj.AddComponent<EventSystem>();

        var moduleType = FindType("UnityEngine.InputSystem.UI.InputSystemUIInputModule")
                      ?? FindType("UnityEngine.EventSystems.StandaloneInputModule");
        if (moduleType != null)
            esObj.AddComponent(moduleType);
        else
            Debug.LogWarning("[SceneSetup] No UI input module found — InputField may not receive keyboard input.");
    }

    static void EnsureDirectionalLight()
    {
#if UNITY_2023_1_OR_NEWER
        if (Object.FindAnyObjectByType<Light>() != null) return;
#else
        if (Object.FindObjectOfType<Light>() != null) return;
#endif
        var lightObj = new GameObject("Directional Light");
        var l = lightObj.AddComponent<Light>();
        l.type = LightType.Directional;
        l.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    }

    static System.Type FindType(string fullName)
    {
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType(fullName);
            if (t != null) return t;
        }
        return null;
    }
}
