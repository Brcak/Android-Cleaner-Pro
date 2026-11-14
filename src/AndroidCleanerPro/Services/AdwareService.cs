using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace AndroidCleanerPro.Services;

public sealed record AdwareFinding(
    string Package,
    string Installer,
    string Priority,
    int Score,
    string Indicators,
    string Explanation);

public sealed record AdwareScanProgress(
    int Finished,
    int Total,
    AdwareFinding? Finding);

public sealed record ForegroundObservation(
    string Package,
    string Source,
    DateTimeOffset RecordedAt,
    string Explanation);

/// <summary>
/// Conservative, read-only heuristics. This cannot establish that a package serves ads
/// and must not be represented as a malware verdict.
/// </summary>
public sealed class AdwareService(AdbService adb)
{
    private static readonly string[] NamePatterns =
    [
        "adpopup", "adshelper", "fakeupdate", "screenlocker", "speedup",
        "batteryoptimizer", "phonebooster", "adcleaner", "adservice", "flashlightpro"
    ];

    private static readonly HashSet<string> CommonInstallers = new(StringComparer.OrdinalIgnoreCase)
    {
        "com.android.vending", "com.sec.android.app.samsungapps", "com.amazon.venezia",
        "org.fdroid.fdroid", "com.google.android.packageinstaller", "com.android.packageinstaller",
        "com.google.android.apps.packageinstaller", "com.samsung.android.packageinstaller"
    };

    public async Task<IReadOnlyList<AdwareFinding>> ScanAsync(
        string serial,
        IProgress<AdwareScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        // Inventory only third-party apps; no modifications or root required.
        var packages = await adb.GetPackagesAsync(serial);
        var results = new ConcurrentBag<AdwareFinding>();
        var completed = 0;

        // Avoid flooding phones or preventing other ADB operations.
        await Parallel.ForEachAsync(packages, new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = 3
        }, async (package, token) =>
        {
            token.ThrowIfCancellationRequested();
            AdwareFinding finding;
            try
            {
                finding = await InspectAsync(serial, package, token);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                finding = new AdwareFinding(package.Name, package.Installer,
                    "Incomplete", 0, "Inspection unavailable",
                    "Could not inspect this app: " + ex.Message);
            }
            results.Add(finding);
            var done = Interlocked.Increment(ref completed);
            progress?.Report(new AdwareScanProgress(done, packages.Count, finding));
        });

        return results.OrderByDescending(f => f.Score).ThenBy(f => f.Package).ToArray();
    }

    private async Task<AdwareFinding> InspectAsync(
        string serial,
        PackageInfo package,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        var score = 0;
        var indicators = new List<string>();
        var explanation = new List<string>();
        var name = package.Name;

        if (NamePatterns.Any(term => name.Contains(term, StringComparison.OrdinalIgnoreCase)))
        {
            score += 1;
            indicators.Add("Name keyword");
            explanation.Add("Package name contains an advertising/optimization-related keyword.");
        }

        var installer = string.IsNullOrWhiteSpace(package.Installer) ? "(not reported)" : package.Installer;
        if (installer == "(not reported)" || installer == "null")
        {
            score += 1;
            indicators.Add("No installer reported");
            explanation.Add("Install source is not reported; may occur with legitimate sideloads.");
        }
        else if (!CommonInstallers.Contains(installer))
        {
            // An unfamiliar installer is a weak indicator, not an accusation.
            score += 1;
            indicators.Add("Unfamiliar installer");
            explanation.Add($"Install source is {installer}; review whether it is expected.");
        }

        string dump;
        try { dump = await adb.ExecuteAsync(serial, "shell", "dumpsys", "package", name); }
        catch (Exception ex)
        {
            return new AdwareFinding(name, installer, "Incomplete", score,
                indicators.Count == 0 ? "Package inspection unavailable" : string.Join(" · ", indicators),
                string.Join(" ", explanation) + " Permission inspection unavailable: " + ex.Message);
        }
        token.ThrowIfCancellationRequested();

        // These are package dump references, not proof of permission grants or misuse.
        if (dump.Contains("android.permission.SYSTEM_ALERT_WINDOW", StringComparison.Ordinal))
        {
            score += 1;
            indicators.Add("Overlay permission listed");
            explanation.Add("Package metadata references draw-over-other-apps permission; actual access may differ.");

            try
            {
                var appOps = await adb.ExecuteAsync(serial, "shell", "cmd", "appops",
                    "get", name, "SYSTEM_ALERT_WINDOW");
                if (Regex.IsMatch(appOps, @"(?im)SYSTEM_ALERT_WINDOW\s*:\s*(?:allow|mode=allow)\b"))
                {
                    score += 3;
                    indicators.Add("Overlay allowed by AppOps");
                    explanation.Add("Android currently reports overlay AppOps mode as allowed.");
                }
            }
            catch
            {
                explanation.Add("Android did not expose overlay AppOps status for this app.");
            }
        }
        if (dump.Contains("android.permission.BIND_ACCESSIBILITY_SERVICE", StringComparison.Ordinal))
        {
            score += 2;
            indicators.Add("Accessibility service declaration");
            explanation.Add("Package metadata references an accessibility service; this does not show whether it is enabled.");
        }
        if (dump.Contains("android.permission.BIND_NOTIFICATION_LISTENER_SERVICE", StringComparison.Ordinal))
        {
            score += 1;
            indicators.Add("Notification listener declaration");
            explanation.Add("Package metadata references a notification-listener service.");
        }
        if (dump.Contains("android.permission.REQUEST_INSTALL_PACKAGES", StringComparison.Ordinal))
        {
            score += 1;
            indicators.Add("APK install permission listed");
            explanation.Add("Package metadata references permission to request package installations.");
        }

        var priority = score >= 5 ? "Elevated review" : score >= 2 ? "Review"
            : score == 1 ? "Weak signal" : "No indicators";
        var combined = string.Join(" · ", indicators);
        return new AdwareFinding(name, installer, priority, score,
            combined.Length == 0 ? "No checked indicators found" : combined,
            explanation.Count == 0
                ? "No checked indicators detected. This does not prove the app is safe."
                : string.Join(" ", explanation));
    }

    /// <summary>
    /// Snapshot of the current Android foreground window. If an ad is on screen,
    /// this can be a clue, but the focused app may be an innocent host or system UI.
    /// </summary>
    public async Task<ForegroundObservation> ReadForegroundAsync(string serial)
    {
        var raw = await adb.ExecuteAsync(serial, "shell", "dumpsys", "window");
        var lines = raw.Split('\n').Select(x => x.Trim()).ToArray();

        foreach (var key in new[] { "mCurrentFocus", "mFocusedApp" })
        {
            var line = lines.FirstOrDefault(l => l.Contains(key + "=", StringComparison.Ordinal));
            if (line is null) continue;
            var match = Regex.Match(line,
                @"(?<package>[A-Za-z_]\w*(?:\.[A-Za-z_]\w*){1,})/(?=[A-Za-z_.])");
            if (match.Success)
                return new ForegroundObservation(match.Groups["package"].Value,
                    "Window Manager (" + key + ")", DateTimeOffset.Now,
                    "This app owns the currently focused window. It may not be the source of an overlay or ad.");
        }

        var activity = await adb.ExecuteAsync(serial, "shell", "dumpsys", "activity", "activities");
        foreach (var line in activity.Split('\n'))
        {
            if (!line.Contains("topResumedActivity", StringComparison.OrdinalIgnoreCase) &&
                !line.Contains("mResumedActivity", StringComparison.OrdinalIgnoreCase))
                continue;
            var match = Regex.Match(line,
                @"(?<package>[A-Za-z_]\w*(?:\.[A-Za-z_]\w*){1,})/(?=[A-Za-z_.])");
            if (match.Success)
                return new ForegroundObservation(match.Groups["package"].Value,
                    "Activity Manager", DateTimeOffset.Now,
                    "This activity is reported as foreground. The advertising SDK may belong to this app or another app.");
        }

        return new ForegroundObservation("(not available)", "ADB",
            DateTimeOffset.Now, "Android did not expose a foreground package in these diagnostics.");
    }
}
