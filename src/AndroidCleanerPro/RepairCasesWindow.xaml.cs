using System.IO;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using AndroidCleanerPro.Services;
using Microsoft.Win32;

namespace AndroidCleanerPro;

public partial class RepairCasesWindow : Window
{
    private long _editingId;

    public RepairCasesWindow(string? deviceModel = null)
    {
        InitializeComponent();
        ModelBox.Text = deviceModel ?? "";
        LoadJobs();
    }

    private void LoadJobs(long selectId = 0)
    {
        try
        {
            var jobs = RepairCaseStore.ReadAll();
            JobsList.ItemsSource = jobs;
            if (selectId > 0)
                JobsList.SelectedItem = jobs.FirstOrDefault(j => j.Id == selectId);
            StatusText.Text = $"{jobs.Count} recent jobs on this PC.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Could not open repair records: " + ex.Message;
        }
    }

    private void JobsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (JobsList.SelectedItem is not RepairCase job) return;
        _editingId = job.Id;
        TicketBox.Text = job.Ticket;
        ModelBox.Text = job.DeviceModel;
        ProblemBox.Text = job.ReportedProblem;
        DiagnosisBox.Text = job.Diagnosis;
        WorkBox.Text = job.WorkPerformed;
        ResultBox.Text = job.Result;
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        JobsList.SelectedItem = null;
        _editingId = 0;
        TicketBox.Clear();
        ProblemBox.Clear();
        DiagnosisBox.Clear();
        WorkBox.Clear();
        ResultBox.Clear();
        StatusText.Text = "New job: fill out a ticket and the customer's reported issue.";
    }

    private RepairCase CurrentCase() => new(
        _editingId, TicketBox.Text, ModelBox.Text, ProblemBox.Text,
        DiagnosisBox.Text, WorkBox.Text, ResultBox.Text, "");

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var id = RepairCaseStore.Save(CurrentCase());
            _editingId = id;
            LoadJobs(id);
            StatusText.Text = $"Repair job {id} saved locally.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could not save repair job",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        var job = CurrentCase();
        if (string.IsNullOrWhiteSpace(job.Ticket) || string.IsNullOrWhiteSpace(job.ReportedProblem))
        {
            MessageBox.Show(this, "Enter a job reference and reported problem before exporting.",
                "Repair report", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "Printable HTML report (*.html)|*.html",
            FileName = "android-repair-report.html"
        };
        if (dialog.ShowDialog(this) != true) return;
        static string E(string value) => WebUtility.HtmlEncode(value).Replace("\r\n", "\n")
            .Replace("\n", "<br>");
        var html = new StringBuilder("""
            <!doctype html><html lang="en"><head><meta charset="utf-8"><title>Android Repair Report</title>
            <style>body{font:15px Segoe UI,Arial,sans-serif;max-width:820px;margin:45px auto;color:#202c3a}
            h1{font-size:27px;border-bottom:3px solid #18a999;padding-bottom:14px}
            section{border-bottom:1px solid #d8e1eb;padding:14px 0}
            h2{font-size:15px;color:#275c76;margin:0 0 9px}p{line-height:1.55}
            .meta{color:#627084;font-size:13px}.notice{margin-top:25px;font-size:12px;color:#69778a}
            @media print{body{margin:12mm}}</style></head><body>
            <h1>Android Cleaner Pro · Repair Report</h1>
            """);
        html.Append("<p class=\"meta\">Report created ").Append(E(DateTimeOffset.Now.ToString("g")))
            .Append(" · Ticket ").Append(E(job.Ticket)).Append("</p>");
        foreach (var section in new[]
        {
            ("Device Model", job.DeviceModel),
            ("Reported Problem (Before)", job.ReportedProblem),
            ("Diagnosis / Evidence", job.Diagnosis),
            ("Work Performed", job.WorkPerformed),
            ("Result (After)", job.Result)
        })
        {
            html.Append("<section><h2>").Append(E(section.Item1)).Append("</h2><p>")
                .Append(E(section.Item2)).Append("</p></section>");
        }
        html.Append("""
            <p class="notice">This report reflects technician-entered notes, not independent
            malware verification. Review for customer personal data before sharing.</p>
            </body></html>
            """);
        try
        {
            await File.WriteAllTextAsync(dialog.FileName, html.ToString(), Encoding.UTF8);
            StatusText.Text = "Saved printable report: " + dialog.FileName +
                ". Open in a browser to print or save as PDF.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Unable to export report",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
