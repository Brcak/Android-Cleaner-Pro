using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using AndroidCleanerPro.Services;
using Microsoft.Win32;

namespace AndroidCleanerPro;

public partial class FirmwareWindow : Window
{
    private readonly AdbService _adb;
    private readonly FirmwareService _firmware;
    private readonly string? _adbSerial;
    private bool _busy;
    private bool _flashing;
    private FastbootPreflight? _preflight;
    private FlashImageInfo? _image;

    public FirmwareWindow(AdbService adb, string? adbSerial)
    {
        InitializeComponent();
        _adb = adb;
        _firmware = new FirmwareService(adb);
        _adbSerial = adbSerial;
        EnterBootloaderButton.IsEnabled = !string.IsNullOrWhiteSpace(adbSerial);
        PartitionSelector.ItemsSource = FirmwareService.SupportedPartitions;
        PartitionSelector.SelectedIndex = 0;
        FastbootSelector.SelectionChanged += (_, _) =>
        {
            _preflight = null;
            BootloaderText.Text = "Select Read Bootloader State to check device product and lock state.";
        };
        Closing += FirmwareWindow_Closing;
        AddLog("Firmware tools initialized. No commands will flash unless the technician explicitly confirms.");
        if (_adbSerial is null)
            AddLog("No authorized ADB device selected. Boot into Fastboot on the phone and click Find Fastboot Devices.");
    }

    private void FirmwareWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_flashing)
        {
            e.Cancel = true;
            MessageBox.Show(this,
                "A flash operation is still running. Do not disconnect the phone, close the app, or interrupt power.",
                "Firmware operation in progress", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private FastbootDeviceInfo SelectedFastbootDevice =>
        FastbootSelector.SelectedItem as FastbootDeviceInfo ??
        throw new InvalidOperationException("Select a connected device in Fastboot mode.");

    private void AddLog(string message)
    {
        var safe = message.Length <= 14000 ? message : message[..14000] + "\n... output truncated";
        LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {safe}{Environment.NewLine}");
        LogBox.ScrollToEnd();
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        EnterBootloaderButton.IsEnabled = !busy && _adbSerial is not null;
        DetectButton.IsEnabled = !busy;
        InspectButton.IsEnabled = !busy;
        RebootButton.IsEnabled = !busy;
        BrowseButton.IsEnabled = !busy;
        VerifyButton.IsEnabled = !busy;
        FlashButton.IsEnabled = !busy;
        FastbootSelector.IsEnabled = !busy;
        PartitionSelector.IsEnabled = !busy;
    }

    private async Task RunActionAsync(Func<Task> action)
    {
        if (_busy) return;
        SetBusy(true);
        try { await action(); }
        catch (Exception ex)
        {
            StatusText.Text = "Operation failed: " + ex.Message;
            AddLog("ERROR: " + ex.Message);
            MessageBox.Show(this, ex.Message, "Firmware tools", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { SetBusy(false); }
    }

    private async void Detect_Click(object sender, RoutedEventArgs e)
        => await RunActionAsync(async () =>
        {
            StatusText.Text = "Looking for Fastboot devices...";
            var previous = (FastbootSelector.SelectedItem as FastbootDeviceInfo)?.Serial;
            var devices = await _firmware.GetDevicesAsync();
            FastbootSelector.ItemsSource = devices;
            FastbootSelector.SelectedItem = devices.FirstOrDefault(x => x.Serial == previous)
                ?? devices.FirstOrDefault();
            _preflight = null;
            AddLog($"Fastboot devices found: {devices.Count}.");
            StatusText.Text = devices.Count == 0
                ? "No Fastboot phone detected. Check bootloader mode, USB driver and cable."
                : $"{devices.Count} Fastboot device(s) found. Select the target and inspect its bootloader.";
        });

    private async void EnterBootloader_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        if (_adbSerial is null) return;
        if (MessageBox.Show(this,
                $"Reboot authorized ADB phone {_adbSerial} into bootloader mode?\n\n" +
                "Only proceed when the customer authorizes a service-mode reboot. " +
                "This action alone does not flash or wipe data.",
                "Enter Bootloader", MessageBoxButton.YesNo, MessageBoxImage.Question)
            != MessageBoxResult.Yes) return;
        await RunActionAsync(async () =>
        {
            await _firmware.RebootAdbToBootloaderAsync(_adbSerial);
            AddLog("ADB reboot to bootloader requested. Windows may need a Fastboot USB driver.");
            StatusText.Text = "Phone rebooting. Once Fastboot appears, click Find Fastboot Devices.";
        });
    }

    private async void Inspect_Click(object sender, RoutedEventArgs e)
        => await RunActionAsync(async () =>
        {
            var selected = SelectedFastbootDevice;
            StatusText.Text = "Reading bootloader variables...";
            _preflight = await _firmware.ReadPreflightAsync(selected.Serial);
            var p = _preflight;
            BootloaderText.Text =
                $"Product: {p.Product}\nUnlocked: {p.Unlocked}\nActive slot: {p.Slot}\nUserspace fastboot: {p.IsUserspace}\nSecure: {p.Secure}";
            AddLog($"Bootloader query for {p.Serial}: product={p.Product}; unlocked={p.Unlocked}; slot={p.Slot}; fastbootd={p.IsUserspace}; secure={p.Secure}");
            StatusText.Text = p.UnlockedConfirmed
                ? "Unlocked bootloader confirmed. Verify the exact device firmware before flashing."
                : "No confirmed unlocked bootloader. Flash operations will be blocked.";
        });

    private async void Reboot_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        FastbootDeviceInfo selected;
        try { selected = SelectedFastbootDevice; }
        catch (Exception ex) { MessageBox.Show(this, ex.Message); return; }

        if (MessageBox.Show(this, $"Reboot {selected.Serial} from Fastboot into Android?",
                "Reboot phone", MessageBoxButton.YesNo, MessageBoxImage.Question)
            != MessageBoxResult.Yes) return;

        await RunActionAsync(async () =>
        {
            await _firmware.RebootFastbootToSystemAsync(selected.Serial);
            AddLog("Fastboot reboot requested for " + selected.Serial);
            StatusText.Text = "Reboot requested. Reconnect after Android starts.";
            FastbootSelector.ItemsSource = null;
        });
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var dialog = new OpenFileDialog
        {
            Title = "Choose verified firmware image",
            Filter = "Android firmware image (*.img)|*.img",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == true)
        {
            ImagePathBox.Text = dialog.FileName;
            _image = null;
            ImageHashText.Text = "Image changed; calculate the SHA-256 before proceeding.";
        }
    }

    private string SelectedPartition =>
        PartitionSelector.SelectedItem as string ??
        throw new InvalidOperationException("Choose a target partition.");

    private async void Verify_Click(object sender, RoutedEventArgs e)
        => await RunActionAsync(async () =>
        {
            StatusText.Text = "Calculating firmware image SHA-256...";
            _image = await FirmwareService.InspectImageAsync(ImagePathBox.Text, SelectedPartition);
            var img = _image;
            ImageHashText.Text = $"File: {Path.GetFileName(img.Path)}\nSize: {img.SizeBytes:N0} bytes\nSHA-256: {img.Sha256}";
            AddLog($"Verified local image: {Path.GetFileName(img.Path)}; partition={img.Partition}; sha256={img.Sha256}");
            StatusText.Text = "Image checksum calculated. Ensure the source is official and the firmware matches your device.";
        });

    private async void Flash_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        await RunActionAsync(async () =>
        {
            var target = SelectedFastbootDevice;
            var p = await _firmware.ReadPreflightAsync(target.Serial);
            _preflight = p;
            if (!p.UnlockedConfirmed)
                throw new InvalidOperationException(
                    "Flashing blocked: bootloader state was not confirmed as unlocked. " +
                    "This tool will not unlock, relock, erase or bypass security.");
            if (p.Product == "Not reported")
                throw new InvalidOperationException(
                    "Flashing blocked: Fastboot did not report a product/codename.");
            if (p.IsUserspace.Equals("yes", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "Flashing blocked in fastbootd. Return to the bootloader to use this limited workflow.");
            if (string.IsNullOrWhiteSpace(PackageProductBox.Text) ||
                !PackageProductBox.Text.Trim().Equals(p.Product, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"Flashing blocked: enter the firmware's exact product codename and verify it matches '{p.Product}'.");

            if (AuthorizationCheck.IsChecked != true)
                throw new InvalidOperationException(
                    "Confirm owner permission, a backup and exact firmware/partition compatibility before flashing.");
            if (!ConfirmationBox.Text.Trim().Equals("FLASH " + p.Product, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Type FLASH {p.Product} exactly in the confirmation box.");

            var partition = SelectedPartition;
            var image = await FirmwareService.InspectImageAsync(ImagePathBox.Text, partition);
            if (_image is null || _image.Path != image.Path || _image.Partition != image.Partition ||
                _image.Sha256 != image.Sha256 || _image.SizeBytes != image.SizeBytes)
                throw new InvalidOperationException(
                    "Image must be verified using Calculate Image SHA-256. Re-verify if the image or partition changes.");

            var expected = ExpectedHashBox.Text.Trim();
            if (expected.Length > 0)
            {
                if (!Regex.IsMatch(expected, "^[a-fA-F0-9]{64}$") ||
                    !expected.Equals(image.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        "The firmware's published SHA-256 does not match the selected image. Flash blocked.");
            }

            var warning = $"HIGH-RISK FIRMWARE WRITE\n\n" +
                $"Phone serial: {p.Serial}\nDevice product: {p.Product}\n" +
                $"Bootloader unlocked: {p.Unlocked}\nPartition: {partition}\n" +
                $"Image: {Path.GetFileName(image.Path)}\nSize: {image.SizeBytes:N0} bytes\n" +
                $"SHA-256: {image.Sha256}\n\n" +
                (expected.Length == 0
                    ? "No trusted external checksum supplied. File authenticity is NOT established.\n\n"
                    : "Checksum matches the manually supplied expected value.\n\n") +
                "An incorrect firmware image can brick the phone, damage the boot process or destroy data. " +
                "Proceed with this single-partition flash?";
            if (MessageBox.Show(this, warning, "Final confirmation: Flash Image",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                AddLog("Firmware write cancelled at final confirmation.");
                return;
            }

            _flashing = true;
            StatusText.Text = "FLASHING — do not disconnect the device or close the application!";
            AddLog($"WRITE START: serial={p.Serial}; product={p.Product}; partition={partition}; sha256={image.Sha256}");
            try
            {
                var output = await _firmware.FlashSinglePartitionAsync(p.Serial, p.Product, image);
                AddLog("Fastboot output: " + output);
                AddLog("WRITE COMPLETED: fastboot exited successfully.");
                StatusText.Text = "Fastboot reported success. Inspect the phone before rebooting.";
                MessageBox.Show(this,
                    "Fastboot reported that the image was flashed successfully.\n" +
                    "Review the bootloader screen and confirm correct operation before rebooting.",
                    "Flash completed", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AddLog("WRITE FAILED/UNCERTAIN: " + ex.Message);
                StatusText.Text = "Flash failed or state uncertain. Do not automatically retry. Inspect device state.";
                throw;
            }
            finally { _flashing = false; }
        });
    }

    private void OpenOfficialUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Cannot open the link: " + ex.Message,
                "External documentation", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void FastbootDocs_Click(object sender, RoutedEventArgs e)
        => OpenOfficialUrl("https://source.android.com/docs/setup/test/running");

    private void SamsungDocs_Click(object sender, RoutedEventArgs e)
        => OpenOfficialUrl("https://www.samsung.com/us/support/troubleshooting/TSG01212623/");

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
