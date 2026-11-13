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
        Loaded += async (_, _) => await RunAsync(RefreshDevicesAsync);
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
        DeviceSelector.ItemsSource = devices;
        DeviceSelector.SelectedItem = devices.FirstOrDefault(x => x.Serial == previous) ?? devices.FirstOrDefault();
        StatusText.Text = devices.Count == 0
            ? "No ADB devices. Check USB cable, drivers, debugging authorization, and adb devices -l."
            : $"{devices.Count} device(s) detected. Unauthorized devices must be approved on the phone.";
    }
    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RunAsync(RefreshDevicesAsync);
    private async void DeviceSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _packages.Clear();
        if (SelectedDevice is not { } device) { DeviceDetails.Text = "No device selected"; return; }
        DeviceDetails.Text = $"Serial: {device.Serial}  |  Status: {device.State}";
        if (device.State != "device") { StatusText.Text = "Unlock phone and approve the USB debugging prompt."; return; }
        await RunAsync(async () =>
        {
            var model = await _adb.GetPropertyAsync(device.Serial, "ro.product.model");
            var version = await _adb.GetPropertyAsync(device.Serial, "ro.build.version.release");
            DeviceDetails.Text = $"{model}  •  Android {version}  •  Serial: {device.Serial}";
        });
    }
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