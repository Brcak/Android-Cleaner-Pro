using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace AndroidCleanerPro.Services;

public sealed record FastbootDeviceInfo(string Serial, string Detail)
{
    public override string ToString() => string.IsNullOrWhiteSpace(Detail) ? Serial : $"{Serial} — {Detail}";
}

public sealed record FastbootPreflight(
    string Serial, string Product, string Unlocked, string Slot, string IsUserspace, string Secure)
{
    public bool UnlockedConfirmed =>
        Unlocked.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
        Unlocked.Equals("true", StringComparison.OrdinalIgnoreCase) ||
        Unlocked == "1";
}

public sealed record FlashImageInfo(string Path, string Partition, long SizeBytes, string Sha256);

/// <summary>
/// Limited fastboot diagnostics and explicitly authorized single-partition image flashing.
/// No bootloader unlocking, erasing, locking, bypasses, OEM scripts or batch flashing.
/// </summary>
public sealed class FirmwareService(AdbService adb)
{
    public static readonly string[] SupportedPartitions = ["boot", "init_boot", "vendor_boot", "recovery"];

    private static string FastbootPath
    {
        get
        {
            var customAdb = SettingsStore.Current.AdbExecutablePath;
            if (!string.IsNullOrWhiteSpace(customAdb))
            {
                var sibling = Path.Combine(Path.GetDirectoryName(customAdb)!, "fastboot.exe");
                if (!File.Exists(sibling))
                    throw new InvalidOperationException(
                        "A custom ADB path is configured but fastboot.exe is not beside it. Choose Platform Tools in Settings.");
                return sibling;
            }
            var bundled = Path.Combine(AppContext.BaseDirectory, "platform-tools", "fastboot.exe");
            return File.Exists(bundled) ? bundled : "fastboot";
        }
    }

    private static async Task<string> RunFastbootAsync(
        string? serial, TimeSpan timeout, params string[] arguments)
    {
        var info = new ProcessStartInfo(FastbootPath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            UseShellExecute = false
        };
        if (!string.IsNullOrWhiteSpace(serial))
        {
            info.ArgumentList.Add("-s");
            info.ArgumentList.Add(serial);
        }
        foreach (var argument in arguments)
            info.ArgumentList.Add(argument);

        try
        {
            using var process = new Process { StartInfo = info };
            process.Start();
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            using var timed = new CancellationTokenSource(timeout);
            try
            {
                await process.WaitForExitAsync(timed.Token);
            }
            catch (OperationCanceledException)
            {
                // A timed-out flash is not safe to blindly retry. Report the
                // physical state and never automatically issue a second write.
                try { process.Kill(entireProcessTree: true); } catch { }
                throw new TimeoutException(
                    "Fastboot timed out. Do not disconnect or retry flashing blindly. " +
                    "Inspect the phone screen and its bootloader state before taking further action.");
            }
            var standard = await stdoutTask;
            var errors = await stderrTask;
            var output = string.Join(Environment.NewLine,
                new[] { standard, errors }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
            if (process.ExitCode != 0)
                throw new InvalidOperationException(
                    $"Fastboot exited with code {process.ExitCode}: {output}");
            return output;
        }
        catch (Win32Exception)
        {
            throw new InvalidOperationException(
                "fastboot.exe was not found. Update Android Cleaner Pro or install official Android SDK Platform Tools.");
        }
    }

    public async Task<IReadOnlyList<FastbootDeviceInfo>> GetDevicesAsync()
    {
        var output = await RunFastbootAsync(null, TimeSpan.FromSeconds(15), "devices", "-l");
        var result = new List<FastbootDeviceInfo>();
        foreach (var line in output.Split('\n'))
        {
            var match = Regex.Match(line.Trim(), @"^(\S+)\s+fastboot(?:\s+(.+))?$");
            if (match.Success)
                result.Add(new FastbootDeviceInfo(match.Groups[1].Value, match.Groups[2].Value));
        }
        return result;
    }

    private async Task<string> GetVarAsync(string serial, string name)
    {
        try
        {
            var output = await RunFastbootAsync(serial, TimeSpan.FromSeconds(15), "getvar", name);
            var match = Regex.Match(output,
                @"(?im)^\s*(?:\(bootloader\)\s*)?" + Regex.Escape(name) + @":\s*(\S+)");
            return match.Success ? match.Groups[1].Value.Trim() : "Not reported";
        }
        catch
        {
            return "Not reported";
        }
    }

    public async Task<FastbootPreflight> ReadPreflightAsync(string serial)
    {
        var devices = await GetDevicesAsync();
        if (!devices.Any(x => x.Serial.Equals(serial, StringComparison.Ordinal)))
            throw new InvalidOperationException("Selected phone is no longer present in Fastboot mode.");
        // Sequential queries avoid overwhelming some OEM fastboot bootloaders.
        var product = await GetVarAsync(serial, "product");
        var unlocked = await GetVarAsync(serial, "unlocked");
        var slot = await GetVarAsync(serial, "current-slot");
        var userspace = await GetVarAsync(serial, "is-userspace");
        var secure = await GetVarAsync(serial, "secure");
        return new FastbootPreflight(serial, product, unlocked, slot, userspace, secure);
    }

    public async Task RebootAdbToBootloaderAsync(string adbSerial)
    {
        await adb.ExecuteAsync(adbSerial, "reboot", "bootloader");
    }

    public async Task RebootFastbootToSystemAsync(string serial)
    {
        var devices = await GetDevicesAsync();
        if (!devices.Any(x => x.Serial.Equals(serial, StringComparison.Ordinal)))
            throw new InvalidOperationException("Selected phone is no longer present in Fastboot mode.");
        await RunFastbootAsync(serial, TimeSpan.FromSeconds(25), "reboot");
    }

    public static async Task<FlashImageInfo> InspectImageAsync(string path, string partition)
    {
        if (!SupportedPartitions.Contains(partition, StringComparer.Ordinal))
            throw new InvalidOperationException("This partition is not in the limited flash allowlist.");
        if (!File.Exists(path) || !Path.IsPathFullyQualified(path) ||
            !string.Equals(Path.GetExtension(path), ".img", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Choose an existing .img firmware file.");
        var info = new FileInfo(path);
        if (info.Length == 0)
            throw new InvalidOperationException("The image file is empty.");
        await using var source = File.OpenRead(path);
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(source));
        return new FlashImageInfo(path, partition, info.Length, hash);
    }

    public async Task<string> FlashSinglePartitionAsync(
        string serial, string expectedProduct, FlashImageInfo image)
    {
        if (!SupportedPartitions.Contains(image.Partition, StringComparer.Ordinal))
            throw new InvalidOperationException("Partition rejected by the flash safety policy.");

        var fresh = await ReadPreflightAsync(serial);
        if (!fresh.UnlockedConfirmed)
            throw new InvalidOperationException(
                "Flashing blocked: Fastboot did not confirm an unlocked bootloader. " +
                "The app does not unlock bootloaders or erase user data.");
        if (fresh.Product == "Not reported" ||
            !fresh.Product.Equals(expectedProduct, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Flashing blocked: the device product changed or could not be verified.");
        if (fresh.IsUserspace.Equals("yes", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Boot/recovery images must not be written from userspace fastbootd in this workflow. " +
                "Return to the device bootloader and verify OEM instructions.");

        var recheck = await InspectImageAsync(image.Path, image.Partition);
        if (recheck.Sha256 != image.Sha256 || recheck.SizeBytes != image.SizeBytes)
            throw new InvalidOperationException("Image file changed since verification. Flash was blocked.");

        return await RunFastbootAsync(serial, TimeSpan.FromMinutes(20),
            "flash", image.Partition, image.Path);
    }
}
