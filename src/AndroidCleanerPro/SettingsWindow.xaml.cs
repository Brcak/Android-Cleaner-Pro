using System.Diagnostics;
using System.IO;
using System.Windows;
using AndroidCleanerPro.Services;
using Microsoft.Win32;

namespace AndroidCleanerPro;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        AutoUpdateCheck.IsChecked = SettingsStore.Current.CheckForUpdatesOnStartup;
        AdbPathBox.Text = SettingsStore.Current.AdbExecutablePath;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Android Debug Bridge (adb.exe)",
            Filter = "ADB executable (adb.exe)|adb.exe",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == true)
            AdbPathBox.Text = dialog.FileName;
    }

    private void DefaultAdb_Click(object sender, RoutedEventArgs e)
    {
        AdbPathBox.Clear();
        TestResult.Text = "Bundled ADB will be selected when you save.";
    }

    private async void TestAdb_Click(object sender, RoutedEventArgs e)
    {
        TestResult.Text = "Testing the selected adb.exe...";
        var input = AdbPathBox.Text.Trim().Trim('"');
        var bundled = Path.Combine(AppContext.BaseDirectory, "platform-tools", "adb.exe");
        var path = !string.IsNullOrWhiteSpace(input) ? input :
            File.Exists(bundled) ? bundled : "adb";
        try
        {
            if (!string.IsNullOrWhiteSpace(input) &&
                (!Path.IsPathFullyQualified(input) ||
                 !string.Equals(Path.GetFileName(input), "adb.exe", StringComparison.OrdinalIgnoreCase) ||
                 !File.Exists(input)))
                throw new InvalidOperationException("Select an existing adb.exe file.");

            var info = new ProcessStartInfo(path)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            info.ArgumentList.Add("version");
            using var process = Process.Start(info)
                ?? throw new InvalidOperationException("Windows could not start ADB.");
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                throw new InvalidOperationException("ADB version check timed out.");
            }
            var output = await outputTask;
            var error = await errorTask;
            if (process.ExitCode != 0) throw new InvalidOperationException(error);
            TestResult.Text = output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault()?.Trim() ?? "ADB executable is available.";
        }
        catch (Exception ex)
        {
            TestResult.Text = "ADB test failed: " + ex.Message;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SettingsStore.Save(new AppSettings
            {
                CheckForUpdatesOnStartup = AutoUpdateCheck.IsChecked == true,
                AdbExecutablePath = AdbPathBox.Text
            });
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Unable to save settings",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
