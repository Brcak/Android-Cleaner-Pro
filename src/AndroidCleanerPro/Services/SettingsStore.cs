using System.IO;
using System.Text.Json;

namespace AndroidCleanerPro.Services;

public sealed class AppSettings
{
    public bool CheckForUpdatesOnStartup { get; set; } = true;

    // Blank means use bundled Platform Tools, then the system PATH.
    public string AdbExecutablePath { get; set; } = "";
}

/// <summary>
/// Per-user preferences only. Do not store device identifiers, IMEI values,
/// customer package inventories or other diagnostic data in settings.
/// </summary>
public static class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AndroidCleanerPro", "settings.json");

    public static AppSettings Current { get; private set; } = Load();

    private static AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new AppSettings();
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch
        {
            // A corrupted or inaccessible file must never prevent the app from launching.
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        var adbPath = settings.AdbExecutablePath.Trim().Trim('"');
        if (!string.IsNullOrEmpty(adbPath))
        {
            if (!Path.IsPathFullyQualified(adbPath) ||
                !string.Equals(Path.GetFileName(adbPath), "adb.exe", StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(adbPath))
                throw new InvalidOperationException(
                    "Choose an existing adb.exe file using its full Windows path, or leave the field blank for bundled ADB.");
        }

        var normalized = new AppSettings
        {
            CheckForUpdatesOnStartup = settings.CheckForUpdatesOnStartup,
            AdbExecutablePath = adbPath
        };

        var folder = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(folder);
        var temp = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(normalized, Options));
            File.Move(temp, FilePath, overwrite: true);
            Current = normalized;
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
        }
    }
}
