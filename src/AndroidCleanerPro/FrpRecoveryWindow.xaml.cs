using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace AndroidCleanerPro;

/// <summary>
/// Non-invasive, informational owner-recovery workflow.
/// No account credentials are requested or stored. No FRP bypass commands exist.
/// </summary>
public partial class FrpRecoveryWindow : Window
{
    private const string GoogleRecoveryUrl = "https://accounts.google.com/signin/recovery";
    private const string GoogleDeviceProtectionUrl = "https://support.google.com/android/answer/9459346";
    private const string SamsungFrpUrl = "https://www.samsung.com/us/support/answer/ANS10003349/";
    private const string MotorolaFrpUrl = "https://help.motorola.com/hc/1797/12/global/en-us/CGd0e1460.html";
    private const string PixelFrpUrl = "https://support.google.com/pixelphone/answer/6172890";

    public FrpRecoveryWindow()
    {
        InitializeComponent();
        BrandBox.SelectedIndex = 0;
        SituationBox.SelectedIndex = 0;
        UpdateGuidance();
    }

    private string Brand =>
        (BrandBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Other Android";

    private string Situation =>
        (SituationBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Locked after reset";

    private void Selection_Changed(object sender, SelectionChangedEventArgs e) => UpdateGuidance();
    private void Checklist_Changed(object sender, RoutedEventArgs e) => UpdateChecklist();

    private void UpdateChecklist()
    {
        if (ChecklistStatus is null) return;
        var owner = OwnerCheck?.IsChecked == true;
        var credentials = NoCredentialsCheck?.IsChecked == true;
        ChecklistStatus.Text = owner && credentials
            ? "Owner authorization and credential-handling checklist acknowledged. Follow official recovery instructions."
            : "Verify the customer's authorization before performing any service.";
    }

    private void UpdateGuidance()
    {
        if (GuidanceText is null || BrandNoteText is null) return;

        GuidanceText.Text = Situation switch
        {
            "Locked after reset" =>
                "1. Ask the owner to complete setup on the PHONE using a Google Account previously added and synced to it, or a previous screen lock if the phone offers that option.\n" +
                "2. If the owner forgot the account details, open Google's official Account Recovery site on their own trusted device.\n" +
                "3. After recovering the account, retry normal setup. A recent password reset may require a security waiting period.\n" +
                "4. If the owner cannot verify their account, stop and contact the manufacturer's authorized support.",
            "Forgot Google password" =>
                "1. Have the account owner open Google Account Recovery on their own computer or phone.\n" +
                "2. Follow Google's identity-verification prompts privately. Do not enter passwords or recovery codes in this program.\n" +
                "3. After recovering the account, follow the phone's normal setup. A recent password reset may trigger a waiting period, often 24 hours on some devices.",
            "Forgot Google account email" =>
                "1. Open Google's official account recovery/username assistance on the owner's trusted device.\n" +
                "2. Use the owner's recovery email or phone number and full name as requested by Google.\n" +
                "3. Once the owner knows which previously synced account is accepted by the phone, complete normal sign-in on the phone.",
            "Preparing phone for sale or repair" =>
                "1. While the device is UNLOCKED, have the owner back up their data.\n" +
                "2. In Settings, remove the owner's Google accounts before handing the phone to someone else.\n" +
                "3. Use the manufacturer's supported factory-reset procedure from Settings, if a reset is appropriate.\n" +
                "4. Do not wipe a locked device just to try to clear FRP. A reset alone cannot prove ownership or clear the account requirement.",
            "Purchased used / previous owner unavailable" =>
                "1. Verify the purchase record and contact the seller or original owner.\n" +
                "2. Request a return or refund if the seller cannot supply a device that can be legitimately activated.\n" +
                "3. Contact the manufacturer or an authorized service center with proof of purchase to ask about available ownership-verification options.\n" +
                "4. Do not use unknown unlock services, FRP exploits or firmware wipes as substitutes for ownership verification.",
            _ => "Follow Google's official device protection and account recovery guidance."
        };

        BrandNoteText.Text = Brand switch
        {
            "Samsung" =>
                "Samsung: Its official support guidance says owners who cannot recall account information may contact an authorized Samsung Service Center with proof of purchase. Some devices offer previous screen lock verification.",
            "Motorola" =>
                "Motorola: Its device-reset guidance recommends removing Google accounts and screen lock BEFORE transferring an unlocked phone. A recovered Google password may require a waiting period.",
            "Google Pixel" =>
                "Pixel: Google's Pixel support documents account-based device protection and verification after a reset. Use the owner's previous account or supported screen lock.",
            _ =>
                "Other Android: FRP details vary by manufacturer, Android version and setup state. Follow the official support process for the exact model."
        };

        UpdateChecklist();
    }

    private void OpenOfficialUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Could not open the official link: " + ex.Message,
                "Open support website", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void GoogleRecovery_Click(object sender, RoutedEventArgs e) =>
        OpenOfficialUrl(GoogleRecoveryUrl);

    private void GoogleFrp_Click(object sender, RoutedEventArgs e) =>
        OpenOfficialUrl(GoogleDeviceProtectionUrl);

    private void Manufacturer_Click(object sender, RoutedEventArgs e)
    {
        var url = Brand switch
        {
            "Samsung" => SamsungFrpUrl,
            "Motorola" => MotorolaFrpUrl,
            "Google Pixel" => PixelFrpUrl,
            _ => GoogleDeviceProtectionUrl
        };
        OpenOfficialUrl(url);
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var content = new StringBuilder()
                .AppendLine("Android Cleaner Pro — FRP Recovery Guidance")
                .AppendLine($"Brand: {Brand}")
                .AppendLine($"Situation: {Situation}")
                .AppendLine()
                .AppendLine(GuidanceText.Text)
                .AppendLine()
                .AppendLine(BrandNoteText.Text)
                .AppendLine()
                .AppendLine($"Google account recovery: {GoogleRecoveryUrl}")
                .AppendLine($"Android device protection: {GoogleDeviceProtectionUrl}")
                .AppendLine()
                .AppendLine("No unlocking commands were run. No credentials or customer identifiers were collected.")
                .ToString();
            Clipboard.SetText(content);
            MessageBox.Show(this, "Official recovery guidance copied; no account details or device identifiers included.",
                "FRP Recovery", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Could not copy guidance: " + ex.Message,
                "Clipboard error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
