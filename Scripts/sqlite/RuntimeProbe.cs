using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using Hangfire.Storage.SQLite;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Radish.Api.Services;
using SqlSugar;

namespace Radish.SqliteDiagnostics;

// Compiled against a validated API output, then run beside each independently built host's
// original deps/runtimeconfig. Optional EF/Hangfire paths are called only when present.
public static class SqliteNativeRuntimeProbe
{
    private const string Version = "3.53.3";
    private const string SourceId = "2026-06-26 20:14:12 d4c0e51e4aeb96955b99185ab9cde75c339e2c29c3f3f12428d364a10d782c62";

    public static int Main(string[] args)
    {
        var directory = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(directory);
        try
        {
            var first = args[0];
            switch (first)
            {
                case "microsoft": CheckMicrosoft(); break;
                case "sqlsugar": CheckSqlSugar(); break;
                case "ef": CheckEf(); break;
                case "hangfire": CheckHangfire(directory); break;
                default: throw new ArgumentException("Unknown first-use probe");
            }
            CheckMicrosoft();
            CheckSqlSugar();
            var modules = LoadedSqliteModules();
            Require(modules.Length == 1, "Expected one actually loaded SQLite native module; found " + modules.Length);
            var native = modules[0];
            var moduleHash = Hash(native);
            var assemblies = new[] { "SQLitePCLRaw.core", "SQLitePCLRaw.provider.e_sqlite3", "SQLitePCLRaw.batteries_v2", "Microsoft.Data.Sqlite" }
                .Select(name => AppDomain.CurrentDomain.GetAssemblies().Single(assembly => assembly.GetName().Name == name))
                .Select(assembly => new { name = assembly.GetName().Name, path = assembly.Location, sha256 = Hash(assembly.Location) }).ToArray();
            File.WriteAllText(Path.Combine(directory, "result.json"), JsonSerializer.Serialize(new
            {
                first, sqliteVersion = Version, sourceId = SourceId, native, nativeSha256 = moduleHash,
                rawVersion = SQLitePCL.raw.sqlite3_libversion().utf8_to_string(),
                rawSourceId = SQLitePCL.raw.sqlite3_sourceid().utf8_to_string(), assemblies
            }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            foreach (var file in Directory.GetFiles(directory).Where(file => !file.EndsWith(".json", StringComparison.Ordinal))) File.Delete(file);
        }
    }

    private static string Hash(string file) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(file)));

    // Process.Modules does not enumerate dylibs on macOS. Query dyld's loaded-image
    // table after the real provider calls; do not load a separate candidate library.
    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "_dyld_image_count")]
    private static extern uint DyldImageCount();

    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "_dyld_get_image_name")]
    private static extern IntPtr DyldImageName(uint index);

    private static string[] LoadedSqliteModules()
    {
        if (!OperatingSystem.IsMacOS())
        {
            using var process = Process.GetCurrentProcess();
            return process.Modules.Cast<ProcessModule>().Where(module => module.ModuleName.Contains("e_sqlite3", StringComparison.Ordinal))
                .Select(module => module.FileName).ToArray();
        }

        var paths = new List<string>();
        var count = DyldImageCount();
        for (uint index = 0; index < count; index++)
        {
            var file = Marshal.PtrToStringUTF8(DyldImageName(index));
            if (file is not null && Path.GetFileName(file) == "libe_sqlite3.dylib") paths.Add(file);
        }
        return paths.ToArray();
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void CheckMicrosoft()
    {
        using var connection = new SqliteConnection("Data Source=:memory:;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT sqlite_version(), sqlite_source_id(), @number, @text, @empty";
        command.Parameters.AddWithValue("@number", long.MaxValue);
        command.Parameters.AddWithValue("@text", "萝卜 SQLite 🥕");
        command.Parameters.AddWithValue("@empty", DBNull.Value);
        using var reader = command.ExecuteReader();
        Require(reader.Read() && reader.GetString(0) == Version && reader.GetString(1) == SourceId
            && reader.GetInt64(2) == long.MaxValue && reader.GetString(3) == "萝卜 SQLite 🥕" && reader.IsDBNull(4), "Microsoft SQLite contract mismatch");
    }

    private static void CheckSqlSugar()
    {
        using var db = new SqlSugarClient(new ConnectionConfig
        {
            DbType = SqlSugar.DbType.Sqlite, ConnectionString = "Data Source=:memory:", IsAutoCloseConnection = true
        });
        Require(db.Ado.GetString("SELECT sqlite_version()") == Version, "SqlSugar SQLite version mismatch");
        Require(db.Ado.SqlQuerySingle<long>("SELECT @number", new SugarParameter("@number", long.MaxValue)) == long.MaxValue, "SqlSugar Int64 mismatch");
    }

    private static void CheckEf()
    {
        using var db = new DbContext(new DbContextOptionsBuilder().UseSqlite("Data Source=:memory:;Pooling=False").Options);
        db.Database.OpenConnection();
        db.Database.ExecuteSqlRaw("CREATE TABLE ef_probe(id INTEGER PRIMARY KEY, value TEXT NOT NULL)");
        using (var transaction = db.Database.BeginTransaction())
        {
            db.Database.ExecuteSqlRaw("INSERT INTO ef_probe VALUES (1, 'rollback')");
            transaction.Rollback();
        }
        Require(db.Database.SqlQueryRaw<long>("SELECT COUNT(*) AS Value FROM ef_probe").Single() == 0, "EF transaction did not roll back");
        Require(db.Database.SqlQueryRaw<string>("SELECT sqlite_version() AS Value").Single() == Version, "EF SQLite version mismatch");
    }

    private static void CheckHangfire(string directory)
    {
        var storage = new SQLiteStorage(Path.Combine(directory, "hangfire.db"), new SQLiteStorageOptions
        {
            QueuePollInterval = TimeSpan.FromMilliseconds(100)
        });
        try
        {
            JobStorage.Current = storage;
            using var connection = storage.GetConnection();
            var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Hangfire:Shop:DailyStats:Enable"] = "true"
            }).Build();
            ApiRecurringJobRegistration.Register(configuration, zone);
            var jobs = connection.GetRecurringJobs();
            var expectedIds = new[] { "reliable-outbox-dispatch", "cleanup-chat-reaction-operations", "cleanup-wiki-terminal-draft-payloads",
                "cleanup-notification-inbox", "cleanup-deleted-files", "cleanup-temp-files", "cleanup-recycle-bin", "cleanup-orphan-attachments",
                "cleanup-expired-upload-sessions", "cleanup-expired-access-tokens", "comment-highlight-stat", "retention-reward",
                "forum-post-lottery-auto-draw", "shop-cancel-timeout-orders", "shop-mark-expired-benefits", "shop-daily-stats" };
            var schedules = new[] { "*/1 * * * *", "15 4 * * *", "45 3 * * *", "30 3 * * *", "0 3 * * *", "0 * * * *", "0 4 * * *",
                "0 5 * * *", "*/15 * * * *", "0 7 * * *", "0 1 * * *", "0 2 * * 0", "*/1 * * * *", "*/10 * * * *", "0 0 * * *", "0 1 * * *" };
            var methods = new[] { "DispatchAsync", "ExecuteAsync", "ExecuteAsync", "ExecuteAsync", "CleanupDeletedFilesAsync", "CleanupTempFilesAsync",
                "CleanupRecycleBinAsync", "CleanupOrphanAttachmentsAsync", "CleanupExpiredSessionsAsync", "CleanupExpiredTokensAsync", "ExecuteAsync",
                "ExecuteAsync", "ExecuteAutoDrawAsync", "CancelTimeoutOrdersAsync", "MarkExpiredBenefitsAsync", "GenerateDailyStatsAsync" };
            var arguments = new[] { "[50]", "[500]", "[90,200]", "[200,5000]", "[30]", "[2]", "[90]", "[24]", "[]", "[]", "[null]", "[]", "[20]", "[30]", "[]", "[]" };
            Require(jobs.Count == expectedIds.Length, "Recurring registration count mismatch");
            for (var index = 0; index < expectedIds.Length; index++)
            {
                var job = jobs.Single(item => item.Id == expectedIds[index]);
                Require(job.Cron == schedules[index] && job.TimeZoneId == (index < 4 ? TimeZoneInfo.Utc.Id : zone.Id)
                    && job.Job.Method.Name == methods[index] && JsonSerializer.Serialize(job.Job.Args) == arguments[index], "Recurring registration contract mismatch: " + job.Id);
                new RecurringJobManager(storage).RemoveIfExists(job.Id);
            }
            const string queue = "sqlite-native-probe";
            var client = new BackgroundJobClient(storage);
            var marker = Path.Combine(directory, "success.txt");
            var retryMarker = Path.Combine(directory, "retry.txt");
            var successId = client.Create(Job.FromExpression(() => SqliteProbeJobs.Succeed(marker)), new EnqueuedState(queue));
            var retryId = client.Create(Job.FromExpression(() => SqliteProbeJobs.Retry(retryMarker)), new EnqueuedState(queue));
            using (var worker = new BackgroundJobServer(new BackgroundJobServerOptions
            {
                Queues = new[] { queue }, WorkerCount = 1, SchedulePollingInterval = TimeSpan.FromMilliseconds(100), ShutdownTimeout = TimeSpan.FromSeconds(5)
            }, storage))
            {
                var timer = Stopwatch.StartNew();
                while (timer.Elapsed < TimeSpan.FromSeconds(20))
                {
                    if (connection.GetStateData(successId)?.Name == "Succeeded" && connection.GetStateData(retryId)?.Name == "Succeeded") break;
                    Thread.Sleep(50);
                }
                var success = connection.GetStateData(successId);
                var retry = connection.GetStateData(retryId);
                File.WriteAllText(Path.Combine(directory, "job-states.json"), JsonSerializer.Serialize(new { success, retry }, new JsonSerializerOptions { WriteIndented = true }));
                Require(success?.Name == "Succeeded" && retry?.Name == "Succeeded", "Probe execution/retry did not succeed");
            }
            Require(File.ReadAllText(marker) == "success" && File.ReadAllText(retryMarker) == "first\nretry", "Probe execution markers mismatch");
            File.WriteAllText(Path.Combine(directory, "hangfire-result.json"), JsonSerializer.Serialize(new { registered = jobs.Count, successId, retryId, retryAttempts = 2 }));
        }
        finally
        {
            (storage as IDisposable)?.Dispose();
        }
    }
}

[Queue("sqlite-native-probe")]
public static class SqliteProbeJobs
{
    public static void Succeed(string marker) => File.WriteAllText(marker, "success");

    [AutomaticRetry(Attempts = 1, DelaysInSeconds = new[] { 0 })]
    public static void Retry(string marker)
    {
        if (!File.Exists(marker))
        {
            File.WriteAllText(marker, "first");
            throw new InvalidOperationException("planned SQLite probe retry");
        }
        File.AppendAllText(marker, "\nretry");
    }
}
