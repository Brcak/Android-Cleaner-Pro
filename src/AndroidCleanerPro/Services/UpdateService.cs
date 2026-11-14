using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;

namespace AndroidCleanerPro.Services;

/// <summary>
/// Opt-in, in-place updates from the repository's published GitHub Releases.
/// Downloads are checksum-verified before the standard (visible) installer starts.
/// </summary>
public static class UpdateService
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromMinutes(5) };
    private static int _checking;
    private const string LatestUrl = "https://api.github.com/repos/Brcak/Android-Cleaner-Pro/releases/latest";
    private const string OfficialDownloadPrefix = "https://github.com/Brcak/Android-Cleaner-Pro/releases/download/";
    private const string SetupName = "AndroidCleanerPro-Setup.exe";

    public static string LogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AndroidCleanerPro", "logs", "updater.log");

    public static async Task CheckAndOfferAsync(Window owner, bool manual = false)
    {
        if (Interlocked.Exchange(ref _checking, 1) != 0)
        {
            if (manual)
                MessageBox.Show(owner, "An update check is already running.", "Android Cleaner Pro");
            return;
        }

        var accepted = false;
        try
        {
            WriteLog("Checking for a new published release.");
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestUrl);
            request.Headers.UserAgent.ParseAdd("AndroidCleanerPro-Updater/1.1");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var response = await Client.SendAsync(request);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                WriteLog("No public GitHub Release was found.");
                if (manual)
                    MessageBox.Show(owner, "No public update release is available.", "Software Updates");
                return;
            }

            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = json.RootElement;

            if (!root.TryGetProperty("tag_name", out var tagNode) ||
                !Version.TryParse(tagNode.GetString()?.TrimStart('v', 'V'), out var latest))
                throw new InvalidOperationException("GitHub returned a release without a valid version.");

            var installed = typeof(UpdateService).Assembly.GetName().Version ?? new Version(0, 0);
            WriteLog($"Installed version {installed}; latest published version {latest}.");
            if (latest <= installed)
            {
                if (manual)
                    MessageBox.Show(owner, $"You're up to date (version {installed}).", "Software Updates");
                return;
            }

            string? downloadUrl = null;
            string? expectedHash = null;
            if (root.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    if (asset.GetProperty("name").GetString() != SetupName) continue;
                    downloadUrl = asset.GetProperty("browser_download_url").GetString();
                    var digest = asset.TryGetProperty("digest", out var digestNode) ? digestNode.GetString() : null;
                    expectedHash = digest is not null &&
                        Regex.IsMatch(digest, @"^sha256:[0-9a-fA-F]{64}$") ? digest[7..] : null;
                    break;
                }
            }

            if (downloadUrl is null || !downloadUrl.StartsWith(OfficialDownloadPrefix, StringComparison.Ordinal) ||
                expectedHash is null)
                throw new InvalidOperationException("The new release has no verified official Windows installer.");

            if (MessageBox.Show(owner,
                $"Android Cleaner Pro {latest} is available (installed: {installed}).\n\n" +
                "Download the verified update and open the normal Windows installer?\n" +
                "The current application will close only after the installer starts.\n" +
                "You will not need to uninstall your existing copy.",
                "Software Update", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                WriteLog($"User deferred update {latest}.");
                return;
            }

            accepted = true;
            WriteLog($"User accepted update {latest}; downloading installer.");
            var path = await DownloadVerifiedInstallerAsync(downloadUrl, expectedHash, latest);
            WriteLog($"SHA-256 verification succeeded for update {latest}.");

            // The bundled ADB server may keep adb.exe in the installation directory
            // in use. Stop it before starting Inno Setup so the files can be replaced.
            WriteLog("Stopping ADB services before starting update installer.");
            var adbStop = await new AdbService().StopAllAdbProcessesAsync();
            WriteLog($"ADB stopped. Other processes stopped: {adbStop.StoppedProcesses}; blocked: {adbStop.BlockedProcesses}. {adbStop.Notes}");
            if (adbStop.BlockedProcesses > 0)
                throw new InvalidOperationException(
                    "Some ADB processes could not be closed. Close Android Studio or other ADB tools and retry the update. " +
                    adbStop.Notes);

            // Use a visible installer instead of silent install + forced restart.
            // Only close the app once Windows confirms the installer was launched.
            Process? installer;
            try
            {
                installer = Process.Start(new ProcessStartInfo(path)
                {
                    UseShellExecute = true,
                    Verb = "runas"
                });
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                WriteLog("Windows elevation request was cancelled. Existing installation left running.");
                MessageBox.Show(owner, "The update was cancelled at the Windows administrator prompt. " +
                    "Android Cleaner Pro has not been changed.", "Software Update");
                return;
            }

            if (installer is null)
                throw new InvalidOperationException("Windows did not start the update installer.");

            WriteLog($"Started installer process for {latest}. Closing old application normally.");
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            WriteLog("Update failed: " + ex);
            if (manual || accepted)
                MessageBox.Show(owner, "The update could not finish. Your current installation is unchanged.\n\n" +
                    ex.Message + "\n\nDiagnostic log:\n" + LogPath,
                    "Software Update Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            // Failed startup checks must not interrupt normal use of the application.
        }
        finally
        {
            Interlocked.Exchange(ref _checking, 0);
        }
    }

    private static async Task<string> DownloadVerifiedInstallerAsync(string url, string expectedHash, Version version)
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AndroidCleanerPro", "Updates", version.ToString());
        Directory.CreateDirectory(directory);

        var destination = Path.Combine(directory, SetupName);
        if (File.Exists(destination))
        {
            if (await HasExpectedHashAsync(destination, expectedHash))
                return destination;
            File.Delete(destination);
        }

        var partial = destination + ".download";
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd("AndroidCleanerPro-Updater/1.1");
            using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is > 524288000)
                throw new InvalidOperationException("Update exceeds the maximum allowed download size.");

            await using (var source = await response.Content.ReadAsStreamAsync())
            await using (var target = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None))
                await source.CopyToAsync(target);

            if (new FileInfo(partial).Length > 524288000)
                throw new InvalidOperationException("Update exceeds the maximum allowed download size.");
            if (!await HasExpectedHashAsync(partial, expectedHash))
                throw new InvalidOperationException("Downloaded installer failed SHA-256 verification.");

            File.Move(partial, destination, overwrite: true);
            return destination;
        }
        finally
        {
            try { if (File.Exists(partial)) File.Delete(partial); } catch { }
        }
    }

    private static async Task<bool> HasExpectedHashAsync(string path, string expectedHash)
    {
        await using var file = File.OpenRead(path);
        var actual = Convert.ToHexString(await SHA256.HashDataAsync(file));
        return actual.Equals(expectedHash, StringComparison.OrdinalIgnoreCase);
    }

    private static void WriteLog(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
        }
        catch { /* Logging must never make the application fail. */ }
    }
}
