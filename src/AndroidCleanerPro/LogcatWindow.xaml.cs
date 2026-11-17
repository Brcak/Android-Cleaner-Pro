using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using AndroidCleanerPro.Services;
using Microsoft.Win32;

namespace AndroidCleanerPro;

public partial class LogcatWindow : Window
{
    private readonly AdbService _adb;
    private readonly string _serial;
    private string[] _rawLines = [];
    private bool _busy;

    public LogcatWindow(AdbService adb, string serial)
    {
        InitializeComponent();
        _adb = adb;
        _serial = serial;
        DeviceLabel.Text = "Authorized device: " + serial;
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        _busy = true;
        RefreshButton.IsEnabled = false;
        StatusText.Text = "Reading a read-only Android log snapshot...";
        try
        {
            // Bounded snapshot; do not run an indefinite background capture or clear logs.
            var raw = await _adb.ExecuteAsync(_serial, "shell", "logcat",
                "-d", "-t", "500", "-v", "threadtime");
            _rawLines = raw.Split('\n').Select(x => x.TrimEnd('\r')).ToArray();
            ApplyFilters();
        }
        catch (Exception ex)
        {
            StatusText.Text = "Log read unavailable: " + ex.Message;
        }
        finally
        {
            RefreshButton.IsEnabled = true;
            _busy = false;
        }
    }

    private void Filter_Changed(object sender, SelectionChangedEventArgs e) => ApplyFilters();
    private void Search_Changed(object sender, TextChangedEventArgs e) => ApplyFilters();

    private void ApplyFilters()
    {
        if (LogText is null || StatusText is null) return;
        var severity = (SeverityBox?.SelectedItem as ComboBoxItem)?.Content?.ToString()
            ?? "All lines";
        var search = SearchBox?.Text?.Trim();
        var lines = _rawLines.Where(line =>
        {
            if (severity != "All lines")
            {
                // Android logcat -v threadtime: date/time PID TID priority tag: content
                var match = Regex.Match(line,
                    @"^\d\d-\d\d\s+\d\d:\d\d:\d\d\.\d+\s+\d+\s+\d+\s+([VDIWEAF])\s+");
                if (!match.Success) return false;
                var level = match.Groups[1].Value;
                if (severity == "Errors only" && level is not ("E" or "A" or "F"))
                    return false;
                if (severity == "Warnings + Errors" && level is not ("W" or "E" or "A" or "F"))
                    return false;
            }
            return string.IsNullOrWhiteSpace(search) ||
                line.Contains(search, StringComparison.OrdinalIgnoreCase);
        }).ToArray();
        LogText.Text = string.Join(Environment.NewLine, lines);
        StatusText.Text = $"{lines.Length:N0} visible lines / {_rawLines.Length:N0} captured. " +
            "Logs may contain private user information.";
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        if (MessageBox.Show(this,
                "Logcat can contain phone numbers, app activity, and other private customer data. " +
                "Review the visible logs and obtain customer approval before saving them.",
                "Save sensitive logs", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        var dialog = new SaveFileDialog
        {
            Filter = "Text logs (*.txt)|*.txt",
            FileName = "android-diagnostic-logcat.txt"
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            await File.WriteAllTextAsync(dialog.FileName, LogText.Text);
            StatusText.Text = "Saved visible lines locally to " + dialog.FileName;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Unable to save logs: " + ex.Message,
                "Log export", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
