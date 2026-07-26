using System.IO.Compression;
using System.Net.Http;

namespace Ueq.Launcher;

/// <summary>
/// 6.4 (BP6, option 1 — a real but right-sized launcher, not a full delta patcher). Checks a tiny
/// <c>version.txt</c> against a locally installed marker; if different, re-downloads the whole
/// game zip and extracts over the previous install; then launches it. No self-update for the
/// launcher itself — its logic is meant to stay simple and stable enough to rarely need touching.
/// </summary>
public class MainForm : Form
{
    // 6.4 (BP1) — same host the web app / API / game server already live on.
    const string BaseUrl = "https://18-218-79-193.sslip.io/downloads";

    static readonly string InstallDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UeqGame");
    static readonly string GameDir = Path.Combine(InstallDir, "Game");
    static readonly string LocalVersionFile = Path.Combine(InstallDir, "version.txt");
    static readonly string GameExePath = Path.Combine(GameDir, "Ueq.exe");

    readonly Label _status = new() { AutoSize = false, TextAlign = ContentAlignment.MiddleCenter };
    readonly ProgressBar _progress = new() { Style = ProgressBarStyle.Continuous };
    readonly Button _retry = new() { Text = "Retry", Visible = false };

    public MainForm()
    {
        Text = "Ueq Launcher";
        ClientSize = new Size(420, 120);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;

        _status.SetBounds(10, 15, 400, 40);
        _progress.SetBounds(10, 60, 400, 23);
        _retry.SetBounds(160, 90, 100, 25);
        _retry.Click += (_, _) => _ = RunAsync();

        Controls.Add(_status);
        Controls.Add(_progress);
        Controls.Add(_retry);

        Load += (_, _) => _ = RunAsync();
    }

    async Task RunAsync()
    {
        _retry.Visible = false;
        Directory.CreateDirectory(InstallDir);

        string? remoteVersion = null;
        try
        {
            SetStatus("Checking for updates...");
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            remoteVersion = (await http.GetStringAsync($"{BaseUrl}/version.txt")).Trim();
        }
        catch (Exception ex)
        {
            // Offline-tolerant: if we already have a working copy, let them play anyway rather
            // than blocking on a transient network hiccup. Only a hard failure if nothing's
            // installed yet — there's genuinely nothing to launch.
            if (File.Exists(GameExePath))
            {
                SetStatus($"Couldn't check for updates ({ex.Message}). Launching existing copy...");
                await Task.Delay(1500);
                Launch();
                return;
            }
            SetStatus($"Couldn't reach the update server: {ex.Message}");
            _retry.Visible = true;
            return;
        }

        string localVersion = File.Exists(LocalVersionFile) ? File.ReadAllText(LocalVersionFile).Trim() : "";
        bool needsUpdate = remoteVersion != localVersion || !File.Exists(GameExePath);

        if (needsUpdate)
        {
            try
            {
                await DownloadAndInstallAsync(remoteVersion);
            }
            catch (Exception ex)
            {
                SetStatus($"Update failed: {ex.Message}");
                _retry.Visible = true;
                return;
            }
        }

        SetStatus("Launching...");
        await Task.Delay(300);
        Launch();
    }

    async Task DownloadAndInstallAsync(string version)
    {
        SetStatus("Downloading update...");
        _progress.Style = ProgressBarStyle.Blocks;

        string tempZip = Path.Combine(Path.GetTempPath(), $"UeqClient-{Guid.NewGuid():N}.zip");
        using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) })
        using (var response = await http.GetAsync($"{BaseUrl}/UeqClient.zip", HttpCompletionOption.ResponseHeadersRead))
        {
            response.EnsureSuccessStatusCode();
            long? total = response.Content.Headers.ContentLength;
            await using var source = await response.Content.ReadAsStreamAsync();
            await using var dest = File.Create(tempZip);

            var buffer = new byte[81920];
            long copied = 0;
            int read;
            while ((read = await source.ReadAsync(buffer)) > 0)
            {
                await dest.WriteAsync(buffer.AsMemory(0, read));
                copied += read;
                if (total.HasValue)
                    _progress.Value = (int)Math.Clamp(copied * 100 / total.Value, 0, 100);
            }
        }

        SetStatus("Installing...");
        _progress.Style = ProgressBarStyle.Marquee;

        if (Directory.Exists(GameDir))
            Directory.Delete(GameDir, recursive: true);
        Directory.CreateDirectory(GameDir);
        ZipFile.ExtractToDirectory(tempZip, GameDir, overwriteFiles: true);
        File.Delete(tempZip);

        File.WriteAllText(LocalVersionFile, version);
    }

    void Launch()
    {
        if (!File.Exists(GameExePath))
        {
            SetStatus("Install looks incomplete — Ueq.exe not found after extracting.");
            _retry.Visible = true;
            return;
        }
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(GameExePath)
        {
            WorkingDirectory = GameDir,
        });
        Close();
    }

    void SetStatus(string text) => _status.Text = text;
}
