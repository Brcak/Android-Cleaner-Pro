using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace AndroidCleanerPro.Services;

public sealed record HazardAppRecord(
    string PackageName, string Status, string Reason, string UpdatedUtc, int Attempts,
    int Successes, string LastResult);

public sealed record RemovalEvent(
    long Id, string TimeUtc, string PackageName, string DeviceCode,
    string Origin, string Result, string Detail);

/// <summary>
/// Per-Windows-user, on-disk SQLite record of technician-reviewed app flags
/// and app uninstall outcomes. No Google accounts, IMEIs, or raw device serials
/// are stored. A locally salted opaque device code is used for audit grouping.
/// </summary>
public sealed class HazardDatabase
{
    private static readonly object Gate = new();

    public static string DatabasePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AndroidCleanerPro", "hazards.sqlite3");

    private static string SaltFile => Path.Combine(
        Path.GetDirectoryName(DatabasePath)!, "hazards-device-salt.bin");

    private static SqliteConnection Open()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath, Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString());
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            PRAGMA busy_timeout=5000;
            CREATE TABLE IF NOT EXISTS hazard_apps (
                package_name TEXT PRIMARY KEY NOT NULL,
                status TEXT NOT NULL CHECK(status IN ('Review', 'Confirmed by technician', 'Cleared')),
                reason TEXT NOT NULL,
                updated_utc TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS removal_events (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                occurred_utc TEXT NOT NULL,
                package_name TEXT NOT NULL,
                device_code TEXT NOT NULL,
                origin TEXT NOT NULL,
                outcome TEXT NOT NULL CHECK(outcome IN ('Success', 'Failed')),
                detail TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_events_package ON removal_events(package_name);
            CREATE INDEX IF NOT EXISTS idx_events_time ON removal_events(occurred_utc DESC);
            """;
        cmd.ExecuteNonQuery();
        return connection;
    }

    public static void SetHazard(string packageName, string status, string reason)
    {
        if (!IsValidPackage(packageName))
            throw new ArgumentException("Invalid Android package name.");
        if (status is not ("Review" or "Confirmed by technician" or "Cleared"))
            throw new ArgumentException("Invalid review status.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 5)
            throw new ArgumentException("Enter a reason (at least five characters).");
        lock (Gate)
        {
            using var db = Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                INSERT INTO hazard_apps(package_name,status,reason,updated_utc)
                VALUES($package,$status,$reason,$time)
                ON CONFLICT(package_name) DO UPDATE SET
                  status=excluded.status, reason=excluded.reason,
                  updated_utc=excluded.updated_utc;
                """;
            cmd.Parameters.AddWithValue("$package", packageName);
            cmd.Parameters.AddWithValue("$status", status);
            cmd.Parameters.AddWithValue("$reason", reason.Trim()[..Math.Min(reason.Trim().Length, 2000)]);
            cmd.Parameters.AddWithValue("$time", DateTimeOffset.UtcNow.ToString("O"));
            cmd.ExecuteNonQuery();
        }
    }

    public static void RecordRemoval(string packageName, string serial, string origin,
        bool success, string detail)
    {
        if (!IsValidPackage(packageName))
            throw new ArgumentException("Invalid Android package name.");
        lock (Gate)
        {
            using var db = Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                INSERT INTO removal_events(occurred_utc,package_name,device_code,origin,outcome,detail)
                VALUES($time,$package,$device,$origin,$outcome,$detail);
                """;
            cmd.Parameters.AddWithValue("$time", DateTimeOffset.UtcNow.ToString("O"));
            cmd.Parameters.AddWithValue("$package", packageName);
            cmd.Parameters.AddWithValue("$device", DeviceCode(serial));
            cmd.Parameters.AddWithValue("$origin", origin);
            cmd.Parameters.AddWithValue("$outcome", success ? "Success" : "Failed");
            cmd.Parameters.AddWithValue("$detail", detail.Length > 1500 ? detail[..1500] : detail);
            cmd.ExecuteNonQuery();
        }
    }

    public static IReadOnlyList<HazardAppRecord> GetHazards()
    {
        lock (Gate)
        {
            using var db = Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                SELECT h.package_name,h.status,h.reason,h.updated_utc,
                  (SELECT COUNT(*) FROM removal_events e WHERE e.package_name=h.package_name),
                  (SELECT COUNT(*) FROM removal_events e WHERE e.package_name=h.package_name AND outcome='Success'),
                  COALESCE((SELECT outcome FROM removal_events e WHERE e.package_name=h.package_name ORDER BY id DESC LIMIT 1),'—')
                FROM hazard_apps h ORDER BY h.updated_utc DESC;
                """;
            using var reader = cmd.ExecuteReader();
            var rows = new List<HazardAppRecord>();
            while (reader.Read())
                rows.Add(new HazardAppRecord(reader.GetString(0), reader.GetString(1),
                    reader.GetString(2), reader.GetString(3), reader.GetInt32(4),
                    reader.GetInt32(5), reader.GetString(6)));
            return rows;
        }
    }

    public static IReadOnlyList<RemovalEvent> GetRemovalEvents(int limit = 500)
    {
        lock (Gate)
        {
            using var db = Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                SELECT id,occurred_utc,package_name,device_code,origin,outcome,detail
                FROM removal_events ORDER BY id DESC LIMIT $limit;
                """;
            cmd.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 10000));
            using var reader = cmd.ExecuteReader();
            var rows = new List<RemovalEvent>();
            while (reader.Read())
                rows.Add(new RemovalEvent(reader.GetInt64(0), reader.GetString(1),
                    reader.GetString(2), reader.GetString(3), reader.GetString(4),
                    reader.GetString(5), reader.GetString(6)));
            return rows;
        }
    }

    private static bool IsValidPackage(string package)
    {
        return package.Length <= 250 &&
            System.Text.RegularExpressions.Regex.IsMatch(
                package, @"^[a-zA-Z0-9_]+(?:\.[a-zA-Z0-9_]+)+$");
    }

    private static string DeviceCode(string serial)
    {
        var salt = File.Exists(SaltFile) ? File.ReadAllBytes(SaltFile) : null;
        if (salt is null || salt.Length != 32)
        {
            salt = RandomNumberGenerator.GetBytes(32);
            File.WriteAllBytes(SaltFile, salt);
        }
        var input = Encoding.UTF8.GetBytes(serial);
        return Convert.ToHexString(HMACSHA256.HashData(salt, input))[..16];
    }
}
