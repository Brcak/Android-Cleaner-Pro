using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using AndroidCleanerPro.Services;
using Microsoft.Win32;

namespace AndroidCleanerPro;

public partial class AdwareScanWindow : Window
{
    private readonly AdbService _adb;
    private readonly DeviceInfo _device;
    private readonly AdwareService _scanner;
    private readonly ObservableCollection<AdwareFinding> _findings = new();
    private readonly List<ForegroundObservation> _observations = new();
    private CancellationTokenSource? _scanCancel;
    private bool _busy;

    public AdwareScanWindow(AdbService adb, DeviceInfo device)
    {
        InitializeComponent();
        _adb = adb;
        _device = device;
        _scanner = new AdwareService(adb);
        DeviceLabel.Text = $"Connected device: {device.Serial}";
        FindingsGrid.ItemsSource = _findings;
        Closing += Window_Closing;
    }

    private void Window_Closing(object? sender, CancelEventArgs e) => _scanCancel?.Cancel();

    private void SetBusy(bool busy)
    {
        _busy = busy;
        StartScanButton.IsEnabled = !busy;
        CancelScanButton.IsEnabled = busy && _scanCancel is not null;
        CaptureButton.IsEnabled = !busy;
        WatchButton.IsEnabled = !busy;
        RemoveButton.IsEnabled = !busy;
        ExportButton.IsEnabled = !busy;
    }

    private async void Scan_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        _scanCancel?.Dispose();
        _scanCancel = new CancellationTokenSource();
        _findings.Clear();
        ScanProgress.Value = 0;
        SetBusy(true);
        StatusText.Text = "Reading installed apps and inspecting advertising-related permissions. This may take a few minutes...";
        try
        {
            var progress = new Progress<AdwareScanProgress>(p =>
            {
                ScanProgress.Value = p.Total == 0 ? 100 : 100.0 * p.Finished / p.Total;
                if (p.Finding is not null) _findings.Add(p.Finding);
                StatusText.Text = $"Inspecting app {p.Finished} of {p.Total} • " +
                    $"{_findings.Count(x => x.Priority is "Review" or "Elevated review")} require review so far.";
            });

            var sorted = await _scanner.ScanAsync(_device.Serial, progress, _scanCancel.Token);
            // Recreate the ordered view after all asynchronous progress callbacks have posted.
            _findings.Clear();
            foreach (var finding in sorted) _findings.Add(finding);
            var elevated = sorted.Count(x => x.Priority == "Elevated review");
            var review = sorted.Count(x => x.Priority == "Review");
            var incomplete = sorted.Count(x => x.Priority == "Incomplete");
            ScanProgress.Value = 100;
            StatusText.Text = $"Scanned {sorted.Count} third-party apps. " +
                $"{elevated} elevated review, {review} review, {incomplete} incomplete. " +
                "These are diagnostic indicators, not confirmed malicious apps.";
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = $"Scan cancelled. {_findings.Count} app results are shown; this scan is incomplete.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Scan failed: " + ex.Message;
            MessageBox.Show(this, ex.Message, "Adware Scan", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            SetBusy(false);
            _scanCancel?.Dispose();
            _scanCancel = null;
            CancelScanButton.IsEnabled = false;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (_scanCancel is null) return;
        StatusText.Text = "Cancelling inspection after running ADB commands finish...";
        _scanCancel.Cancel();
        CancelScanButton.IsEnabled = false;
    }

    private void FindingsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var finding = FindingsGrid.SelectedItem as AdwareFinding;
        SelectedPackage.Text = finding?.Package ?? "Choose a package to review its evidence";
        SelectedInstaller.Text = finding?.Installer ?? "—";
        SelectedExplanation.Text = finding?.Explanation ?? "No package selected.";
    }

    private async void Capture_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        SetBusy(true);
        try
        {
            var observation = await _scanner.ReadForegroundAsync(_device.Serial);
            RecordObservation(observation);
            StatusText.Text = "Foreground window captured. This is a clue, not proof that the app generated an ad.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Unable to inspect foreground window: " + ex.Message;
        }
        finally { SetBusy(false); }
    }

    private async void Watch_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        SetBusy(true);
        StatusText.Text = "Watching the focused phone window for 15 seconds. Trigger the ad on the phone now.";
        try
        {
            var stopAt = DateTimeOffset.UtcNow.AddSeconds(15);
            string? lastKey = null;
            while (DateTimeOffset.UtcNow < stopAt && IsVisible)
            {
                var observation = await _scanner.ReadForegroundAsync(_device.Serial);
                var key = observation.Source + "|" + observation.Package;
                if (key != lastKey)
                {
                    RecordObservation(observation);
                    lastKey = key;
                }
                await Task.Delay(1200);
            }
            StatusText.Text = $"Foreground observation complete. {_observations.Count} observations recorded in this session.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Foreground observation failed: " + ex.Message;
        }
        finally { SetBusy(false); }
    }

    private void RecordObservation(ForegroundObservation observation)
    {
        _observations.Add(observation);
        // Keep the text short and show the latest few distinct observations.
        ForegroundText.Text = string.Join(Environment.NewLine,
            _observations.TakeLast(5).Select(o =>
                $"{o.RecordedAt:HH:mm:ss} • {o.Package} • {o.Source}"));
        var selected = _findings.FirstOrDefault(x => x.Package == observation.Package);
        if (selected is not null)
        {
            FindingsGrid.SelectedItem = selected;
            FindingsGrid.ScrollIntoView(selected);
        }
    }

    private async void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        if (FindingsGrid.SelectedItem is not AdwareFinding finding)
        {
            MessageBox.Show(this, "Choose one package to review first.", "Uninstall app",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (MessageBox.Show(this,
                $"Uninstall {finding.Package} from Android user 0?\n\n" +
                "These indicators are not proof the app shows ads or is malware. " +
                "Uninstalling may permanently delete customer app data. " +
                "Proceed only after the device owner authorizes this specific removal.",
                "Confirm owner-authorized removal", MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        SetBusy(true);
        try
        {
            var result = await _adb.UninstallAsync(_device.Serial, finding.Package);
            _findings.Remove(finding);
            StatusText.Text = $"Uninstall result: {result} — {finding.Package}";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Uninstall not completed: " + ex.Message;
            MessageBox.Show(this, ex.Message, "Uninstall error", MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally { SetBusy(false); }
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var save = new SaveFileDialog
        {
            Filter = "JSON report (*.json)|*.json",
            FileName = "android-cleaner-adware-review.json"
        };
        if (save.ShowDialog(this) != true) return;
        try
        {
            var report = new
            {
                createdUtc = DateTimeOffset.UtcNow,
                deviceSerial = _device.Serial,
                scanComplete = ScanProgress.Value >= 100,
                findings = _findings.ToArray(),
                foregroundObservations = _observations.ToArray(),
                disclaimer = "Review indicators only. Foreground window and permission status do not prove adware."
            };
            await File.WriteAllTextAsync(save.FileName,
                JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            StatusText.Text = "Saved local report: " + save.FileName;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Export failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
