using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using AndroidCleanerPro.Services;
using Microsoft.Win32;

namespace AndroidCleanerPro;

public partial class MainWindow : Window
{
    private readonly AdbService _adb = new();
    private readonly ObservableCollection<PackageInfo> _packages = new();
    private ICollectionView? _packageView;
    private bool _busy;
    public MainWindow()
    {
        InitializeComponent();
        _packageView = CollectionViewSource.GetDefaultView(_packages);
        _packageView.Filter = IsVisibleInventoryApp;
        PackagesGrid.ItemsSource = _packageView;
        _packages.CollectionChanged += (_, _) => UpdateInventoryCount();
        UpdateInventoryCount();
        VersionText.Text = "v" + (typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "unknown") + " • Native C# / WPF";
        Loaded += async (_, _) =>
        {
            await RunAsync(RefreshDevicesAsync);
            if (SettingsStore.Current.CheckForUpdatesOnStartup)
                await UpdateService.CheckAndOfferAsync(this);
        };
    }
    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var dialog = new SettingsWindow { Owner = this };
        if (dialog.ShowDialog() == true)
            StatusText.Text = "Settings saved. Changes to the ADB path will be used by the next device operation.";
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AboutWindow { Owner = this };
        dialog.ShowDialog();
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    private void Documentation_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(
                "https://github.com/Brcak/Android-Cleaner-Pro/blob/main/docs/DOTNET_WINDOWS.md")
            { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Unable to open documentation: " + ex.Message,
                "Documentation", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void InventoryFilter_Changed(object sender, SelectionChangedEventArgs e)
        => ApplyInventoryFilters();

    private void InventorySearch_Changed(object sender, TextChangedEventArgs e)
        => ApplyInventoryFilters();

    private void InventoryHide_Changed(object sender, RoutedEventArgs e)
        => ApplyInventoryFilters();

    private static string SelectedFilterText(ComboBox? box, string fallback)
        => (box?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? fallback;

    private bool IsVisibleInventoryApp(object item)
    {
        if (item is not PackageInfo app) return false;

        var category = SelectedFilterText(InventoryCategoryFilter, "All categories");
        if (category != "All categories" && category != app.Category)
            return false;

        var type = SelectedFilterText(InventoryTypeFilter, "All apps");
        if (type == "System apps" && !app.IsSystemApp) return false;
        if (type == "User apps" && app.IsSystemApp) return false;

        // If a category is chosen explicitly, that selection overrides hiding
        // familiar namespaces so Google/Samsung etc. can still be inspected.
        if (category == "All categories" && HideFamiliarCheck?.IsChecked == true
            && app.IsFamiliarCategory)
            return false;

        var search = InventorySearchBox?.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(search) &&
            !app.Name.Contains(search, StringComparison.OrdinalIgnoreCase) &&
            !app.Category.Contains(search, StringComparison.OrdinalIgnoreCase) &&
            !app.Installer.Contains(search, StringComparison.OrdinalIgnoreCase))
            return false;

        var tag = (InventoryAgeFilter?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "all";
        if (tag == "unknown") return !app.FirstInstalledUtc.HasValue;
        if (int.TryParse(tag, out var days))
        {
            if (!app.FirstInstalledUtc.HasValue) return false;
            var elapsed = DateTimeOffset.UtcNow - app.FirstInstalledUtc.Value;
            if (elapsed < TimeSpan.Zero || elapsed > TimeSpan.FromDays(days))
                return false;
        }
        return true;
    }

    private void ApplyInventoryFilters()
    {
        if (_packageView is null) return;
        _packageView.Refresh();
        UpdateInventoryCount();
    }

    private void UpdateInventoryCount()
    {
        if (InventoryCountText is null) return;
        var visible = _packageView?.Cast<PackageInfo>().Count() ?? 0;
        InventoryCountText.Text = $"{visible:N0} shown / {_packages.Count:N0} scanned";
    }

    private DeviceInfo? SelectedDevice => DeviceSelector.SelectedItem as DeviceInfo;
    private async Task RunAsync(Func<Task> action)
    {
        if (_busy) return;
        _busy = true;
        StatusText.Text = "Working…";
        try { await action(); }
        catch (Exception ex) { StatusText.Text = ex.Message; MessageBox.Show(this, ex.Message, "Android Cleaner Pro", MessageBoxButton.OK, MessageBoxImage.Warning); }
        finally { _busy = false; }
    }
    private async Task RefreshDevicesAsync()
    {
        var previous = SelectedDevice?.Serial;
        var devices = await _adb.GetDevicesAsync();
        var named = devices.Select(d => d with { Description = string.IsNullOrWhiteSpace(d.Description) ? d.Serial + " (" + d.State + ")" : d.Serial + " (" + d.State + ") • " + d.Description }).ToArray();
        DeviceSelector.ItemsSource = named;
        DeviceSelector.SelectedItem = named.FirstOrDefault(x => x.Serial == previous) ?? named.FirstOrDefault();
        if (SelectedDevice is { State: "device" } connected)
        {
            try { await LoadPhoneInfoAsync(connected); }
            catch (Exception ex)
            {
                DeviceDetails.Text = $"Connected: {connected.Serial} • device";
                PhoneInfoText.Text = "Connected, but optional phone details could not be read: " + ex.Message;
            }
        }
        StatusText.Text = devices.Count == 0
            ? "No ADB devices. Check USB cable, drivers, debugging authorization, and adb devices -l."
            : $"{devices.Count} device(s) detected. Unauthorized devices must be approved on the phone.";
    }
    private async void StopAdb_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        if (MessageBox.Show(this,
            "Stop every accessible adb.exe process on this PC? This will disconnect connected phones and may interrupt Android Studio, other repair tools, and active ADB transfers. No files on the phone will be removed.",
            "Confirm: Stop All ADB Services",
            MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

        await RunAsync(async () =>
        {
            var result = await _adb.StopAllAdbProcessesAsync();
            DeviceSelector.ItemsSource = null;
            _packages.Clear();
            DeviceDetails.Text = "ADB services stopped.";
            PhoneInfoText.Text = "Click Restart ADB + Detect Phone to reconnect.";
            StatusText.Text = $"ADB server stopped. Additional adb.exe processes stopped: {result.StoppedProcesses}. Unable to stop: {result.BlockedProcesses}.";
            if (result.BlockedProcesses > 0)
                MessageBox.Show(this, result.Notes,
                    "Some ADB processes could not be stopped",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
        });
    }

    private async void RestartAdb_Click(object sender, RoutedEventArgs e)
        => await RunAsync(async () =>
        {
            StatusText.Text = "Stopping ADB services before reconnecting...";
            var result = await _adb.StopAllAdbProcessesAsync();
            if (result.BlockedProcesses > 0)
                throw new InvalidOperationException(
                    $"Unable to stop {result.BlockedProcesses} ADB process(es). Close other Android tools or run with appropriate Windows permissions. {result.Notes}");
            StatusText.Text = "Starting a clean ADB server...";
            await _adb.StartServerAsync();
            await Task.Delay(750);
            await RefreshDevicesAsync();
            if (SelectedDevice is null)
                StatusText.Text = "ADB restarted but no phone is listed. Unlock the phone, approve USB debugging, and check the USB data cable or Windows driver.";
        });

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RunAsync(RefreshDevicesAsync);
    private async Task LoadPhoneInfoAsync(DeviceInfo device)
    {
        DeviceDetails.Text = $"Connected: {device.Serial} • {device.State}";
        var brand = await _adb.GetPropertyAsync(device.Serial, "ro.product.manufacturer");
        var model = await _adb.GetPropertyAsync(device.Serial, "ro.product.model");
        var version = await _adb.GetPropertyAsync(device.Serial, "ro.build.version.release");
        var sdk = await _adb.GetPropertyAsync(device.Serial, "ro.build.version.sdk");
        var build = await _adb.GetPropertyAsync(device.Serial, "ro.build.display.id");
        var battery = await _adb.ExecuteAsync(device.Serial, "shell", "dumpsys", "battery");
        var level = System.Text.RegularExpressions.Regex.Match(battery, @"(?m)^\s*level:\s*(\d+)");
        PhoneInfoText.Text = $"{brand} {model} • Android {version} (API {sdk}) • Battery: {(level.Success ? level.Groups[1].Value + "%" : "Unavailable")}\nBuild: {build}";
    }
    private async void DeviceSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _packages.Clear();
        if (SelectedDevice is not { } device) { DeviceDetails.Text = "No device selected"; PhoneInfoText.Text = ""; return; }
        DeviceDetails.Text = $"Serial: {device.Serial} • Status: {device.State}";
        if (device.State != "device") { PhoneInfoText.Text = "Unlock phone and approve USB debugging."; return; }
        if (_busy) return;
        await RunAsync(() => LoadPhoneInfoAsync(device));
    }
    private async void PhoneInfo_Click(object sender, RoutedEventArgs e)
        => await RunAsync(async () =>
        {
            var device = RequireDevice();
            StatusText.Text = "Reading phone identity, battery, and hardware details...";
            var details = await new PhoneDetailsService(_adb).ReadAsync(device);
            var window = new PhoneInfoWindow(details) { Owner = this };
            window.ShowDialog();
            StatusText.Text = $"Phone details loaded for {device.Serial}.";
        });
    private async void Recovery_Click(object sender, RoutedEventArgs e)
        => await RunAsync(async () =>
        {
            var device = RequireDevice();
            if (MessageBox.Show(this,
                $"Reboot {device.Serial} into Android Recovery? The phone will disconnect from ADB. Do not select Wipe Data in recovery.",
                "Confirm recovery reboot", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            await _adb.ExecuteAsync(device.Serial, "reboot", "recovery");
            StatusText.Text = "Recovery reboot requested. Reconnect after returning to Android.";
        });
    private void SafeMode_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(this,
            "Safe Mode cannot be started reliably through standard ADB on every Android model.\n\nOn many Android phones: hold the Power button (or Power + Volume Down), then touch and hold Power off and choose Safe mode.\n\nOn Samsung devices, the exact button combination can vary. No reboot command was sent.",
            "Safe Mode instructions", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    private async void Update_Click(object sender, RoutedEventArgs e)
        => await UpdateService.CheckAndOfferAsync(this, manual: true);
    private void FrpRecovery_Click(object sender, RoutedEventArgs e)
    {
        // FRP setup locks generally do not allow ADB debugging. This help window
        // intentionally works without a connected or authorized device.
        var window = new FrpRecoveryWindow { Owner = this };
        window.ShowDialog();
    }

    private void Firmware_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var authorizedAdbSerial = SelectedDevice is { State: "device" } connected
            ? connected.Serial
            : null;
        var window = new FirmwareWindow(_adb, authorizedAdbSerial) { Owner = this };
        window.ShowDialog();
    }

    private void Adware_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        if (SelectedDevice is not { State: "device" } device)
        {
            MessageBox.Show(this, "Connect and authorize a phone first, then click Refresh Devices.",
                "Adware & Suspicious Apps", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var window = new AdwareScanWindow(_adb, device) { Owner = this };
        window.ShowDialog();
    }
    private async void Scan_Click(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        var device = RequireDevice();
        StatusText.Text = "Reading system and user app inventory. Android install dates may take longer...";
        var result = await new InventoryService(_adb).ScanAsync(device.Serial);
        _packages.Clear();
        foreach (var package in result.Packages) _packages.Add(package);
        ApplyInventoryFilters();
        StatusText.Text = $"Scanned {result.Packages.Count} Android apps. {result.DateStatus} " +
                          "Vendor/finance categories are name-based clues, not trusted-app verdicts.";
    });
    private DeviceInfo RequireDevice()
    {
        if (SelectedDevice is not { State: "device" } device)
            throw new InvalidOperationException("Select an authorized device first.");
        return device;
    }
    private async void Uninstall_Click(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        var device = RequireDevice();
        if (PackagesGrid.SelectedItem is not PackageInfo package)
            throw new InvalidOperationException("Select one package first.");
        if (package.IsSystemApp)
            throw new InvalidOperationException("System apps cannot be uninstalled through this inventory. Use manufacturer-supported service procedures instead.");
        var answer = MessageBox.Show(this,
            $"Remove {package.Name} for Android user 0 on {device.Serial}? This may delete customer app data. Confirm owner authorization before proceeding.",
            "Confirm package removal", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;
        var result = await _adb.UninstallAsync(device.Serial, package.Name);
        _packages.Remove(package);
        StatusText.Text = $"{package.Name}: {result}";
    });
    private async void Export_Click(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        var device = RequireDevice();
        var dialog = new SaveFileDialog { Filter = "JSON reports (*.json)|*.json", FileName = "android-cleaner-report.json" };
        if (dialog.ShowDialog(this) != true) return;
        var visible = (_packageView?.Cast<PackageInfo>() ?? Enumerable.Empty<PackageInfo>()).ToArray();
        var report = new
        {
            timestampUtc = DateTimeOffset.UtcNow,
            deviceSerial = device.Serial,
            exportedVisibleRowsOnly = true,
            totalScanned = _packages.Count,
            visibleCount = visible.Length,
            categoryFilter = SelectedFilterText(InventoryCategoryFilter, "All categories"),
            typeFilter = SelectedFilterText(InventoryTypeFilter, "All apps"),
            installAgeFilter = (InventoryAgeFilter.SelectedItem as ComboBoxItem)?.Content?.ToString(),
            search = InventorySearchBox.Text,
            hideFamiliarCategories = HideFamiliarCheck.IsChecked == true,
            packages = visible,
            note = "Vendor category is an unverified namespace clue; timestamps are Android-reported. This is not a malware diagnosis."
        };
        await File.WriteAllTextAsync(dialog.FileName, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        StatusText.Text = $"Exported {visible.Length} visible inventory rows to {dialog.FileName}";
    });
}