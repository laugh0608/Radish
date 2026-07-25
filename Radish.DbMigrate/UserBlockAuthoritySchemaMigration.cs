using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Radish.Common;
using Radish.Common.DbTool;
using Radish.Model;
using SqlSugar;

namespace Radish.DbMigrate;

/// <summary>建立 Main 用户屏蔽唯一真相，并吸收 Chat 旧 Direct 屏蔽事实。</summary>
internal sealed class UserBlockAuthoritySchemaMigration : ISchemaMigration
{
    private const string UserBlockTable = "UserBlock";
    private const string UserBlockOperationTable = "UserBlockOperation";
    private const string DistinctUsersConstraint = "ck_user_block_distinct_users";
    private const string SqliteInsertTrigger = "trg_user_block_distinct_insert";
    private const string SqliteUpdateTrigger = "trg_user_block_distinct_update";

    public static UserBlockAuthoritySchemaMigration Instance { get; } = new();

    public string MigrationId => "20260725_011_user_block_authority";

    public string Scope => "Main";

    public string Description => "建立用户屏蔽权威关系、操作回放及旧 Direct 屏蔽回填";

    public string ChecksumSource =>
        "20260725_011_user_block_authority|Main|" +
        "UserBlock-v1|UserBlockOperation-v1|distinct-users-check-v1|" +
        "DirectConversation-block-backfill-v1|follow-consistency-v1|ReliableOutboxMessage-v1";

    public void Apply(ISqlSugarClient db, IServiceProvider services)
    {
        var issues = Diagnose(db, services);
        if (issues.Count > 0)
        {
            throw new InvalidOperationException(
                "用户屏蔽迁移前置诊断未通过：" + string.Join("；", issues));
        }

        db.CodeFirst.InitTables<UserBlock>();
        db.CodeFirst.InitTables<UserBlockOperation>();
        EnsureDistinctUsersConstraint(db);

        var legacyBlocks = ReadValidLegacyBlocks(db, services);
        if (legacyBlocks.Count == 0)
        {
            return;
        }

        if (!db.DbMaintenance.IsAnyTable(nameof(UserFollow), false) ||
            !db.DbMaintenance.IsAnyTable(nameof(ReliableOutboxMessage), false))
        {
            throw new InvalidOperationException("Main 缺少 UserFollow 或 ReliableOutboxMessage，不能安全回填用户屏蔽。");
        }

        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        foreach (var legacy in legacyBlocks)
        {
            var block = db.Queryable<UserBlock>()
                .Where(item =>
                    item.TenantId == legacy.TenantId &&
                    item.BlockerUserId == legacy.BlockerUserId &&
                    item.BlockedUserId == legacy.BlockedUserId)
                .First();
            var changed = false;
            if (block == null)
            {
                block = new UserBlock
                {
                    Id = SnowFlakeSingle.Instance.NextId(),
                    TenantId = legacy.TenantId,
                    BlockerUserId = legacy.BlockerUserId,
                    BlockedUserId = legacy.BlockedUserId,
                    RelationshipVersion = 1,
                    IsDeleted = false,
                    CreateTime = legacy.BlockedAtUtc,
                    CreateBy = "UserBlockMigration",
                    CreateId = legacy.BlockerUserId
                };
                db.Insertable(block).ExecuteCommand();
                changed = true;
            }
            else if (block.IsDeleted)
            {
                block.IsDeleted = false;
                block.DeletedAt = null;
                block.DeletedBy = null;
                block.RelationshipVersion = Math.Max(1, block.RelationshipVersion + 1);
                block.ModifyTime = nowUtc;
                block.ModifyBy = "UserBlockMigration";
                block.ModifyId = legacy.BlockerUserId;
                db.Updateable(block).ExecuteCommand();
                changed = true;
            }

            SoftDeleteFollows(db, legacy, nowUtc);
            if (changed)
            {
                AddReliableTask(db, block, nowUtc);
            }
        }
    }

    public IReadOnlyList<string> Diagnose(ISqlSugarClient db, IServiceProvider services)
    {
        var chatDb = TryGetChatDb(services);
        if (chatDb == null)
        {
            return [];
        }
        if (!chatDb.DbMaintenance.IsAnyTable(nameof(DirectConversation), false))
        {
            return [];
        }

        var legacy = chatDb.Queryable<DirectConversation>()
            .Where(item => item.BlockedByUserId != null)
            .Select(item => new DirectConversation
            {
                Id = item.Id,
                TenantId = item.TenantId,
                ParticipantLowUserId = item.ParticipantLowUserId,
                ParticipantHighUserId = item.ParticipantHighUserId,
                BlockedByUserId = item.BlockedByUserId,
                BlockedAt = item.BlockedAt,
                CreateTime = item.CreateTime
            })
            .ToList();
        if (legacy.Count == 0)
        {
            return [];
        }

        if (!db.DbMaintenance.IsAnyTable(nameof(User), false))
        {
            return ["Main 缺少 User 表，不能验证旧 Direct 屏蔽执行者与目标。"];
        }

        var userIds = legacy
            .SelectMany(item => new[]
            {
                item.ParticipantLowUserId,
                item.ParticipantHighUserId,
                item.BlockedByUserId.GetValueOrDefault()
            })
            .Where(id => id > 0)
            .Distinct()
            .ToList();
        var users = db.Queryable<User>()
            .Where(item => userIds.Contains(item.Id))
            .Select(item => new User
            {
                Id = item.Id,
                TenantId = item.TenantId,
                IsEnable = item.IsEnable,
                IsDeleted = item.IsDeleted
            })
            .ToList();
        var userMap = users.ToDictionary(item => (item.TenantId, item.Id));
        var issues = new List<string>();
        foreach (var item in legacy)
        {
            var blockerUserId = item.BlockedByUserId.GetValueOrDefault();
            var isMember = blockerUserId == item.ParticipantLowUserId ||
                           blockerUserId == item.ParticipantHighUserId;
            var blockedUserId = blockerUserId == item.ParticipantLowUserId
                ? item.ParticipantHighUserId
                : item.ParticipantLowUserId;
            if (!isMember)
            {
                issues.Add($"DirectConversation {item.Id} 的 BlockedByUserId 不是会话成员");
                continue;
            }

            if (blockerUserId <= 0 || blockedUserId <= 0 || blockerUserId == blockedUserId)
            {
                issues.Add($"DirectConversation {item.Id} 形成无效或自屏蔽关系");
                continue;
            }

            if (!userMap.TryGetValue((item.TenantId, blockerUserId), out var blocker) ||
                !userMap.TryGetValue((item.TenantId, blockedUserId), out var blocked))
            {
                issues.Add($"DirectConversation {item.Id} 的参与者缺失或跨租户");
                continue;
            }

            if (blocker.IsDeleted || blocked.IsDeleted || !blocker.IsEnable || !blocked.IsEnable)
            {
                issues.Add($"DirectConversation {item.Id} 的参与者已删除或不可用");
            }
        }

        return issues;
    }

    public IReadOnlyList<string> Verify(ISqlSugarClient db, IServiceProvider services)
    {
        var issues = new List<string>();
        foreach (var table in new[] { UserBlockTable, UserBlockOperationTable })
        {
            if (!db.DbMaintenance.IsAnyTable(table, false))
            {
                issues.Add($"缺少表 {table}。");
            }
        }

        if (issues.Count > 0)
        {
            return issues;
        }

        if (!HasDistinctUsersConstraint(db))
        {
            issues.Add($"{UserBlockTable} 缺少执行者与目标不得相同的数据库约束。");
        }

        var selfBlocks = db.Queryable<UserBlock>()
            .Where(item => item.BlockerUserId == item.BlockedUserId)
            .Count();
        if (selfBlocks > 0)
        {
            issues.Add($"存在 {selfBlocks} 条自屏蔽关系。");
        }

        var duplicatePairs = db.Queryable<UserBlock>()
            .GroupBy(item => new { item.TenantId, item.BlockerUserId, item.BlockedUserId })
            .Having(item => SqlFunc.AggregateCount(item.Id) > 1)
            .Count();
        if (duplicatePairs > 0)
        {
            issues.Add($"存在 {duplicatePairs} 组重复屏蔽方向关系。");
        }

        var activeBlocks = db.Queryable<UserBlock>()
            .Where(item => !item.IsDeleted)
            .Select(item => new UserBlock
            {
                TenantId = item.TenantId,
                BlockerUserId = item.BlockerUserId,
                BlockedUserId = item.BlockedUserId
            })
            .ToList();
        if (db.DbMaintenance.IsAnyTable(nameof(UserFollow), false))
        {
            var inconsistentFollows = db.Queryable<UserFollow>().Where(item => !item.IsDeleted).ToList()
                .Count(follow => activeBlocks.Any(block =>
                    block.TenantId == follow.TenantId &&
                    ((block.BlockerUserId == follow.FollowerUserId &&
                      block.BlockedUserId == follow.FollowingUserId) ||
                     (block.BlockerUserId == follow.FollowingUserId &&
                      block.BlockedUserId == follow.FollowerUserId))));
            if (inconsistentFollows > 0)
            {
                issues.Add($"存在 {inconsistentFollows} 条跨越有效屏蔽关系的关注。");
            }
        }

        var legacyIssues = Diagnose(db, services);
        if (legacyIssues.Count > 0)
        {
            issues.AddRange(legacyIssues);
            return issues;
        }

        foreach (var legacy in ReadValidLegacyBlocks(db, services))
        {
            if (!activeBlocks.Any(block =>
                    block.TenantId == legacy.TenantId &&
                    block.BlockerUserId == legacy.BlockerUserId &&
                    block.BlockedUserId == legacy.BlockedUserId))
            {
                issues.Add(
                    $"旧 Direct 屏蔽未映射：Tenant={legacy.TenantId}, " +
                    $"Blocker={legacy.BlockerUserId}, Blocked={legacy.BlockedUserId}。");
            }
        }

        return issues;
    }

    private static IReadOnlyList<LegacyBlock> ReadValidLegacyBlocks(
        ISqlSugarClient mainDb,
        IServiceProvider services)
    {
        var chatDb = TryGetChatDb(services);
        if (chatDb == null)
        {
            return [];
        }
        if (!chatDb.DbMaintenance.IsAnyTable(nameof(DirectConversation), false))
        {
            return [];
        }

        return chatDb.Queryable<DirectConversation>()
            .Where(item => item.BlockedByUserId != null)
            .Select(item => new DirectConversation
            {
                TenantId = item.TenantId,
                ParticipantLowUserId = item.ParticipantLowUserId,
                ParticipantHighUserId = item.ParticipantHighUserId,
                BlockedByUserId = item.BlockedByUserId,
                BlockedAt = item.BlockedAt,
                CreateTime = item.CreateTime
            })
            .ToList()
            .Select(item =>
            {
                var blockerUserId = item.BlockedByUserId!.Value;
                var blockedUserId = blockerUserId == item.ParticipantLowUserId
                    ? item.ParticipantHighUserId
                    : item.ParticipantLowUserId;
                return new LegacyBlock(
                    item.TenantId,
                    blockerUserId,
                    blockedUserId,
                    NormalizeUtc(item.BlockedAt ?? item.CreateTime));
            })
            .GroupBy(item => new { item.TenantId, item.BlockerUserId, item.BlockedUserId })
            .Select(group => group.OrderBy(item => item.BlockedAtUtc).First())
            .OrderBy(item => item.TenantId)
            .ThenBy(item => item.BlockerUserId)
            .ThenBy(item => item.BlockedUserId)
            .ToList();
    }

    private static void SoftDeleteFollows(ISqlSugarClient db, LegacyBlock legacy, DateTime nowUtc)
    {
        db.Updateable<UserFollow>()
            .SetColumns(item => item.IsDeleted == true)
            .SetColumns(item => item.DeletedAt == nowUtc)
            .SetColumns(item => item.DeletedBy == "UserBlockMigration")
            .SetColumns(item => item.ModifyTime == nowUtc)
            .SetColumns(item => item.ModifyBy == "UserBlockMigration")
            .SetColumns(item => item.ModifyId == legacy.BlockerUserId)
            .Where(item =>
                item.TenantId == legacy.TenantId &&
                !item.IsDeleted &&
                ((item.FollowerUserId == legacy.BlockerUserId &&
                  item.FollowingUserId == legacy.BlockedUserId) ||
                 (item.FollowerUserId == legacy.BlockedUserId &&
                  item.FollowingUserId == legacy.BlockerUserId)))
            .ExecuteCommand();
    }

    private static void AddReliableTask(ISqlSugarClient db, UserBlock block, DateTime nowUtc)
    {
        var idempotencyKey =
            $"user-block:migration:{block.TenantId}:{block.BlockerUserId}:{block.BlockedUserId}:v{block.RelationshipVersion}";
        if (db.Queryable<ReliableOutboxMessage>()
            .Any(item => item.TenantId == block.TenantId && item.IdempotencyKey == idempotencyKey))
        {
            return;
        }

        var payload = new UserBlockRelationshipChangedTaskPayload(
            block.TenantId,
            block.Id,
            UserBlockRelationshipEventTypes.Blocked,
            block.BlockerUserId,
            block.BlockedUserId,
            block.RelationshipVersion,
            nowUtc);
        db.Insertable(new ReliableOutboxMessage
        {
            Id = SnowFlakeSingle.Instance.NextId(),
            TenantId = block.TenantId,
            TaskType = ReliableTaskTypes.UserBlockRelationshipChanged,
            SchemaVersion = 1,
            IdempotencyKey = idempotencyKey,
            AggregateType = nameof(UserBlock),
            AggregateId = block.Id.ToString(CultureInfo.InvariantCulture),
            PayloadJson = JsonSerializer.Serialize(payload),
            Status = ReliableOutboxStatuses.Pending,
            AttemptCount = 0,
            MaxAttempts = 6,
            OccurredAtUtc = nowUtc,
            AvailableAtUtc = nowUtc,
            CreateTime = nowUtc
        }).ExecuteCommand();
    }

    private static ISqlSugarClient? TryGetChatDb(IServiceProvider services)
    {
        if (services.GetService<ISqlSugarClient>() is not SqlSugarScope scope)
        {
            return null;
        }

        try
        {
            return scope.GetConnectionScope(SqlSugarConst.ChatConfigId.ToLowerInvariant());
        }
        catch (SqlSugarException)
        {
            return null;
        }
    }

    private static void EnsureDistinctUsersConstraint(ISqlSugarClient db)
    {
        var blockerColumn = DatabaseIdentifierResolver.ResolveColumn(
                                db,
                                UserBlockTable,
                                nameof(UserBlock.BlockerUserId))
                            ?? throw new InvalidOperationException(
                                $"{UserBlockTable}.{nameof(UserBlock.BlockerUserId)} 不存在。");
        var blockedColumn = DatabaseIdentifierResolver.ResolveColumn(
                                db,
                                UserBlockTable,
                                nameof(UserBlock.BlockedUserId))
                            ?? throw new InvalidOperationException(
                                $"{UserBlockTable}.{nameof(UserBlock.BlockedUserId)} 不存在。");
        switch (db.CurrentConnectionConfig.DbType)
        {
            case DbType.PostgreSQL:
                if (!HasDistinctUsersConstraint(db))
                {
                    db.Ado.ExecuteCommand(
                        $"ALTER TABLE {Quote(blockerColumn.TableName)} " +
                        $"ADD CONSTRAINT {Quote(DistinctUsersConstraint)} " +
                        $"CHECK ({Quote(blockerColumn.ColumnName)} <> {Quote(blockedColumn.ColumnName)})");
                }

                break;
            case DbType.Sqlite:
                db.Ado.ExecuteCommand(
                    $"CREATE TRIGGER IF NOT EXISTS {Quote(SqliteInsertTrigger)} " +
                    $"BEFORE INSERT ON {Quote(blockerColumn.TableName)} " +
                    $"WHEN NEW.{Quote(blockerColumn.ColumnName)} = NEW.{Quote(blockedColumn.ColumnName)} " +
                    "BEGIN SELECT RAISE(ABORT, 'UserBlock users must differ'); END");
                db.Ado.ExecuteCommand(
                    $"CREATE TRIGGER IF NOT EXISTS {Quote(SqliteUpdateTrigger)} " +
                    $"BEFORE UPDATE OF {Quote(blockerColumn.ColumnName)}, {Quote(blockedColumn.ColumnName)} " +
                    $"ON {Quote(blockerColumn.TableName)} " +
                    $"WHEN NEW.{Quote(blockerColumn.ColumnName)} = NEW.{Quote(blockedColumn.ColumnName)} " +
                    "BEGIN SELECT RAISE(ABORT, 'UserBlock users must differ'); END");
                break;
            default:
                throw new InvalidOperationException(
                    $"用户屏蔽迁移不支持数据库 {db.CurrentConnectionConfig.DbType}。");
        }
    }

    private static bool HasDistinctUsersConstraint(ISqlSugarClient db)
    {
        var tableName = DatabaseIdentifierResolver.ResolveColumn(
            db,
            UserBlockTable,
            nameof(UserBlock.Id))?.TableName;
        if (tableName == null)
        {
            return false;
        }

        return db.CurrentConnectionConfig.DbType switch
        {
            DbType.PostgreSQL => Convert.ToInt64(
                db.Ado.GetScalar(
                    "SELECT COUNT(*) FROM pg_constraint c " +
                    "JOIN pg_class t ON t.oid=c.conrelid " +
                    "JOIN pg_namespace n ON n.oid=t.relnamespace " +
                    "WHERE c.conname=@name AND t.relname=@tableName AND n.nspname=current_schema()",
                    new SugarParameter("@name", DistinctUsersConstraint),
                    new SugarParameter("@tableName", tableName)),
                CultureInfo.InvariantCulture) > 0,
            DbType.Sqlite => Convert.ToInt64(
                db.Ado.GetScalar(
                    "SELECT COUNT(*) FROM sqlite_master WHERE type='trigger' AND name IN (@insertName,@updateName)",
                    new SugarParameter("@insertName", SqliteInsertTrigger),
                    new SugarParameter("@updateName", SqliteUpdateTrigger)),
                CultureInfo.InvariantCulture) == 2,
            _ => false
        };
    }

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static string Quote(string identifier) =>
        $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private sealed record LegacyBlock(
        long TenantId,
        long BlockerUserId,
        long BlockedUserId,
        DateTime BlockedAtUtc);
}
