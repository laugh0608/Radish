using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using SqlSugar;

namespace Radish.SqliteDiagnostics;

// Compiled for one fixed host capability profile against that output's original dependencies.
public static partial class SqliteNativeRuntimeProbe
{
    private const string Version = "3.53.3";
    private const string SourceId = "2026-06-26 20:14:12 d4c0e51e4aeb96955b99185ab9cde75c339e2c29c3f3f12428d364a10d782c62";

    public static int Main(string[] args)
    {
        if (args.Length != 2) throw new ArgumentException("Expected mode and evidence directory");
        var directory = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(directory);
        var stage = "first-use";
        try
        {
            var first = args[0];
            switch (first)
            {
                case "microsoft": CheckMicrosoft(); break;
                case "sqlsugar": CheckSqlSugar(); break;
#if SQLITE_PROBE_EF
                case "ef": CheckEf(); break;
#endif
#if SQLITE_PROBE_HANGFIRE
                case "hangfire": CheckHangfire(directory); break;
#endif
                default: throw new ArgumentException("Unknown first-use probe");
            }
            stage = "shared-providers";
            CheckMicrosoft();
            CheckSqlSugar();
            stage = "module-inspection";
            var selected = SqliteProbeModuleInspector.CaptureAndRequireSingle(directory);
            var native = selected.Module.FileName;
            var moduleHash = Hash(native);
            Require(moduleHash == selected.Sha256, "Loaded SQLite module changed during inspection");
            stage = "managed-assemblies";
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
            File.WriteAllText(Path.Combine(directory, "result.json"), JsonSerializer.Serialize(new
            {
                status = "failed", mode = args[0], stage, error = error.ToString(),
                moduleInventory = File.Exists(Path.Combine(directory, "module-inventory.json")) ? "module-inventory.json" : null
            }));
            Console.Error.WriteLine(error);
            return 1;
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            foreach (var name in new[] { "hangfire.db", "hangfire.db-wal", "hangfire.db-shm", "hangfire.db-journal", "success.txt", "retry.txt" })
                File.Delete(Path.Combine(directory, name));
        }
    }

    private static string Hash(string file) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(file)));

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

}
