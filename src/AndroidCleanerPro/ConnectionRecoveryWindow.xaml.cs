using System.Diagnostics;
using System.Text;
using System.Windows;
using AndroidCleanerPro.Services;

namespace AndroidCleanerPro;

/// <summary>
/// Read-only USB/ADB/Fastboot triage for owner-authorized devices.
/// Does not change Android debug authorization, install exploits or remove security controls.
/// </summary>
public partial class ConnectionRecoveryWindow : Window
{
    private readonly AdbService _adb = new();
    private readonly FirmwareService _fastboot;
    private bool _busy;

    public ConnectionRecoveryWindow()
    {
        InitializeComponent();
        _fastboot = new FirmwareService(_adb);
        Loaded += async (_, _) => await RunCheckAsync();
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        CheckButton.IsEnabled = !busy;
        RestartButton.IsEnabled = !busy;
    }

    private async Task RunCheckAsync()
    {
        if (_busy) return;
        SetBusy(true);
        StatusText.Text = "Checking connected ADB and Fastboot devices...";
        try
        {
            IReadOnlyList<DeviceInfo> adbDevices = [];
            IReadOnlyList<FastbootDeviceInfo> fastbootDevices = [];
            string? adbError = null, fastbootError = null;

            try { adbDevices = await _adb.GetDevicesAsync(); }
            catch (Exception ex) { adbError = ex.Message; }
            try { fastbootDevices = await _fastboot.GetDevicesAsync(); }
            catch (Exception ex) { fastbootError = ex.Message; }

            var sb = new StringBuilder().AppendLine("ADB:");
            if (adbError is not null) sb.AppendLine("Error: " + adbError);
            else if (adbDevices.Count == 0) sb.AppendLine("(no ADB device)");
            else foreach (var device in adbDevices)
                sb.AppendLine($"  {device.Serial}  [{device.State}]  {device.Description}");

            sb.AppendLine().AppendLine("Fastboot:");
            if (fastbootError is not null) sb.AppendLine("Error: " + fastbootError);
            else if (fastbootDevices.Count == 0) sb.AppendLine("(no Fastboot device)");
            else foreach (var device in fastbootDevices)
                sb.AppendLine($"  {device.Serial}  {device.Detail}");

            DeviceListText.Text = sb.ToString();

            if (adbDevices.Any(x => x.State == "device"))
            {
                StatusText.Text = "ADB authorized and ready.";
                RecommendationText.Text =
                    "An authorized ADB connection exists. Return to App Inventory or Security Review to investigate unwanted apps. " +
                    "If the screen is difficult to use because of popups, consider Safe Mode before removing apps one at a time.";
            }
            else if (adbDevices.Any(x => x.State == "unauthorized"))
            {
                StatusText.Text = "Phone found, but this computer is not authorized.";
                RecommendationText.Text =
                    "Unlock the phone and approve its USB debugging RSA fingerprint dialog. If adware blocks access, " +
                    "restart into Safe Mode using the phone's physical buttons or the power menu, then try the normal authorization process. " +
                    "A PC cannot legitimately grant itself access when debugging permission has not been approved.";
            }
            else if (adbDevices.Any(x => x.State == "offline"))
            {
                StatusText.Text = "ADB device is offline.";
                RecommendationText.Text =
                    "Use Restart ADB Server, unplug and reconnect a known-good data cable, check Windows USB drivers, " +
                    "then unlock the phone. An offline connection does not indicate that ADB authorization can be bypassed.";
            }
            else if (adbDevices.Any(x => x.State == "sideload"))
            {
                StatusText.Text = "Phone is in recovery sideload mode.";
                RecommendationText.Text =
                    "Sideload mode is designed for approved OTA update packages. Ordinary app inventory, shell and uninstall " +
                    "commands will not run here. Exit recovery using the phone's recovery menu; do not flash or wipe solely to remove adware.";
            }
            else if (adbDevices.Any(x => x.State == "recovery"))
            {
                StatusText.Text = "Phone is in Android Recovery.";
                RecommendationText.Text =
                    "Recovery usually has limited ADB capabilities. Do not assume access to normal app data or Android's package manager. " +
                    "Use the device's approved reboot option to return to Android; avoid Wipe Data without explicit customer authorization.";
            }
            else if (fastbootDevices.Count > 0)
            {
                StatusText.Text = "Fastboot device detected (not an ADB session).";
                RecommendationText.Text =
                    "The device's bootloader is responding. This does not enable Android USB debugging or allow installed-app removal. " +
                    "Return to a normal Android boot and use owner-approved Safe Mode or USB debugging setup. Never unlock the bootloader to force ADB; unlocking can wipe data.";
            }
            else
            {
                StatusText.Text = "No usable device connection detected.";
                RecommendationText.Text =
                    "First try a different USB data cable/port, inspect Windows Device Manager and install the manufacturer's USB driver. " +
                    "Make sure the phone is on and unlocked. If a malicious app blocks Settings, use its supported physical-key Safe Mode procedure " +
                    "to disable most third-party apps temporarily. USB debugging still requires on-device approval.";
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = "Unable to complete check.";
            RecommendationText.Text = ex.Message;
        }
        finally { SetBusy(false); }
    }

    private async void Check_Click(object sender, RoutedEventArgs e) => await RunCheckAsync();

    private async void Restart_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        if (MessageBox.Show(this,
                "Restart the PC's ADB server? Other Android repair tools may lose their connection. This will not override USB debugging authorization.",
                "Restart ADB", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        SetBusy(true);
        try
        {
            var result = await _adb.StopAllAdbProcessesAsync();
            if (result.BlockedProcesses > 0)
                throw new InvalidOperationException(
                    $"Could not stop {result.BlockedProcesses} ADB process(es). Close other Android tools first. {result.Notes}");
            await _adb.StartServerAsync();
        }
        catch (Exception ex)
        {
            StatusText.Text = "ADB restart failed: " + ex.Message;
        }
        finally { SetBusy(false); }
        await RunCheckAsync();
    }

    private void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Open documentation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void DriverGuide_Click(object sender, RoutedEventArgs e)
        => OpenUrl("https://developer.android.com/studio/run/oem-usb");

    private void AdbGuide_Click(object sender, RoutedEventArgs e)
        => OpenUrl("https://developer.android.com/tools/adb");

    private void MotorolaGuide_Click(object sender, RoutedEventArgs e)
        => OpenUrl("https://help.motorola.com/hc/8020/11/global/en-us/CGjcb1208210742.html");

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
