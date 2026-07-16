using System;
using System.Data.Common;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Radish.Repository.UnitOfWorks;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Repositories;

public class UnitOfWorkManageTest
{
    private const string PostgresConnectionStringEnvironmentVariable = "RADISH_TEST_POSTGRES_CONNECTION_STRING";

    [Fact]
    public void CommitTran_Should_Propagate_Original_CommitFailure_After_SuccessfulRollback()
    {
        using var db = new SqlSugarScope(new ConnectionConfig
        {
            ConfigId = "main",
            DbType = DbType.Sqlite,
            ConnectionString = "Data Source=:memory:",
            IsAutoCloseConnection = false,
            InitKeyType = InitKeyType.Attribute
        });
        var unitOfWork = new UnitOfWorkManage(db, NullLogger<UnitOfWorkManage>.Instance);
        var method = typeof(UnitOfWorkManageTest).GetMethod(
            nameof(TransactionBoundary),
            BindingFlags.NonPublic | BindingFlags.Static)!;

        unitOfWork.BeginTran(method);
        var innerTransaction = Assert.IsAssignableFrom<DbTransaction>(db.Ado.Transaction);
        var expectedException = new CommitFailureException("commit sentinel");
        using var failingTransaction = new CommitFailingDbTransaction(innerTransaction, expectedException);
        db.Ado.Transaction = failingTransaction;

        var actualException = Assert.Throws<CommitFailureException>(() => unitOfWork.CommitTran(method));

        Assert.Same(expectedException, actualException);
        Assert.True(failingTransaction.RollbackCalled);
        Assert.Equal(0, unitOfWork.TranCount);
        db.Ado.Transaction = null;
    }

    [Fact]
    public async Task ExecuteInSavepointAsync_Should_Keep_OuterSqliteTransaction_Usable_After_UniqueConflict()
    {
        using var db = CreateScope(DbType.Sqlite, "Data Source=:memory:");
        await db.Ado.ExecuteCommandAsync(
            "CREATE TABLE savepoint_fact (id INTEGER PRIMARY KEY, value TEXT NOT NULL UNIQUE)");
        var unitOfWork = new UnitOfWorkManage(db, NullLogger<UnitOfWorkManage>.Instance);
        unitOfWork.BeginTran();
        try
        {
            await db.Ado.ExecuteCommandAsync(
                "INSERT INTO savepoint_fact (id, value) VALUES (1, 'duplicate')");

            await Assert.ThrowsAnyAsync<Exception>(() =>
                unitOfWork.ExecuteInSavepointAsync(() =>
                    db.Ado.ExecuteCommandAsync(
                        "INSERT INTO savepoint_fact (id, value) VALUES (2, 'duplicate')")));

            await db.Ado.ExecuteCommandAsync(
                "INSERT INTO savepoint_fact (id, value) VALUES (3, 'after-conflict')");
            unitOfWork.CommitTran();
        }
        catch
        {
            if (unitOfWork.TranCount > 0)
            {
                unitOfWork.RollbackTran();
            }

            throw;
        }

        var count = Convert.ToInt32(
            await db.Ado.GetScalarAsync("SELECT COUNT(*) FROM savepoint_fact"));
        Assert.Equal(2, count);
    }

    [Fact]
    [Trait("Database", "PostgreSQL")]
    public async Task ExecuteInSavepointAsync_Should_Recover_PostgresTransaction_After_UniqueConflict()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable(
            PostgresConnectionStringEnvironmentVariable);
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(adminConnectionString),
            $"未配置 {PostgresConnectionStringEnvironmentVariable}，跳过 PostgreSQL 保存点集成测试");

        var schema = $"f3c8_submission_savepoint_{Guid.NewGuid():N}";
        using var adminDb = CreateScope(DbType.PostgreSQL, adminConnectionString!);
        await adminDb.Ado.ExecuteCommandAsync($"CREATE SCHEMA {QuoteIdentifier(schema)}");
        try
        {
            var connectionString =
                $"{adminConnectionString!.Trim().TrimEnd(';')};Search Path={schema};Pooling=false";
            using var db = CreateScope(DbType.PostgreSQL, connectionString);
            await db.Ado.ExecuteCommandAsync(
                "CREATE TABLE savepoint_fact (id bigint PRIMARY KEY, value text NOT NULL UNIQUE)");
            var unitOfWork = new UnitOfWorkManage(db, NullLogger<UnitOfWorkManage>.Instance);
            unitOfWork.BeginTran();
            try
            {
                await db.Ado.ExecuteCommandAsync(
                    "INSERT INTO savepoint_fact (id, value) VALUES (1, 'duplicate')");

                await Assert.ThrowsAnyAsync<Exception>(() =>
                    unitOfWork.ExecuteInSavepointAsync(() =>
                        db.Ado.ExecuteCommandAsync(
                            "INSERT INTO savepoint_fact (id, value) VALUES (2, 'duplicate')")));

                await db.Ado.ExecuteCommandAsync(
                    "INSERT INTO savepoint_fact (id, value) VALUES (3, 'after-conflict')");
                unitOfWork.CommitTran();
            }
            catch
            {
                if (unitOfWork.TranCount > 0)
                {
                    unitOfWork.RollbackTran();
                }

                throw;
            }

            var count = Convert.ToInt32(
                await db.Ado.GetScalarAsync("SELECT COUNT(*) FROM savepoint_fact"));
            Assert.Equal(2, count);
        }
        finally
        {
            await adminDb.Ado.ExecuteCommandAsync(
                $"DROP SCHEMA IF EXISTS {QuoteIdentifier(schema)} CASCADE");
        }
    }

    private static void TransactionBoundary()
    {
    }

    private static SqlSugarScope CreateScope(DbType dbType, string connectionString)
    {
        return dbType == DbType.PostgreSQL
            ? PostgreSqlIntegrationSqlSugarFactory.CreateScope(new ConnectionConfig
            {
                ConfigId = "main",
                DbType = dbType,
                ConnectionString = connectionString,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute
            })
            : new SqlSugarScope(new ConnectionConfig
            {
                ConfigId = "main",
                DbType = dbType,
                ConnectionString = connectionString,
                IsAutoCloseConnection = false,
                InitKeyType = InitKeyType.Attribute
            });
    }

    private static string QuoteIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"")}\"";
    }

    private sealed class CommitFailingDbTransaction : DbTransaction
    {
        private readonly DbTransaction _innerTransaction;
        private readonly CommitFailureException _commitException;

        public CommitFailingDbTransaction(
            DbTransaction innerTransaction,
            CommitFailureException commitException)
        {
            _innerTransaction = innerTransaction;
            _commitException = commitException;
        }

        public bool RollbackCalled { get; private set; }

        public override System.Data.IsolationLevel IsolationLevel => _innerTransaction.IsolationLevel;

        protected override DbConnection? DbConnection => _innerTransaction.Connection;

        public override void Commit()
        {
            throw _commitException;
        }

        public override void Rollback()
        {
            RollbackCalled = true;
            _innerTransaction.Rollback();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _innerTransaction.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    private sealed class CommitFailureException : Exception
    {
        public CommitFailureException(string message)
            : base(message)
        {
        }
    }
}
