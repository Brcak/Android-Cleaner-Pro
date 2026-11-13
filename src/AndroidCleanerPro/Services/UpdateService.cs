using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;

namespace AndroidCleanerPro.Services;

public static class UpdateService
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromMinutes(3) };
    private const string LatestUrl = "https://api.github.com/repos/Brcak/Android-Cleaner-Pro/releases/latest";

    public static async Task CheckAndOfferAsync(Window owner)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestUrl);
            request.Headers.UserAgent.ParseAdd("AndroidCleanerPro-Updater/1.0");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var response = await Client.SendAsync(request);
            if (!response.IsSuccessStatusCode) return;
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = json.RootElement;
            if (!root.TryGetProperty("tag_name", out var tagNode)) return;
            var tag = tagNode.GetString()?.TrimStart('v', 'V');
            if (!Version.TryParse(tag, out var latest)) return;
            var installed = typeof(UpdateService).Assembly.GetName().Version ?? new Version(0, 0);
            if (latest <= installed) return;
            if (!root.TryGetProperty("assets", out var assets)) return;
            foreach (var asset in assets.EnumerateArray())
            {
                if (asset.GetProperty("name").GetString() != "AndroidCleanerPro-Setup.exe") continue;
                var url = asset.GetProperty("browser_download_url").GetString();
                var digest = asset.TryGetProperty("digest", out var digestNode) ? digestNode.GetString() : null;
                if (url is null || !url.StartsWith("https://github.com/Brcak/Android-Cleaner-Pro/releases/download/", StringComparison.OrdinalIgnoreCase)) return;
                if (digest is null || !digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) return;
                if (MessageBox.Show(owner, $"Android Cleaner Pro {latest} is available. Download and install the update now?\n\nThe application will close during installation.", "Software Update", MessageBoxButton.YesNo, MessageBoxImage.Information) != MessageBoxResult.Yes) return;
                var destination = Path.Combine(Path.GetTempPath(), "AndroidCleanerPro-Update-" + Guid.NewGuid().ToString("N") + ".exe");
                try
                {
                    using var stream = await Client.GetStreamAsync(url);
                    await using (var file = File.Create(destination)) await stream.CopyToAsync(file);
                    await using var verify = File.OpenRead(destination);
                    var actual = Convert.ToHexString(await SHA256.HashDataAsync(verify));
                    if (!actual.Equals(digest[7..], StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Update checksum verification failed. Nothing was installed.");
                    Process.Start(new ProcessStartInfo(destination)
                    {
                        UseShellExecute = true,
                        Arguments = "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS"
                    });
                    Application.Current.Shutdown();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(owner, "Update could not be installed: " + ex.Message, "Update error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                return;
            }
        }
        catch { /* Network or GitHub API unavailable: never block device operations. */ }
    }
}
