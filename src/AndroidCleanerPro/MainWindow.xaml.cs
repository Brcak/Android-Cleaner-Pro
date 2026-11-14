using System.Collections.ObjectModel;
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
    private bool _busy;
    public MainWindow()
    {
        InitializeComponent();
        PackagesGrid.ItemsSource = _packages;
        VersionText.Text = "v" + (typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "unknown") + " • Native C# / WPF";
        Loaded += async (_, _) => { await RunAsync(RefreshDevicesAsync); await UpdateService.CheckAndOfferAsync(this); };
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
            await LoadPhoneInfoAsync(connected);
        StatusText.Text = devices.Count == 0
            ? "No ADB devices. Check USB cable, drivers, debugging authorization, and adb devices -l."
            : $"{devices.Count} device(s) detected. Unauthorized devices must be approved on the phone.";
    }
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
    private async void Scan_Click(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        var device = RequireDevice();
        var packages = await _adb.GetPackagesAsync(device.Serial);
        _packages.Clear();
        foreach (var package in packages) _packages.Add(package);
        StatusText.Text = $"{packages.Count} third-party apps found. Review labels are heuristics, not malware findings.";
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
        var report = new { timestampUtc = DateTimeOffset.UtcNow, deviceSerial = device.Serial, packages = _packages.ToArray(), note = "Heuristic review only; not a malware diagnosis." };
        await File.WriteAllTextAsync(dialog.FileName, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        StatusText.Text = $"Report exported to {dialog.FileName}";
    });
}