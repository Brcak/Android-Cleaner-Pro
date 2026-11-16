using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using AndroidCleanerPro.Services;
using Microsoft.Win32;

namespace AndroidCleanerPro;

public partial class HazardDatabaseWindow : Window
{
    public HazardDatabaseWindow(string? selectedPackage = null, string? suggestedReason = null)
    {
        InitializeComponent();
        PackageBox.Text = selectedPackage ?? "";
        ReasonBox.Text = suggestedReason ?? "";
        DatabasePathText.Text = "Local database: " + HazardDatabase.DatabasePath;
        RefreshRecords();
    }

    private void RefreshRecords()
    {
        try
        {
            var hazards = HazardDatabase.GetHazards();
            var history = HazardDatabase.GetRemovalEvents();
            HazardsGrid.ItemsSource = hazards;
            HistoryGrid.ItemsSource = history;
            CountText.Text = $"{hazards.Count} reviewed packages · {history.Count} most recent removal attempts shown (up to 500)";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Could not load local database: " + ex.Message, "Hazard Database",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            CountText.Text = "Database unavailable";
        }
    }

    private void Mark_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var status = (StatusBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Review";
            HazardDatabase.SetHazard(PackageBox.Text.Trim(), status, ReasonBox.Text);
            RefreshRecords();
            MessageBox.Show(this, "Technician marking saved locally. No phone apps were changed.",
                "Hazard Database", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Unable to save marking",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void HazardsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (HazardsGrid.SelectedItem is not HazardAppRecord row) return;
        PackageBox.Text = row.PackageName;
        ReasonBox.Text = row.Reason;
        foreach (ComboBoxItem item in StatusBox.Items)
            if (string.Equals(item.Content?.ToString(), row.Status, StringComparison.Ordinal))
            {
                StatusBox.SelectedItem = item;
                break;
            }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshRecords();

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "JSON (*.json)|*.json",
            FileName = "android-cleaner-hazard-history.json"
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var payload = new
            {
                generatedUtc = DateTimeOffset.UtcNow,
                disclaimer = "Technician-reviewed labels and app removal outcomes; no independent malware verification.",
                markings = HazardDatabase.GetHazards(),
                removalEvents = HazardDatabase.GetRemovalEvents(10000)
            };
            await File.WriteAllTextAsync(dialog.FileName,
                JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
            MessageBox.Show(this,
                "Report exported. It contains package names and pseudonymous device codes; protect customer records.",
                "Export complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Export failed",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
