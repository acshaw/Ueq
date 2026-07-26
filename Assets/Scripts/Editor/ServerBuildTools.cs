using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 5.10 — Dedicated server build spike. Builds a headless Standalone server and a normal
/// windowed Standalone client from the same enabled scene list, to known short paths, so a
/// fresh build can be produced repeatably instead of hand-driving File > Build Settings.
/// 6.2 — added a Linux Dedicated Server build target for the real AWS deploy (DH1/DH2).
/// </summary>
public static class ServerBuildTools
{
    const string ServerOutputDir = @"C:\Builds\Ueq\Server";
    const string ClientOutputDir = @"C:\Builds\Ueq\Client";
    const string LinuxServerOutputDir = @"C:\Builds\Ueq\ServerLinux";

    [MenuItem("Tools/Build/Build Headless Server (Standalone)")]
    static void BuildHeadlessServer()
    {
        var scenes = EnabledScenePaths();
        if (scenes.Length == 0)
        {
            Debug.LogError("[ServerBuild] No enabled scenes in Build Settings — nothing to build.");
            return;
        }

        if (Directory.Exists(ServerOutputDir))
        {
            Directory.Delete(ServerOutputDir, true);
            Debug.Log($"[ServerBuild] Deleted existing output dir: {ServerOutputDir}");
        }
        Directory.CreateDirectory(ServerOutputDir);

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

        if (Directory.Exists(LinuxServerOutputDir))
        {
            Directory.Delete(LinuxServerOutputDir, true);
            Debug.Log($"[ServerBuild] Deleted existing output dir: {LinuxServerOutputDir}");
        }
        Directory.CreateDirectory(LinuxServerOutputDir);

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

        if (Directory.Exists(ClientOutputDir))
        {
            Directory.Delete(ClientOutputDir, true);
            Debug.Log($"[ServerBuild] Deleted existing output dir: {ClientOutputDir}");
        }
        Directory.CreateDirectory(ClientOutputDir);

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
