using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Text.RegularExpressions;

namespace AndroidCleanerPro.Services;

public record DeviceInfo(string Serial, string State, string Description)
{
    public override string ToString() => $"{Serial} — {State} {Description}";
}
public record PackageInfo(string Name, string Installer, string Risk, string Reason);

public sealed class AdbService
{
    private static readonly object ExtractionLock = new();
    private static string AdbPath
    {
        get
        {
            var local = Path.Combine(AppContext.BaseDirectory, "platform-tools", "adb.exe");
            if (File.Exists(local)) return local;
            var resourceName = "AndroidCleanerPro.PlatformTools.zip";
            var assembly = typeof(AdbService).Assembly;
            using var embedded = assembly.GetManifestResourceStream(resourceName);
            if (embedded is null)
                throw new InvalidOperationException("Bundled ADB is missing. Download the official portable build.");
            var version = assembly.ManifestModule.ModuleVersionId.ToString("N");
            var cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AndroidCleanerPro", "adb", version);
            var adb = Path.Combine(cache, "platform-tools", "adb.exe");
            lock (ExtractionLock)
            {
                if (File.Exists(adb)) return adb;
                Directory.CreateDirectory(cache);
                using var archive = new ZipArchive(embedded, ZipArchiveMode.Read);
                archive.ExtractToDirectory(cache, overwriteFiles: true);
                if (!File.Exists(adb))
                    throw new InvalidOperationException("Bundled ADB extraction failed.");
            }
            return adb;
        }
    }

    public async Task<string> ExecuteAsync(string? serial, params string[] args)
    {
        var start = new ProcessStartInfo(AdbPath)
        {
            RedirectStandardOutput = true, RedirectStandardError = true,
            UseShellExecute = false, CreateNoWindow = true
        };
        if (!string.IsNullOrWhiteSpace(serial))
        {
            start.ArgumentList.Add("-s");
            start.ArgumentList.Add(serial);
        }
        foreach (var arg in args) start.ArgumentList.Add(arg);
        try
        {
            using var process = new Process { StartInfo = start };
            process.Start();
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                throw new InvalidOperationException("ADB timed out. Reconnect the device and try again.");
            }
            var output = await stdout;
            var error = await stderr;
            if (process.ExitCode != 0) throw new InvalidOperationException(error.Trim() is { Length: > 0 } e ? e : $"ADB exited with code {process.ExitCode}.");
            return output;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            throw new InvalidOperationException("ADB not found. Install Android SDK Platform Tools and add adb to PATH, or put platform-tools beside the application.");
        }
    }

    public async Task<IReadOnlyList<DeviceInfo>> GetDevicesAsync()
    {
        var output = await ExecuteAsync(null, "devices", "-l");
        var devices = new List<DeviceInfo>();
        foreach (var line in output.Split('\n').Skip(1))
        {
            var match = Regex.Match(line.Trim(), @"^(\S+)\s+(device|unauthorized|offline|recovery|sideload|bootloader)(?:\s+(.*))?$");
            if (match.Success)
                devices.Add(new DeviceInfo(match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value));
        }
        return devices;
    }

    public async Task<string> GetPropertyAsync(string serial, string property)
        => (await ExecuteAsync(serial, "shell", "getprop", property)).Trim();

    public async Task<IReadOnlyList<PackageInfo>> GetPackagesAsync(string serial)
    {
        var output = await ExecuteAsync(serial, "shell", "pm", "list", "packages", "-3", "-i");
        var result = new List<PackageInfo>();
        foreach (var line in output.Split('\n'))
        {
            var match = Regex.Match(line.Trim(), @"^package:(\S+?)(?:\s+installer=(.*))?$");
            if (!match.Success) continue;
            var name = match.Groups[1].Value;
            var installer = match.Groups[2].Value.Trim();
            var suspicious = new[] { "cleaner", "booster", "speedup", "fakeupdate", "adpopup" }
                .Any(term => name.Contains(term, StringComparison.OrdinalIgnoreCase));
            var risk = suspicious ? "Review" : "Unknown";
            var reason = suspicious ? "Package name contains a review keyword; not proof of malware" : "No heuristic match; not a security verdict";
            result.Add(new PackageInfo(name, installer, risk, reason));
        }
        return result.OrderByDescending(x => x.Risk == "Review").ThenBy(x => x.Name).ToArray();
    }

    public async Task<string> UninstallAsync(string serial, string package)
    {
        if (string.IsNullOrWhiteSpace(package) || !Regex.IsMatch(package, @"^[a-zA-Z0-9_]+(?:\.[a-zA-Z0-9_]+)+$"))
            throw new InvalidOperationException("Invalid package name.");
        if (new[] { "android", "com.android.", "com.google.android.", "com.samsung.android.", "com.sec." }
            .Any(prefix => package == prefix || package.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Protected system/vendor namespace.");
        var output = await ExecuteAsync(serial, "shell", "pm", "uninstall", "--user", "0", package);
        if (!output.Contains("Success", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Uninstall was not confirmed: {output.Trim()}");
        return output.Trim();
    }
}