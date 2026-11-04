using System;
using System.IO;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Radish.Api.Tests.Repositories;

public sealed partial class SqliteNativeContractTests
{
    [Fact]
    public void NativeIdentityAndParameterizedValues_ShouldMatchApprovedSqlite()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT sqlite_version(), sqlite_source_id(), @number, @text, @empty";
        command.Parameters.AddWithValue("@number", long.MaxValue);
        command.Parameters.AddWithValue("@text", "萝卜 SQLite 🥕");
        command.Parameters.AddWithValue("@empty", DBNull.Value);
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("3.53.3", reader.GetString(0));
        Assert.Equal("2026-06-26 20:14:12 d4c0e51e4aeb96955b99185ab9cde75c339e2c29c3f3f12428d364a10d782c62", reader.GetString(1));
        Assert.Equal(long.MaxValue, reader.GetInt64(2));
        Assert.Equal("萝卜 SQLite 🥕", reader.GetString(3));
        Assert.True(reader.IsDBNull(4));
    }

    [Fact]
    public void WalSnapshotRollbackAndCheckpoint_ShouldPreserveCommittedDataAcrossReopen()
    {
        var directory = Path.Combine(Path.GetTempPath(), "radish-sqlite-wal-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var connectionString = $"Data Source={Path.Combine(directory, "probe.db")};Pooling=False;Default Timeout=2";
        try
        {
            using (var writer = new SqliteConnection(connectionString))
            using (var reader = new SqliteConnection(connectionString))
            {
                writer.Open();
                Execute(writer, "PRAGMA journal_mode=WAL; CREATE TABLE probe(id INTEGER PRIMARY KEY, value TEXT NOT NULL); INSERT INTO probe VALUES (1, 'before');");
                reader.Open();
                using (var snapshot = reader.BeginTransaction(deferred: true))
                {
                    using var read = reader.CreateCommand();
                    read.Transaction = snapshot;
                    read.CommandText = "SELECT value FROM probe WHERE id=1";
                    Assert.Equal("before", read.ExecuteScalar());
                    Execute(writer, "UPDATE probe SET value='committed' WHERE id=1");
                    Assert.Equal("before", read.ExecuteScalar());
                    snapshot.Commit();
                }

                using (var rollback = writer.BeginTransaction())
                {
                    using var change = writer.CreateCommand();
                    change.Transaction = rollback;
                    change.CommandText = "UPDATE probe SET value='rolled-back' WHERE id=1";
                    change.ExecuteNonQuery();
                    rollback.Rollback();
                }

                reader.Close();
                using var checkpoint = writer.CreateCommand();
                checkpoint.CommandText = "PRAGMA wal_checkpoint(TRUNCATE)";
                using var result = checkpoint.ExecuteReader();
                Assert.True(result.Read());
                Assert.Equal(0, result.GetInt32(0));
            }

            using var reopened = new SqliteConnection(connectionString);
            reopened.Open();
            using var verify = reopened.CreateCommand();
            verify.CommandText = "SELECT value FROM probe WHERE id=1";
            Assert.Equal("committed", verify.ExecuteScalar());
            verify.CommandText = "PRAGMA integrity_check";
            Assert.Equal("ok", verify.ExecuteScalar());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
