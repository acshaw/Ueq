using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 5.10 — Dedicated server build spike. Builds a headless Standalone server and a normal
/// windowed Standalone client from the same enabled scene list, to known short paths, so a
/// fresh build can be produced repeatably instead of hand-driving File > Build Settings.
/// 6.2 — added a Linux Dedicated Server build target for the real AWS deploy (DH1/DH2).
/// 6.4 — added build-id stamping (BP4) so client/server pairs can detect a mismatch at connect.
/// </summary>
public static class ServerBuildTools
{
    const string ServerOutputDir = @"C:\Builds\Ueq\Server";
    const string ClientOutputDir = @"C:\Builds\Ueq\Client";
    const string LinuxServerOutputDir = @"C:\Builds\Ueq\ServerLinux";
    const string BuildsRootDir = @"C:\Builds\Ueq";
    const string BuildInfoAssetPath = "Assets/Resources/BuildInfo.asset";

    // 6.4 (BP4) — one id per release, not one per build-method call. Stamping is a deliberate,
    // separate step so building the client and the server independently (in either order) for the
    // same release still embeds the identical id — re-stamping inside each build method would give
    // every individual build its own timestamp and make the mismatch check always fire.
    [MenuItem("Tools/Build/Stamp New Build Id")]
    static void StampNewBuildId()
    {
        Directory.CreateDirectory(BuildsRootDir);
        string id = System.DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'");

        var info = AssetDatabase.LoadAssetAtPath<BuildInfo>(BuildInfoAssetPath);
        if (info == null)
        {
            info = ScriptableObject.CreateInstance<BuildInfo>();
            AssetDatabase.CreateAsset(info, BuildInfoAssetPath);
        }
        info.buildId = id;
        EditorUtility.SetDirty(info);
        AssetDatabase.SaveAssets();

        // Plain-text sibling artifact — the standalone launcher (6.4) checks this over HTTP without
        // ever running Unity code, so it can't read the Resources asset directly.
        File.WriteAllText(Path.Combine(BuildsRootDir, "version.txt"), id);

        Debug.Log($"[ServerBuild] Stamped new build id: {id}. Now build whichever of client/server " +
            "changed — both will embed this id until the next stamp.");
    }

    // Auto-stamps on first use so a from-scratch clone doesn't silently ship with an empty build id
    // forever (an empty id disables the mismatch check entirely, rather than enforcing a wrong one —
    // safe by default — but it's better to actually have one from the start).
    static void EnsureBuildInfoStamped()
    {
        if (AssetDatabase.LoadAssetAtPath<BuildInfo>(BuildInfoAssetPath) == null)
        {
            Debug.LogWarning("[ServerBuild] No BuildInfo asset yet — stamping one now. Re-stamp " +
                "explicitly (Tools/Build/Stamp New Build Id) before cutting each new release.");
            StampNewBuildId();
        }
    }

    [MenuItem("Tools/Build/Build Headless Server (Standalone)")]
    static void BuildHeadlessServer()
    {
        var scenes = EnabledScenePaths();
        if (scenes.Length == 0)
        {
            Debug.LogError("[ServerBuild] No enabled scenes in Build Settings — nothing to build.");
            return;
        }
        EnsureBuildInfoStamped();

        if (Directory.Exists(ServerOutputDir))
        {
            Directory.Delete(ServerOutputDir, true);
            Debug.Log($"[ServerBuild] Deleted existing output dir: {ServerOutputDir}");
        }
        Directory.CreateDirectory(ServerOutputDir);

        // 6.2 found the hard way: EditorUserBuildSettings.standaloneBuildSubtarget is a global,
        // cross-session-persisted setting — building the Linux Dedicated Server (subtarget=Server)
        // leaves it stuck on Server, and it silently overrides BuildPlayerOptions.subtarget's
        // default (Player) when the OS target changes but the "Standalone" platform group doesn't.
        // Reset explicitly so every build method here is self-contained, not order-dependent.
        EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Player;

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = Path.Combine(ServerOutputDir, "Ueq.exe"),
            target = BuildTarget.StandaloneWindows64,
            // DS2: Library/PlayerDataCache + Library/Bee cache intermediate player data across
            // builds for speed — deleting only the output folder doesn't invalidate that cache,
            // so a "fresh" build can still be built from stale (possibly corrupt) cached data.
            // CleanBuildCache forces every asset/scene to be genuinely reprocessed.
            options = BuildOptions.CleanBuildCache,
        };

        Debug.Log($"[ServerBuild] Building server → {options.locationPathName} ({scenes.Length} scene(s))");
        var report = BuildPipeline.BuildPlayer(options);
        Debug.Log($"[ServerBuild] Result: {report.summary.result}, size {report.summary.totalSize} bytes, " +
                   $"{report.summary.totalErrors} error(s), {report.summary.totalWarnings} warning(s)");

        if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.Log("[ServerBuild] Launch with, e.g.:\n" +
                $"  \"{options.locationPathName}\" -batchmode -nographics -logFile \"{ServerOutputDir}\\server.log\"\n" +
                "  (set UEQ_DB_CONNSTRING or UEQ_DB_HOST/PORT/NAME/USER/PASSWORD env vars first — see DS4)");
        }
    }

    [MenuItem("Tools/Build/Build Linux Dedicated Server")]
    static void BuildLinuxDedicatedServer()
    {
        var scenes = EnabledScenePaths();
        if (scenes.Length == 0)
        {
            Debug.LogError("[ServerBuild] No enabled scenes in Build Settings — nothing to build.");
            return;
        }
        EnsureBuildInfoStamped();

        if (Directory.Exists(LinuxServerOutputDir))
        {
            Directory.Delete(LinuxServerOutputDir, true);
            Debug.Log($"[ServerBuild] Deleted existing output dir: {LinuxServerOutputDir}");
        }
        Directory.CreateDirectory(LinuxServerOutputDir);

        // See BuildHeadlessServer's comment — explicit, not relying on whatever subtarget a
        // previous build call left active.
        EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Server;

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = Path.Combine(LinuxServerOutputDir, "Ueq.x86_64"),
            target = BuildTarget.StandaloneLinux64,
            // DH2: Unity's purpose-built Dedicated Server subtarget — strips graphics/audio/input
            // modules the Standalone+-nographics workaround (5.10) still carried around.
            subtarget = (int)StandaloneBuildSubtarget.Server,
            // See BuildHeadlessServer's comment (DS2) — force genuinely fresh player data.
            options = BuildOptions.CleanBuildCache,
        };

        Debug.Log($"[ServerBuild] Building Linux Dedicated Server → {options.locationPathName} ({scenes.Length} scene(s))");
        var report = BuildPipeline.BuildPlayer(options);
        Debug.Log($"[ServerBuild] Result: {report.summary.result}, size {report.summary.totalSize} bytes, " +
                   $"{report.summary.totalErrors} error(s), {report.summary.totalWarnings} warning(s)");

        if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.Log("[ServerBuild] Upload the whole output folder (binary + _Data) to the Lightsail box, " +
                "chmod +x the binary, then run with, e.g.:\n" +
                $"  ./Ueq.x86_64 -batchmode -nographics -logFile -\n" +
                "  (set UEQ_DB_HOST/PORT/NAME/USER/PASSWORD env vars first — see DH4/ueq-gameserver.service)");
        }
    }

    [MenuItem("Tools/Build/Build Standalone Client")]
    static void BuildStandaloneClient()
    {
        var scenes = EnabledScenePaths();
        if (scenes.Length == 0)
        {
            Debug.LogError("[ServerBuild] No enabled scenes in Build Settings — nothing to build.");
            return;
        }
        EnsureBuildInfoStamped();

        if (Directory.Exists(ClientOutputDir))
        {
            Directory.Delete(ClientOutputDir, true);
            Debug.Log($"[ServerBuild] Deleted existing output dir: {ClientOutputDir}");
        }
        Directory.CreateDirectory(ClientOutputDir);

        // See BuildHeadlessServer's comment — explicit, not relying on whatever subtarget a
        // previous build call left active.
        EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Player;

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = Path.Combine(ClientOutputDir, "Ueq.exe"),
            target = BuildTarget.StandaloneWindows64,
            // See BuildHeadlessServer's comment — CleanBuildCache forces genuinely fresh player data.
            options = BuildOptions.CleanBuildCache,
        };

        Debug.Log($"[ServerBuild] Building client → {options.locationPathName} ({scenes.Length} scene(s))");
        var report = BuildPipeline.BuildPlayer(options);
        Debug.Log($"[ServerBuild] Result: {report.summary.result}, size {report.summary.totalSize} bytes, " +
                   $"{report.summary.totalErrors} error(s), {report.summary.totalWarnings} warning(s)");
    }

    static string[] EnabledScenePaths()
    {
        return EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();
    }
}
