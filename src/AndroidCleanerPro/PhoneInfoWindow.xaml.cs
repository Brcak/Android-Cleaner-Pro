using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AndroidCleanerPro.Services;

namespace AndroidCleanerPro;

public partial class PhoneInfoWindow : Window
{
    private readonly PhoneDetails _details;

    public PhoneInfoWindow(PhoneDetails details)
    {
        InitializeComponent();
        _details = details;
        DeviceTitle.Text = details.Heading;
        DeviceSubtitle.Text = "Read-only information reported by the connected phone";
        ModelSummary.Text = details.ModelNumber;
        AndroidSummary.Text = details.AndroidVersion;
        BatterySummary.Text = details.BatteryLevel;
        StorageSummary.Text = details.StorageSummary;

        AddFields(IdentityRows, details.Identity);
        AddFields(SoftwareRows, details.Software);
        AddFields(BatteryRows, details.Battery);
        AddFields(HardwareRows, details.Hardware);
        AddFields(StorageRows, details.Storage);
        AddFields(DisplayRows, details.Display);
    }

    private static void AddFields(Panel container, IReadOnlyList<InfoField> fields)
    {
        foreach (var field in fields)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 11) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var label = new TextBlock
            {
                Text = field.Label,
                Foreground = new SolidColorBrush(Color.FromRgb(151, 170, 195)),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 10, 0)
            };
            var value = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(field.Value) ? "Not reported by device" : field.Value,
                Foreground = Brushes.White,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetColumn(value, 1);
            row.Children.Add(label);
            row.Children.Add(value);
            container.Children.Add(row);
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        var text = new StringBuilder();
        text.AppendLine("Android Cleaner Pro — Phone Information")
            .AppendLine(_details.Heading).AppendLine();

        AddSummary(text, "Device identity", _details.Identity);
        AddSummary(text, "Android & firmware", _details.Software);
        AddSummary(text, "Battery", _details.Battery);
        AddSummary(text, "Hardware & memory", _details.Hardware);
        AddSummary(text, "Storage", _details.Storage);
        AddSummary(text, "Display", _details.Display);

        // Do not copy optional/manual IMEI fields; they are especially sensitive identifiers.
        try
        {
            Clipboard.SetText(text.ToString());
            MessageBox.Show(this, "Device summary copied. IMEI fields were excluded.", "Phone Information",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Could not access the clipboard: " + ex.Message, "Phone Information",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static void AddSummary(StringBuilder text, string heading, IReadOnlyList<InfoField> fields)
    {
        text.AppendLine(heading);
        foreach (var field in fields)
            text.AppendLine($"{field.Label}: {field.Value}");
        text.AppendLine();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
