using UnityEditor;
using UnityEngine;
#if MIRROR
using Mirror;
#endif

public static class SceneSetup
{
    [MenuItem("Tools/Setup Player Scene")]
    static void SetupPlayerScene()
    {
        foreach (string name in new[] { "Ground", "Obstacles", "Player", "NetworkManager" })
        {
            var existing = GameObject.Find(name);
            if (existing != null) Object.DestroyImmediate(existing);
        }

        CreateGround();
        CreateObstacles();
        var player = CreatePlayer();
        SetupCamera(player);
        CreateNetworkManager(player);
        EnsureDirectionalLight();

        Selection.activeGameObject = player;
        if (SceneView.lastActiveSceneView != null)
            SceneView.lastActiveSceneView.FrameSelected();

#if MIRROR
        Debug.Log("Scene setup complete (Mirror). Next: drag Player into Assets/Prefabs/, then assign it to NetworkManager > Player Prefab.");
#else
        Debug.LogWarning("Scene setup complete (no Mirror). Install Mirror from the Asset Store, then re-run Tools/Setup Player Scene to add network components.");
#endif
    }

    static void CreateGround()
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(10, 1, 10);
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

    static GameObject CreatePlayer()
    {
        var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "Player";
        player.transform.position = new Vector3(0, 1.1f, -5f);
        player.tag = "Player";

        Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());

        var cc = player.AddComponent<CharacterController>();
        cc.height = 2f;
        cc.radius = 0.5f;
        cc.center = Vector3.zero;

#if MIRROR
        player.AddComponent<NetworkIdentity>();

        // Mirror renamed NetworkTransform → NetworkTransformReliable in newer versions
        var ntType = FindType("Mirror.NetworkTransformReliable") ?? FindType("Mirror.NetworkTransform");
        if (ntType != null)
        {
            var nt = player.AddComponent(ntType);
            // Only sync position — rotation is client-side for instant camera response
            var syncRotField = ntType.GetField("syncRotation");
            syncRotField?.SetValue(nt, false);
        }
        else Debug.LogError("Could not find a NetworkTransform type — check your Mirror version.");

        var np = player.AddComponent<NetworkedPlayer>();
        player.AddComponent<Health>();

        var so = new SerializedObject(np);
        so.FindProperty("cameraHolder").objectReferenceValue = CreateCameraHolder(player);
        so.ApplyModifiedPropertiesWithoutUndo();
#else
        // Mirror not installed — add standalone PlayerController for local testing
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

        Camera mainCam = Camera.main;
        GameObject camObj = mainCam != null ? mainCam.gameObject : new GameObject("Main Camera");
        if (camObj.GetComponent<Camera>() == null) camObj.AddComponent<Camera>();
        camObj.tag = "MainCamera";

#if MIRROR
        // Starts inactive; NetworkedPlayer.OnStartLocalPlayer activates it for the local player only
        camObj.SetActive(false);
#endif

        camObj.transform.SetParent(camHolder);
        camObj.transform.localPosition = Vector3.zero;
        camObj.transform.localRotation = Quaternion.identity;
    }

    static void CreateNetworkManager(GameObject player)
    {
#if MIRROR
        var nmObj = new GameObject("NetworkManager");
        var nm = nmObj.AddComponent<GameNetworkManager>();
        nmObj.AddComponent<NetworkManagerHUD>();

        // KcpTransport (UDP) is Mirror's default. Namespace varies by Mirror version.
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

        if (transport != null)
        {
            var so = new SerializedObject(nm);
            so.FindProperty("transport").objectReferenceValue = transport;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            Debug.LogError("No Mirror transport found. Add KcpTransport to the NetworkManager manually.");
        }

        var spawnPoint = new GameObject("SpawnPoint");
        spawnPoint.transform.position = player.transform.position;
        spawnPoint.AddComponent<NetworkStartPosition>();
#endif
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

    static void EnsureDirectionalLight()
    {
#if UNITY_2023_1_OR_NEWER
        if (Object.FindFirstObjectByType<Light>() != null) return;
#else
        if (Object.FindObjectOfType<Light>() != null) return;
#endif
        var lightObj = new GameObject("Directional Light");
        var l = lightObj.AddComponent<Light>();
        l.type = LightType.Directional;
        l.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    }
}
