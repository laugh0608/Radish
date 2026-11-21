using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Npgsql;
using Radish.Model.DtoModels;
using Radish.Model.LogModels;

namespace Radish.Repository;

public sealed partial class RuntimeLogIngestRepository
{
    private static readonly string[] TerminalCodes = ["outbox.dead_letter", "job.failed"];
    private static readonly Regex LegacyTable = new(@"\A(InformationLog|WarningLog|ErrorLog|AuditLog)_(\d{8})\z",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private const string StorageFault = "collector.storage-write-failed";

    public async Task<(IReadOnlyList<RuntimeLogEventRecord> Rows, RuntimeLogPageBoundary Boundary)> QueryAsync(
        RuntimeLogPageFilter filter, RuntimeLogPageBoundary? boundary, CancellationToken token)
    {
        await using var connection = CreateLogConnection();
        await connection.OpenAsync(token);
        var postgres = connection is NpgsqlConnection;
        if (boundary == null)
        {
            var snapshotSql = postgres ? "SELECT pg_current_snapshot()::text" : "SELECT COALESCE(MAX(rowid),0) FROM runtime_log_event";
            await using var capture = Command(connection, null, snapshotSql);
            boundary = new RuntimeLogPageBoundary(
                Convert.ToString(await capture.ExecuteScalarAsync(token), CultureInfo.InvariantCulture)!, clock.GetUtcNow().UtcDateTime);
        }

        var conditions = new List<string> { "deployment_id=@deployment", "occurred_at_utc>=@from", "occurred_at_utc<@to" };
        // Lift the tuple's 32-bit XID to the epoch nearest the captured snapshot's xmax.
        // Existing live tuples are within the wraparound distance. Frozen rows predate the snapshot.
        var snapshot = postgres
            ? """
              (runtime_log_event.xmin::text IN ('1','2') OR pg_visible_in_snapshot(
                 ((snapshot.xmax + mod(runtime_log_event.xmin::text::bigint - mod(snapshot.xmax,4294967296) + 6442450944,4294967296)
                   - 2147483648)::text)::xid8, CAST(@snapshot AS pg_snapshot)))
              """
            : "rowid<=CAST(@snapshot AS INTEGER)";
        conditions.Add(snapshot);
        var parameters = new List<(string Name, object? Value)>
        {
            ("deployment", filter.Deployment), ("from", filter.From), ("to", filter.To),
            ("snapshot", boundary.Snapshot), ("limit", filter.Limit + 1)
        };
        var services = filter.Services.Select((service, index) =>
        {
            var name = "service" + index;
            parameters.Add((name, service));
            return "@" + name;
        }).ToArray();
        if (services.Length > 0) conditions.Add("service IN (" + string.Join(',', services) + ")");
        foreach (var (column, value) in new[]
        {
            ("level", filter.Level), ("event_code", filter.EventCode), ("trace_id", filter.TraceId),
            ("operation_id", filter.OperationId)
        })
        {
            if (value == null) continue;
            conditions.Add(column + "=@" + column);
            parameters.Add((column, value));
        }
        if (filter.Keyword != null)
        {
            conditions.Add(postgres ? "strpos(event_code,@keyword)>0" : "instr(event_code,@keyword)>0");
            parameters.Add(("keyword", filter.Keyword));
        }
        if (boundary.LastTime != null)
        {
            conditions.Add("(occurred_at_utc<@last OR (occurred_at_utc=@last AND event_id<@id))");
            parameters.Add(("last", boundary.LastTime));
            parameters.Add(("id", boundary.LastId));
        }
        var from = postgres ? """
            runtime_log_event CROSS JOIN
            (SELECT pg_snapshot_xmax(CAST(@snapshot AS pg_snapshot))::text::bigint AS xmax) snapshot
            """ : "runtime_log_event";
        await using var command = Command(connection, null, "SELECT runtime_log_event.* FROM " + from + " WHERE " +
            string.Join(" AND ", conditions) + " ORDER BY occurred_at_utc DESC,event_id DESC LIMIT @limit", parameters.ToArray());
        await using var reader = await command.ExecuteReaderAsync(token);
        var rows = new List<RuntimeLogEventRecord>();
        while (await reader.ReadAsync(token)) rows.Add(ReadEvent(reader));
        return (rows, boundary);
    }

    public async Task<RuntimeLogEventRecord?> GetAsync(string deployment, string eventId, CancellationToken token)
    {
        await using var connection = CreateLogConnection();
        await connection.OpenAsync(token);
        await using var command = Command(connection, null,
            "SELECT * FROM runtime_log_event WHERE deployment_id=@deployment AND event_id=@event",
            ("deployment", deployment), ("event", eventId));
        await using var reader = await command.ExecuteReaderAsync(token);
        return await reader.ReadAsync(token) ? ReadEvent(reader) : null;
    }

    public async Task<IReadOnlyList<LegacyRuntimeLogRow>> GetLegacyAsync(DateTime from, DateTime to, bool audit, CancellationToken token)
    {
        await using var connection = CreateLogConnection();
        await connection.OpenAsync(token);
        var postgres = connection is NpgsqlConnection;
        await using var catalog = Command(connection, null, postgres
            ? "SELECT tablename FROM pg_tables WHERE schemaname=current_schema()"
            : "SELECT name FROM sqlite_master WHERE type='table'");
        var tables = new List<(string Name, string Level)>();
        await using (var reader = await catalog.ExecuteReaderAsync(token))
        {
            while (await reader.ReadAsync(token))
            {
                var name = reader.GetString(0);
                var match = LegacyTable.Match(name);
                if (!match.Success || match.Groups[1].Value.Equals("AuditLog", StringComparison.OrdinalIgnoreCase) != audit ||
                    !DateTime.TryParseExact(match.Groups[2].Value, "yyyyMMdd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) ||
                    day > to.Date || new DateTime(day.Year, day.Month, 1).AddMonths(1) <= from.Date) continue;
                var level = match.Groups[1].Value.ToLowerInvariant() switch
                {
                    "errorlog" => "Error", "warninglog" => "Warning", _ => "Info"
                };
                tables.Add((name, level));
            }
        }
        var rows = new List<LegacyRuntimeLogRow>();
        foreach (var (table, level) in tables)
        {
            // Catalog-derived identifiers have passed the fixed table-name allowlist above.
            var columns = audit ? "id,datetime,operationtype,issuccess" : "id,datetime";
            await using var query = Command(connection, null, "SELECT " + columns + " FROM \"" + table +
                "\" WHERE datetime>=@from AND datetime<@to ORDER BY datetime DESC,id DESC LIMIT 100", ("from", from), ("to", to));
            await using var reader = await query.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
                rows.Add(new LegacyRuntimeLogRow(reader.GetInt64(0), reader.GetDateTime(1),
                    audit ? reader.GetString(2) : level, audit ? reader.GetBoolean(3) : null));
        }
        return rows.OrderByDescending(row => row.RecordedAt).ThenByDescending(row => row.Id).Take(100).ToArray();
    }

    public async Task<(IReadOnlyList<RuntimeLogAlert> Rows, long Open)> GetAlertsAsync(string deployment, CancellationToken token)
    {
        await using var connection = CreateLogConnection();
        await connection.OpenAsync(token);
        await using var count = Command(connection, null,
            "SELECT COUNT(*) FROM runtime_log_alert WHERE deployment_id=@deployment AND state='Open'", ("deployment", deployment));
        var open = Convert.ToInt64(await count.ExecuteScalarAsync(token), CultureInfo.InvariantCulture);
        await using var command = Command(connection, null, """
            SELECT id,deployment_id,service,code,event_id,opened_utc,updated_utc,state,version,known_failures
            FROM runtime_log_alert WHERE deployment_id=@deployment
            ORDER BY CASE WHEN state='Resolved' THEN 1 ELSE 0 END, updated_utc DESC,id DESC LIMIT 100
            """, ("deployment", deployment));
        await using var reader = await command.ExecuteReaderAsync(token);
        var rows = new List<RuntimeLogAlert>();
        while (await reader.ReadAsync(token))
            rows.Add(new RuntimeLogAlert
            {
                Id = reader.GetString(0), DeploymentId = reader.GetString(1), Service = reader.GetString(2),
                Code = reader.GetString(3), EventId = await reader.IsDBNullAsync(4, token) ? null : reader.GetString(4),
                OpenedUtc = Utc(reader.GetDateTime(5)), UpdatedUtc = Utc(reader.GetDateTime(6)), State = reader.GetString(7),
                Version = reader.GetInt32(8), KnownFailures = reader.GetInt64(9)
            });
        return (rows, open);
    }

    public async Task<bool> TransitionAsync(string deployment, string alertId, int version, string action, string actor,
        CancellationToken token)
    {
        var state = action == "acknowledge" ? "Acknowledged" : "Resolved";
        var now = clock.GetUtcNow().UtcDateTime;
        await using var connection = CreateLogConnection();
        await connection.OpenAsync(token);
        await using var transaction = await BeginWriteAsync(connection, token);
        await using var command = Command(connection, transaction, """
            UPDATE runtime_log_alert SET state=@state,version=version+1,updated_utc=@now
            WHERE deployment_id=@deployment AND id=@id AND version=@version AND state<>'Resolved'
              AND ((@state='Acknowledged' AND state='Open') OR (@state='Resolved' AND code<>@pipeline))
            """, ("deployment", deployment), ("id", alertId), ("version", version), ("state", state),
            ("pipeline", StorageFault), ("now", now));
        if (await command.ExecuteNonQueryAsync(token) != 1) return false;
        await AuditAlertAsync(connection, transaction, alertId, version + 1, action, actor, now, token);
        await transaction.CommitAsync(token);
        return true;
    }

    public async Task<RuntimeLogPipeline?> GetPipelineAsync(string deployment, CancellationToken token)
    {
        await using var connection = CreateLogConnection();
        await connection.OpenAsync(token);
        return await ReadPipelineAsync(connection, null, deployment, token);
    }

    public async Task UpdatePipelineAsync(string deployment, RuntimeLogPipelineSample sample, CancellationToken token)
    {
        await using var connection = CreateLogConnection();
        await connection.OpenAsync(token);
        await using var transaction = await BeginWriteAsync(connection, token);
        await using var ensure = Command(connection, transaction, """
            INSERT INTO runtime_log_pipeline(deployment_id,state) VALUES(@deployment,'Unknown')
            ON CONFLICT(deployment_id) DO NOTHING
            """, ("deployment", deployment));
        await ensure.ExecuteNonQueryAsync(token);
        var pipeline = (await ReadPipelineAsync(connection, transaction, deployment, token))!;
        var previous = Deserialize(pipeline.Sample);
        if (previous != null && sample.AtUtc <= previous.AtUtc) return;
        var fault = Deserialize(pipeline.FaultSample);
        var state = pipeline.State;
        var reset = previous != null && (sample.Created < previous.Created || sample.Written < previous.Written ||
            sample.FileDelivered < previous.FileDelivered || sample.LogDelivered < previous.LogDelivered ||
            sample.InputAccepted < previous.InputAccepted);
        var failures = previous == null || reset ? 0 : sample.Created - previous.Created + sample.Written - previous.Written;
        if (previous == null || reset)
        {
            state = "Unknown";
            if (pipeline.AlertId != null) fault = sample;
        }
        else if (failures > 0)
        {
            if (pipeline.AlertId == null)
            {
                pipeline.AlertId = Digest(deployment + "|collector|" + Guid.NewGuid().ToString("N"));
                await InsertAlertAsync(connection, transaction, pipeline.AlertId, deployment, "log-collector",
                    StorageFault, null, failures, sample.AtUtc, token);
            }
            else
            {
                await using var increment = Command(connection, transaction, """
                    UPDATE runtime_log_alert SET known_failures=known_failures+@failures, updated_utc=@now,version=version+1 WHERE id=@id
                    """, ("failures", failures), ("now", sample.AtUtc), ("id", pipeline.AlertId));
                await increment.ExecuteNonQueryAsync(token);
            }
            fault = sample;
            state = "Faulted";
        }
        else if (pipeline.AlertId != null && fault != null && sample.InputAccepted > fault.InputAccepted &&
            sample.FileDelivered > fault.FileDelivered &&
            sample.LogDelivered > fault.LogDelivered && sample.Chunks == 0)
        {
            await using var resolve = Command(connection, transaction, """
                UPDATE runtime_log_alert SET state='Resolved',version=version+1,updated_utc=@now
                WHERE id=@id AND state<>'Resolved' RETURNING version
                """, ("now", sample.AtUtc), ("id", pipeline.AlertId));
            var update = await resolve.ExecuteScalarAsync(token);
            if (update != null)
                await AuditAlertAsync(connection, transaction, pipeline.AlertId,
                    Convert.ToInt32(update, CultureInfo.InvariantCulture), "recover", "system", sample.AtUtc, token);
            pipeline.AlertId = null;
            fault = null;
            state = "Healthy";
        }
        else if (pipeline.AlertId == null && sample.FileDelivered > previous.FileDelivered && sample.LogDelivered > previous.LogDelivered)
            state = "Healthy";

        await using var save = Command(connection, transaction, """
            UPDATE runtime_log_pipeline SET sample=@sample,fault_sample=@fault,alert_id=@alert,state=@state WHERE deployment_id=@deployment
            """, ("sample", JsonSerializer.Serialize(sample)), ("fault", fault == null ? null : JsonSerializer.Serialize(fault)),
            ("alert", pipeline.AlertId), ("state", state), ("deployment", deployment));
        await save.ExecuteNonQueryAsync(token);
        await transaction.CommitAsync(token);
    }

    private async Task<RuntimeLogPipeline?> ReadPipelineAsync(DbConnection connection, DbTransaction? transaction,
        string deployment, CancellationToken token)
    {
        var sql = "SELECT sample,fault_sample,alert_id,state FROM runtime_log_pipeline WHERE deployment_id=@deployment";
        if (transaction != null && connection is NpgsqlConnection) sql += " FOR UPDATE";
        await using var command = Command(connection, transaction, sql, ("deployment", deployment));
        await using var reader = await command.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token)) return null;
        string? Nullable(int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
        return new RuntimeLogPipeline { DeploymentId = deployment, Sample = Nullable(0), FaultSample = Nullable(1),
            AlertId = Nullable(2), State = reader.GetString(3) };
    }

    private async Task RegisterTerminalAlertAsync(DbConnection connection, DbTransaction transaction,
        RuntimeLogEventRecord record, CancellationToken token)
    {
        if (record.OccurredAtUtc < clock.GetUtcNow().UtcDateTime.AddMinutes(-10) || record.Level != "Error") return;
        using var payload = JsonDocument.Parse(record.CanonicalJson);
        var fatal = payload.RootElement.TryGetProperty("isFatal", out var value) && value.ValueKind == JsonValueKind.True;
        if (!fatal && !TerminalCodes.Contains(record.EventCode, StringComparer.Ordinal)) return;
        var identity = Digest(string.Join('|', record.DeploymentId, record.Service, record.EventCode, record.OperationId ?? record.EventId));
        await InsertAlertAsync(connection, transaction, identity, record.DeploymentId, record.Service, record.EventCode,
            record.EventId, 1, record.IngestedAtUtc, token);
    }

    private async Task InsertAlertAsync(DbConnection connection, DbTransaction transaction, string id, string deployment,
        string service, string code, string? eventId, long failures, DateTime utc, CancellationToken token)
    {
        await using var insert = Command(connection, transaction, """
            INSERT INTO runtime_log_alert(id,deployment_id,service,code,event_id,opened_utc,updated_utc,state,version,known_failures)
            VALUES(@id,@deployment,@service,@code,@event,@utc,@utc,'Open',1,@failures) ON CONFLICT(id) DO NOTHING
            """, ("id", id), ("deployment", deployment), ("service", service), ("code", code), ("event", eventId),
            ("utc", utc), ("failures", failures));
        await insert.ExecuteNonQueryAsync(token);
    }

    private async Task AuditAlertAsync(DbConnection connection, DbTransaction transaction, string id,
        int version, string action, string actor, DateTime utc, CancellationToken token)
    {
        await using var insert = Command(connection, transaction, """
            INSERT INTO runtime_log_alert_action(id,alert_id,actor_id,action,at_utc) VALUES(@id,@alert,@actor,@action,@at)
            """, ("id", id + ":" + version.ToString(CultureInfo.InvariantCulture)), ("alert", id), ("actor", actor),
            ("action", action), ("at", utc));
        await insert.ExecuteNonQueryAsync(token);
    }

    private static ValueTask<DbTransaction> BeginWriteAsync(DbConnection connection, CancellationToken token) =>
        connection is SqliteConnection sqlite ? ValueTask.FromResult<DbTransaction>(sqlite.BeginTransaction(deferred: false))
            : connection.BeginTransactionAsync(token);

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
    private static RuntimeLogPipelineSample? Deserialize(string? sample) =>
        sample == null ? null : JsonSerializer.Deserialize<RuntimeLogPipelineSample>(sample);
    private static string Digest(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
