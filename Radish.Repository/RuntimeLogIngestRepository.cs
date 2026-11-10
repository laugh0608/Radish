using System.Data.Common;
using System.Globalization;
using Microsoft.Data.Sqlite;
using Npgsql;
using Radish.Common.OptionTool;
using Radish.IRepository;
using Radish.Model.LogModels;
using SqlSugar;

namespace Radish.Repository;

/// <summary>Log 专用批次事务；原生参数命令避免 ORM 日志 / AOP 重入，不共享业务事务。</summary>
public sealed class RuntimeLogIngestRepository(ISqlSugarClient database, RuntimeLogIngestOptions options,
    TimeProvider clock) : IRuntimeLogIngestRepository
{
    public async Task<IReadOnlyList<RuntimeLogEventRecord?>> CommitAsync(IReadOnlyList<RuntimeLogEventRecord> events,
        IReadOnlyList<RuntimeLogRejectionBucket> rejections, CancellationToken cancellationToken)
    {
        if (events.Count + rejections.Count is < 1 or > RuntimeLogIngestOptions.BatchSize)
            throw new ArgumentOutOfRangeException(nameof(events));
        if (database is not SqlSugarScope scope) throw new InvalidOperationException("Ingestion requires the Log connection scope.");
        var config = scope.GetConnectionScope("log").CurrentConnectionConfig;
        await using var connection = CreateConnection(config);
        await connection.OpenAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await using var transaction = connection is SqliteConnection sqlite
            ? sqlite.BeginTransaction(deferred: false)
            : await connection.BeginTransactionAsync(cancellationToken);
        var committed = false;
        try
        {
            var results = new List<RuntimeLogEventRecord?>();
            var buckets = rejections.ToList();
            foreach (var item in events)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await using var insert = Command(connection, transaction, """
                    INSERT INTO runtime_log_event
                    (id, deployment_id, event_id, event_code, service, level, occurred_at_utc, observed_at_utc,
                     ingested_at_utc, trace_id, operation_id, content_hash, canonical_json)
                    VALUES (@id, @deployment, @event, @code, @service, @level, @occurred, @observed,
                     @ingested, @trace, @operation, @hash, @json)
                    ON CONFLICT (deployment_id, event_id) DO NOTHING
                    """,
                    ("id", item.Id), ("deployment", item.DeploymentId), ("event", item.EventId), ("code", item.EventCode),
                    ("service", item.Service), ("level", item.Level), ("occurred", item.OccurredAtUtc),
                    ("observed", item.ObservedAtUtc), ("ingested", item.IngestedAtUtc), ("trace", item.TraceId),
                    ("operation", item.OperationId), ("hash", item.ContentHash), ("json", item.CanonicalJson));
                if (await insert.ExecuteNonQueryAsync(cancellationToken) == 1) { results.Add(item); continue; }
                await using var select = Command(connection, transaction,
                    "SELECT * FROM runtime_log_event WHERE deployment_id=@deployment AND event_id=@event",
                    ("deployment", item.DeploymentId), ("event", item.EventId));
                await using var reader = await select.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken)) throw new InvalidOperationException("Missing conflicting runtime event.");
                var existing = ReadEvent(reader);
                if (existing.ContentHash == item.ContentHash && existing.CanonicalJson == item.CanonicalJson) results.Add(existing);
                else
                {
                    results.Add(null);
                    buckets.Add(RuntimeLogRejectionBucket.Create(item.DeploymentId, item.Service, "content-conflict", clock.GetUtcNow().UtcDateTime));
                }
            }
            foreach (var bucket in buckets.GroupBy(b => b.Id).Select(g => (Value: g.First(), Count: g.Sum(b => b.AttemptCount))))
            {
                await using var command = Command(connection, transaction, """
                    INSERT INTO runtime_log_rejection_bucket (id, deployment_id, service, reason, day_utc, attempt_count)
                    VALUES (@id, @deployment, @service, @reason, @day, @count)
                    ON CONFLICT (id) DO UPDATE SET attempt_count = CASE
                      WHEN runtime_log_rejection_bucket.attempt_count > 9007199254740991 - @count THEN 9007199254740991
                      ELSE runtime_log_rejection_bucket.attempt_count + @count END
                    """, ("id", bucket.Value.Id), ("deployment", bucket.Value.DeploymentId), ("service", bucket.Value.Service),
                    ("reason", bucket.Value.Reason), ("day", bucket.Value.DayUtc), ("count", bucket.Count));
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            // 保留当前 UTC 日及之前 6 日；每次提交最多清除 200 个过期日桶；运行事件保留 / 重放时窗由 L3-B 单独接入。
            await using var cleanup = Command(connection, transaction, """
                DELETE FROM runtime_log_rejection_bucket WHERE id IN
                  (SELECT id FROM runtime_log_rejection_bucket WHERE day_utc < @before ORDER BY day_utc, id LIMIT 200)
                """, ("before", clock.GetUtcNow().UtcDateTime.Date.AddDays(-6)));
            await cleanup.ExecuteNonQueryAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await transaction.CommitAsync(cancellationToken);
            committed = true;
            return results;
        }
        finally
        {
            if (!committed)
            {
                // 失败时连接释放也是最后的回滚边界；回滚故障继续向 Service 报告不可用。
                await transaction.RollbackAsync(CancellationToken.None);
            }
        }
    }

    private DbConnection CreateConnection(ConnectionConfig config) => config.DbType switch
    {
        SqlSugar.DbType.Sqlite => new SqliteConnection(new SqliteConnectionStringBuilder(config.ConnectionString)
        {
            Mode = SqliteOpenMode.ReadWrite, DefaultTimeout = options.DatabaseTimeoutSeconds
        }.ToString()),
        SqlSugar.DbType.PostgreSQL => new NpgsqlConnection(new NpgsqlConnectionStringBuilder(config.ConnectionString)
        {
            Timeout = options.DatabaseTimeoutSeconds, CommandTimeout = options.DatabaseTimeoutSeconds
        }.ToString()),
        _ => throw new NotSupportedException("Runtime log ingestion supports SQLite and PostgreSQL only.")
    };

    private DbCommand Command(DbConnection connection, DbTransaction transaction, string sql,
        params (string Name, object? Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandTimeout = options.DatabaseTimeoutSeconds;
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            // 与既有 PostgreSQL timestamp-without-time-zone 持久化约定一致，语义始终为 UTC。
            parameter.Value = value is DateTime date ? DateTime.SpecifyKind(date, DateTimeKind.Unspecified) : value ?? DBNull.Value;
            if (value == null) parameter.DbType = System.Data.DbType.String;
            command.Parameters.Add(parameter);
        }
        return command;
    }

    private static RuntimeLogEventRecord ReadEvent(DbDataReader reader)
    {
        string Text(string column) => reader.GetString(reader.GetOrdinal(column));
        string? NullableText(string column) => reader.IsDBNull(reader.GetOrdinal(column)) ? null : Text(column);
        DateTime Utc(string column) => DateTime.SpecifyKind(Convert.ToDateTime(reader[column], CultureInfo.InvariantCulture), DateTimeKind.Utc);
        return new RuntimeLogEventRecord
        {
            Id = Text("id"), DeploymentId = Text("deployment_id"), EventId = Text("event_id"), EventCode = Text("event_code"),
            Service = Text("service"), Level = Text("level"), OccurredAtUtc = Utc("occurred_at_utc"),
            ObservedAtUtc = Utc("observed_at_utc"), IngestedAtUtc = Utc("ingested_at_utc"), TraceId = NullableText("trace_id"),
            OperationId = NullableText("operation_id"), ContentHash = Text("content_hash"), CanonicalJson = Text("canonical_json")
        };
    }
}
