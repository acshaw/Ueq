using System.IO.Compression;
using System.Net.Http;

namespace Ueq.Launcher;

/// <summary>
/// 6.4 (BP6, option 1 — a real but right-sized launcher, not a full delta patcher). Checks the
/// served game zip's own <c>ETag</c>/<c>Last-Modified</c> (via a HEAD request — Caddy's
/// <c>file_server</c> sets these automatically for any static file, no extra deploy step needed)
/// against a locally installed marker; if different, re-downloads the whole game zip and extracts
/// over the previous install; then launches it. No self-update for the launcher itself — its logic
/// is meant to stay simple and stable enough to rarely need touching.
///
/// Deliberately NOT keyed off <c>BuildInfo</c>'s stamped build id (the one
/// <c>AccountAuthenticator</c> checks at login) — that id is only bumped for real client/server
/// *compatibility* changes (see <c>Tools/Build/Stamp New Build Id</c>'s own doc comment), so a
/// routine client-only rebuild (a UI tweak, a bugfix) would never change it and this launcher would
/// never notice a new build existed. The served file's own fingerprint changes on every rebuild,
/// automatically, with no separate "remember to bump something" step.
/// </summary>
public class MainForm : Form
{
    // 6.4 (BP1) — same host the web app / API / game server already live on.
    const string BaseUrl = "https://18-218-79-193.sslip.io/downloads";

    static readonly string InstallDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UeqGame");
    static readonly string GameDir = Path.Combine(InstallDir, "Game");
    static readonly string LocalFingerprintFile = Path.Combine(InstallDir, "installed.fingerprint");
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

        string? remoteFingerprint;
        try
        {
            SetStatus("Checking for updates...");
            remoteFingerprint = await GetRemoteFingerprintAsync();
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

        string localFingerprint = File.Exists(LocalFingerprintFile) ? File.ReadAllText(LocalFingerprintFile).Trim() : "";
        bool needsUpdate = remoteFingerprint != localFingerprint || !File.Exists(GameExePath);

        if (needsUpdate)
        {
            try
            {
                await DownloadAndInstallAsync(remoteFingerprint);
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

    // A HEAD request costs nothing to run every launch and never downloads the ~100+ MB zip just to
    // check it. Prefers ETag (content-addressed — changes iff the bytes changed); falls back to
    // Last-Modified, then Content-Length, so this still works even if a future web server config
    // doesn't emit ETags for some reason.
    static async Task<string> GetRemoteFingerprintAsync()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        using var request = new HttpRequestMessage(HttpMethod.Head, $"{BaseUrl}/UeqClient.zip");
        using var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        if (response.Headers.ETag is { } etag) return "etag:" + etag.Tag;
        if (response.Content.Headers.LastModified is { } modified) return "modified:" + modified.ToUnixTimeSeconds();
        if (response.Content.Headers.ContentLength is { } length) return "length:" + length;
        throw new InvalidOperationException("Server response had no ETag, Last-Modified, or Content-Length to compare.");
    }

    async Task DownloadAndInstallAsync(string fingerprint)
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

        File.WriteAllText(LocalFingerprintFile, fingerprint);
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
