using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace AndroidCleanerPro.Services;

public record DeviceInfo(string Serial, string State, string Description)
{
    public override string ToString() => $"{Serial} — {State} {Description}";
}
public record PackageInfo(string Name, string Installer, string Risk, string Reason);
public record AdbStopResult(int StoppedProcesses, int BlockedProcesses, string Notes);

public sealed class AdbService
{
    private static string AdbPath
    {
        get
        {
            var configured = SettingsStore.Current.AdbExecutablePath;
            if (!string.IsNullOrWhiteSpace(configured))
            {
                if (!File.Exists(configured))
                    throw new InvalidOperationException(
                        "The custom ADB path no longer exists. Open Tools > Settings and select a valid adb.exe or restore bundled ADB.");
                return configured;
            }
            var local = Path.Combine(AppContext.BaseDirectory, "platform-tools", "adb.exe");
            return File.Exists(local) ? local : "adb";
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

    /// <summary>
    /// Stop the ADB server and any remaining adb.exe processes accessible to the current
    /// Windows user. This can interrupt other Android tools using ADB.
    /// </summary>
    public async Task<AdbStopResult> StopAllAdbProcessesAsync()
    {
        var details = new List<string>();
        try
        {
            await ExecuteAsync(null, "kill-server");
            details.Add("ADB kill-server command completed.");
        }
        catch (Exception ex)
        {
            // The ADB binary may be missing or the server may already be stopped.
            details.Add("ADB kill-server: " + ex.Message);
        }

        var stopped = 0;
        var blocked = 0;
        foreach (var process in Process.GetProcessesByName("adb"))
        {
            using (process)
            {
                try
                {
                    if (process.HasExited) continue;
                    process.Kill();
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await process.WaitForExitAsync(timeout.Token);
                    stopped++;
                }
                catch (InvalidOperationException)
                {
                    // The process exited while stopping it.
                }
                catch (Exception ex)
                {
                    blocked++;
                    details.Add($"Could not stop ADB process {process.Id}: {ex.Message}");
                }
            }
        }

        return new AdbStopResult(stopped, blocked, string.Join(" ", details));
    }

    /// <summary>Start the ADB server; device authorization is still required on the phone.</summary>
    public Task<string> StartServerAsync() => ExecuteAsync(null, "start-server");

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