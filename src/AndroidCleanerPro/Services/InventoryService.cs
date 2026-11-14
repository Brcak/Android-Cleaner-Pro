using System.Globalization;
using System.Text.RegularExpressions;

namespace AndroidCleanerPro.Services;

public sealed record InventoryScanResult(
    IReadOnlyList<PackageInfo> Packages,
    bool DatesAvailable,
    int DatedPackages,
    string DateStatus);

/// <summary>
/// Combines Android's package list with best-effort, read-only installation timestamps.
/// Category labels are based on package namespaces, NOT publisher signature verification.
/// </summary>
public sealed class InventoryService(AdbService adb)
{
    private static readonly Regex PackageLine = new(
        @"(?m)^\s*package:(?<name>[^\s]+)(?:\s+installer=(?<installer>[^\r\n]*))?\s*$",
        RegexOptions.Compiled, TimeSpan.FromSeconds(5));

    private static readonly Regex PackageBlock = new(
        @"(?m)^\s*Package \[(?<name>[A-Za-z0-9_.]+)\]\s+\([0-9a-fA-F]+\):",
        RegexOptions.Compiled, TimeSpan.FromSeconds(10));

    private static readonly Regex FirstInstall = new(
        @"(?m)^\s*firstInstallTime\s*=\s*(?<date>\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2})",
        RegexOptions.Compiled, TimeSpan.FromSeconds(5));
    private static readonly Regex LastUpdate = new(
        @"(?m)^\s*lastUpdateTime\s*=\s*(?<date>\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2})",
        RegexOptions.Compiled, TimeSpan.FromSeconds(5));

    public async Task<InventoryScanResult> ScanAsync(string serial)
    {
        // Keep a small, stable Android command set; no root and no APK extraction.
        var userTask = adb.GetPackagesAsync(serial);
        var systemTask = adb.ExecuteAsync(serial, "shell", "pm", "list", "packages", "-s", "-i");
        await Task.WhenAll(userTask, systemTask);

        var entries = new Dictionary<string, PackageInfo>(StringComparer.Ordinal);
        foreach (var app in userTask.Result)
            entries[app.Name] = Classify(app, false);

        var systemOutput = systemTask.Result;
        foreach (Match match in PackageLine.Matches(systemOutput))
        {
            var name = match.Groups["name"].Value;
            var installer = match.Groups["installer"].Success
                ? match.Groups["installer"].Value.Trim() : "";
            var existing = entries.TryGetValue(name, out var value) ? value : null;
            var row = existing ?? new PackageInfo(name, installer,
                "System", "System package; never automatically remove.");
            entries[name] = Classify(row, true);
        }

        // A single dumpsys call is much faster and gentler than one call per app.
        // Some OEMs hide dates or restrict dumpsys output. Such scans still work.
        var datesAvailable = false;
        var dated = 0;
        var dateStatus = "Installation dates are not available from this device.";
        try
        {
            var dumpTask = adb.ExecuteAsync(serial, "shell", "dumpsys", "package");
            var offsetTask = GetDeviceOffsetAsync(serial);
            await Task.WhenAll(dumpTask, offsetTask);
            var timestamps = ReadDates(dumpTask.Result, offsetTask.Result);
            foreach (var item in entries.ToArray())
            {
                if (!timestamps.TryGetValue(item.Key, out var date)) continue;
                entries[item.Key] = item.Value with
                {
                    FirstInstalledUtc = date.First,
                    LastUpdatedUtc = date.Updated
                };
                if (date.First.HasValue) dated++;
            }
            datesAvailable = dated > 0;
            dateStatus = dated > 0
                ? $"Installation dates reported for {dated} of {entries.Count} apps."
                : "Android did not provide readable first-install dates.";
        }
        catch (Exception ex)
        {
            // Do not lose the package inventory because one OEM blocks timestamps.
            dateStatus = "Installation dates unavailable (" + ex.Message + ").";
        }

        return new InventoryScanResult(
            entries.Values.OrderByDescending(x => x.FirstInstalledUtc.HasValue)
                .ThenByDescending(x => x.FirstInstalledUtc)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray(),
            datesAvailable, dated, dateStatus);
    }

    private static PackageInfo Classify(PackageInfo package, bool system)
    {
        var category = Categorize(package.Name, system);
        var familiar = category is not "Other apps";
        return package with
        {
            Category = category,
            IsSystemApp = system,
            IsFamiliarCategory = familiar
        };
    }

    public static string Categorize(string name, bool system)
    {
        // Namespace checks provide grouping, not identity or signature attestation.
        // An app claiming a bank's name is NOT automatically a verified banking app.
        if (Starts(name, "com.chase.", "com.bankofamerica.", "com.wf.",
                "com.usbank.", "com.citi.", "com.capitalone.",
                "com.paypal.", "com.venmo.", "com.sofi.", "com.zelle.",
                "com.pnc.", "com.ally.", "com.americanexpress.",
                "com.fidelity.", "com.robinhood.", "com.schwab.",
                "com.discoverfinancial.", "com.usaa.", "com.navyfederal."))
            return "Banking / Finance";

        if (Starts(name, "com.google.", "com.android.vending", "com.android.chrome",
                "com.google.android."))
            return "Google";

        if (Starts(name, "com.samsung.", "com.sec.", "com.samsungandroid.",
                "com.wssyncmldm", "com.samsung.galaxy."))
            return "Samsung";

        if (Starts(name, "com.motorola.", "com.moto.", "com.lenovo.",
                "com.motorola.mobility."))
            return "Motorola";

        if (Starts(name, "com.tmobile.", "com.tmo.", "com.metropcs.",
                "com.t_mobile.", "com.tmobilepr."))
            return "T-Mobile / Metro";

        if (Starts(name, "com.microsoft.", "com.amazon.", "com.facebook.",
                "com.instagram.", "com.whatsapp.", "com.spotify.",
                "com.netflix.", "com.disney.", "com.adobe.", "com.dropbox.",
                "com.ebay.", "com.ubercab.", "com.google.firebase."))
            return "Other familiar vendors";

        return system ? "Android / System" : "Other apps";
    }

    private static bool Starts(string name, params string[] prefixes) =>
        prefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    private static async Task<TimeSpan?> GetDeviceOffsetAsync(string serial, AdbService adb)
    {
        try
        {
            var raw = (await adb.ExecuteAsync(serial, "shell", "date", "+%z")).Trim();
            var match = Regex.Match(raw, @"^(?<sign>[+-])(?<hh>\d{2})(?<mm>\d{2})$");
            if (!match.Success) return null;
            var hours = int.Parse(match.Groups["hh"].Value, CultureInfo.InvariantCulture);
            var minutes = int.Parse(match.Groups["mm"].Value, CultureInfo.InvariantCulture);
            var result = TimeSpan.FromMinutes(hours * 60 + minutes);
            if (match.Groups["sign"].Value == "-") result = -result;
            return result is >= { TotalHours: -14 } and <= { TotalHours: 14 }
                ? result : null;
        }
        catch { return null; }
    }

    private Task<TimeSpan?> GetDeviceOffsetAsync(string serial) => GetDeviceOffsetAsync(serial, adb);

    private static Dictionary<string, (DateTimeOffset? First, DateTimeOffset? Updated)> ReadDates(
        string dump, TimeSpan? offset)
    {
        var result = new Dictionary<string, (DateTimeOffset?, DateTimeOffset?)>(StringComparer.Ordinal);
        var blocks = PackageBlock.Matches(dump);
        for (var i = 0; i < blocks.Count; i++)
        {
            var current = blocks[i];
            var end = i + 1 < blocks.Count ? blocks[i + 1].Index : dump.Length;
            if (end <= current.Index) continue;
            var body = dump.AsSpan(current.Index, end - current.Index).ToString();
            var installed = FirstInstall.Match(body);
            var updated = LastUpdate.Match(body);
            var date1 = ParseDate(installed.Success ? installed.Groups["date"].Value : null, offset);
            var date2 = ParseDate(updated.Success ? updated.Groups["date"].Value : null, offset);
            var name = current.Groups["name"].Value;
            if (!result.TryGetValue(name, out var existing) ||
                (!existing.Item1.HasValue && date1.HasValue))
                result[name] = (date1, date2);
        }
        return result;
    }

    private static DateTimeOffset? ParseDate(string? text, TimeSpan? offset)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (!DateTime.TryParseExact(text.Trim(), "yyyy-MM-dd HH:mm:ss",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return null;
        try
        {
            // Device prints timestamps in its own local time zone. If we cannot
            // query the offset, don't guess the PC timezone: return unavailable.
            if (offset is null) return null;
            return new DateTimeOffset(DateTime.SpecifyKind(date, DateTimeKind.Unspecified), offset.Value);
        }
        catch { return null; }
    }
}
