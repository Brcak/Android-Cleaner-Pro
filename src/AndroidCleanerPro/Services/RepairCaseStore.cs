using System.IO;
using Microsoft.Data.Sqlite;

namespace AndroidCleanerPro.Services;

public sealed record RepairCase(
    long Id,
    string Ticket,
    string DeviceModel,
    string ReportedProblem,
    string Diagnosis,
    string WorkPerformed,
    string Result,
    string UpdatedUtc);

/// <summary>
/// Local technician case notes without automatic storage of customer identifiers.
/// Individual repair records are edited and exported only at technician direction.
/// </summary>
public static class RepairCaseStore
{
    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AndroidCleanerPro", "repair-cases.sqlite3");

    private static SqliteConnection Open()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var db = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = FilePath, Pooling = false
        }.ToString());
        db.Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            PRAGMA busy_timeout=5000;
            CREATE TABLE IF NOT EXISTS repair_cases(
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                ticket TEXT NOT NULL,
                model TEXT NOT NULL,
                problem TEXT NOT NULL,
                diagnosis TEXT NOT NULL,
                work_performed TEXT NOT NULL,
                result TEXT NOT NULL,
                updated_utc TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_repair_updated
              ON repair_cases(updated_utc DESC);
            """;
        cmd.ExecuteNonQuery();
        return db;
    }

    public static IReadOnlyList<RepairCase> ReadAll()
    {
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT id,ticket,model,problem,diagnosis,work_performed,result,updated_utc
            FROM repair_cases ORDER BY updated_utc DESC LIMIT 1000;
            """;
        using var reader = cmd.ExecuteReader();
        var rows = new List<RepairCase>();
        while (reader.Read())
            rows.Add(new RepairCase(reader.GetInt64(0), reader.GetString(1),
                reader.GetString(2), reader.GetString(3), reader.GetString(4),
                reader.GetString(5), reader.GetString(6), reader.GetString(7)));
        return rows;
    }

    public static long Save(RepairCase job)
    {
        if (string.IsNullOrWhiteSpace(job.Ticket))
            throw new InvalidOperationException("Enter a shop ticket/reference.");
        if (string.IsNullOrWhiteSpace(job.ReportedProblem))
            throw new InvalidOperationException("Enter the customer's reported problem.");
        foreach (var item in new[] { job.Ticket, job.DeviceModel, job.ReportedProblem,
                                     job.Diagnosis, job.WorkPerformed, job.Result })
            if (item.Length > 8000)
                throw new InvalidOperationException("A note exceeds the maximum allowed length.");

        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = job.Id == 0
            ? """
              INSERT INTO repair_cases(ticket,model,problem,diagnosis,work_performed,result,updated_utc)
              VALUES($ticket,$model,$problem,$diagnosis,$work,$result,$time);
              SELECT last_insert_rowid();
              """
            : """
              UPDATE repair_cases SET ticket=$ticket, model=$model, problem=$problem,
                  diagnosis=$diagnosis, work_performed=$work,
                  result=$result, updated_utc=$time
              WHERE id=$id;
              SELECT changes();
              """;
        cmd.Parameters.AddWithValue("$ticket", job.Ticket.Trim());
        cmd.Parameters.AddWithValue("$model", job.DeviceModel.Trim());
        cmd.Parameters.AddWithValue("$problem", job.ReportedProblem.Trim());
        cmd.Parameters.AddWithValue("$diagnosis", job.Diagnosis.Trim());
        cmd.Parameters.AddWithValue("$work", job.WorkPerformed.Trim());
        cmd.Parameters.AddWithValue("$result", job.Result.Trim());
        cmd.Parameters.AddWithValue("$time", DateTimeOffset.UtcNow.ToString("O"));
        if (job.Id != 0) cmd.Parameters.AddWithValue("$id", job.Id);
        var n = Convert.ToInt64(cmd.ExecuteScalar());
        if (job.Id != 0 && n != 1)
            throw new InvalidOperationException("The selected repair case no longer exists.");
        return job.Id == 0 ? n : job.Id;
    }
}
