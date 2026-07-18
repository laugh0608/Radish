using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Radish.Common;
using Radish.Model;
using SqlSugar;

namespace Radish.DbMigrate;

/// <summary>建立通知稳定定义、用户分组、权威摘要和分类偏好。</summary>
internal sealed class NotificationInboxSchemaMigration : ISchemaMigration
{
    private const string UserNotificationTable = "UserNotification";
    private const string SettingTable = "NotificationSetting";
    private const string GroupTable = "NotificationInboxGroup";
    private const string StateTable = "NotificationInboxState";
    private static readonly Regex NotificationTablePattern = new(
        "^Notification_[0-9]{8}$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static NotificationInboxSchemaMigration Instance { get; } = new();

    public string MigrationId => "20260718_003_notification_inbox_governance";

    public string Scope => "Message";

    public string Description => "建立通知定义、结构化目标、用户分组、权威摘要与分类偏好";

    public string ChecksumSource =>
        "20260718_003_notification_inbox_governance|Message|" +
        "notification-definition-target-v1|user-relation-group-cutoff-v1|" +
        "inbox-group-state-v1|category-preference-v1|history-conservation-v1";

    public void Apply(ISqlSugarClient db, IServiceProvider services)
    {
        var safetyIssues = Diagnose(db, services);
        if (safetyIssues.Count > 0)
        {
            throw new InvalidOperationException(
                "通知收件箱迁移前置诊断未通过：" + string.Join("；", safetyIssues));
        }

        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var legacySettings = ReadLegacySettings(db);
        var notificationTables = GetNotificationTables(db);
        if (notificationTables.Count == 0)
        {
            db.CodeFirst.InitTables<Notification>();
            notificationTables = GetNotificationTables(db);
        }

        foreach (var tableName in notificationTables)
        {
            EnsureNotificationColumns(db, tableName);
            BackfillNotificationTable(db, tableName);
        }

        EnsureUserNotificationColumns(db);
        db.CodeFirst.InitTables<UserNotification>();
        if (HasLegacySettingSchema(db))
        {
            var settingId = DatabaseIdentifierResolver.ResolveColumn(db, SettingTable, "Id")
                            ?? throw new InvalidOperationException($"{SettingTable}.Id 不存在。");
            db.Ado.ExecuteCommand($"DROP TABLE {Quote(settingId.TableName)}");
        }

        db.CodeFirst.InitTables<NotificationSetting>();
        db.CodeFirst.InitTables<NotificationInboxGroup>();
        db.CodeFirst.InitTables<NotificationInboxState>();

        RestoreLegacySettings(db, legacySettings, nowUtc);
        BackfillGroupsAndRelations(db, notificationTables, nowUtc);
        RebuildStates(db, nowUtc);
    }

    public IReadOnlyList<string> Diagnose(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        var issues = new List<string>();
        var notificationTables = GetNotificationTables(db);
        var identities = ReadNotificationIdentities(db, notificationTables);

        var duplicateIds = identities
            .GroupBy(item => new { item.TenantId, item.Id })
            .Count(group => group.Count() > 1);
        if (duplicateIds > 0)
        {
            issues.Add($"发现 {duplicateIds} 组跨月重复 NotificationId，需先治理后迁移");
        }

        var duplicateBusinessKeys = identities
            .Where(item => !string.IsNullOrWhiteSpace(item.BusinessKey))
            .GroupBy(item => new { item.TenantId, item.BusinessKey })
            .Count(group => group.Count() > 1);
        if (duplicateBusinessKeys > 0)
        {
            issues.Add($"发现 {duplicateBusinessKeys} 组跨月重复 BusinessKey，需先治理后迁移");
        }

        if (db.DbMaintenance.IsAnyTable(UserNotificationTable, false))
        {
            var relationNotificationIds = db.Queryable<UserNotification>()
                .Select(relation => new NotificationIdentity
                {
                    Id = relation.NotificationId,
                    TenantId = relation.TenantId
                })
                .ToList();
            var eventKeys = identities.Select(item => (item.TenantId, item.Id)).ToHashSet();
            var orphanCount = relationNotificationIds.Count(item => !eventKeys.Contains((item.TenantId, item.Id)));
            if (orphanCount > 0)
            {
                issues.Add($"发现 {orphanCount} 条孤立 UserNotification 关系，需先治理后迁移");
            }

            var nonDefaultDeliveryCount = CountNonDefaultDeliveryRows(db);
            if (nonDefaultDeliveryCount > 0)
            {
                issues.Add($"发现 {nonDefaultDeliveryCount} 条非默认 delivery 状态，不能按虚假字段静默删除");
            }
        }

        issues.AddRange(DiagnoseLegacySettings(db));
        return issues;
    }

    public IReadOnlyList<string> Verify(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        var issues = new List<string>();
        foreach (var tableName in new[] { UserNotificationTable, SettingTable, GroupTable, StateTable })
        {
            if (!db.DbMaintenance.IsAnyTable(tableName, false))
            {
                issues.Add($"{tableName} 表不存在。");
            }
        }

        foreach (var tableName in GetNotificationTables(db))
        {
            foreach (var columnName in NotificationColumns.Keys)
            {
                if (DatabaseIdentifierResolver.ResolveColumn(db, tableName, columnName) == null)
                {
                    issues.Add($"{tableName}.{columnName} 列不存在。");
                }
            }

            if (DatabaseIdentifierResolver.ResolveColumn(db, tableName, nameof(Notification.OccurredAtUtc)) is
                { } occurredColumn)
            {
                var nullCount = Convert.ToInt64(db.Ado.GetScalar(
                    $"SELECT COUNT(*) FROM {Quote(tableName)} WHERE {Quote(occurredColumn.ColumnName)} IS NULL"),
                    CultureInfo.InvariantCulture);
                if (nullCount > 0)
                {
                    issues.Add($"{tableName} 仍有 {nullCount} 条通知缺少 OccurredAtUtc。");
                }
            }
        }

        if (issues.Count > 0 || !db.DbMaintenance.IsAnyTable(UserNotificationTable, false))
        {
            return issues;
        }

        foreach (var columnName in new[] { nameof(UserNotification.InboxGroupId), nameof(UserNotification.OccurredAtUtc) })
        {
            if (DatabaseIdentifierResolver.ResolveColumn(db, UserNotificationTable, columnName) == null)
            {
                issues.Add($"{UserNotificationTable}.{columnName} 列不存在。");
            }
        }

        if (DatabaseIdentifierResolver.ResolveColumn(
                db,
                GroupTable,
                nameof(NotificationInboxGroup.DistinctTriggerCount)) == null)
        {
            issues.Add($"{GroupTable}.{nameof(NotificationInboxGroup.DistinctTriggerCount)} 列不存在。");
        }

        foreach (var indexName in new[]
                 {
                     "idx_user_notification_group_unread",
                     "idx_notification_setting_user_category",
                     "idx_notification_inbox_group_key",
                     "idx_notification_inbox_group_page",
                     "idx_notification_inbox_group_category",
                     "idx_notification_inbox_state_user"
                 })
        {
            if (!db.DbMaintenance.IsAnyIndex(indexName))
            {
                issues.Add($"缺少索引 {indexName}。");
            }
        }

        if (issues.Count > 0)
        {
            return issues;
        }

        var groups = db.Queryable<NotificationInboxGroup>().ToList();
        var groupById = groups.ToDictionary(group => group.Id);
        var relations = db.Queryable<UserNotification>().ToList();
        var notificationById = GetNotificationTables(db)
            .SelectMany(tableName => db.Queryable<Notification>().AS(tableName).ToList())
            .ToDictionary(notification => (notification.TenantId, notification.Id));
        foreach (var relation in relations)
        {
            if (!groupById.TryGetValue(relation.InboxGroupId, out var group) ||
                group.TenantId != relation.TenantId ||
                group.UserId != relation.UserId)
            {
                issues.Add($"UserNotification {relation.Id} 未关联到同租户同用户的有效分组。");
            }
        }

        foreach (var group in groups)
        {
            var groupRelations = relations.Where(relation =>
                relation.TenantId == group.TenantId &&
                relation.UserId == group.UserId &&
                relation.InboxGroupId == group.Id).ToList();
            var unreadCount = groupRelations.LongCount(relation => !relation.IsRead && !relation.IsDeleted);
            var distinctTriggerCount = groupRelations
                .Select(relation => notificationById.GetValueOrDefault((relation.TenantId, relation.NotificationId))?.TriggerId)
                .Where(triggerId => triggerId.HasValue)
                .Distinct()
                .LongCount();
            if (group.OccurrenceCount != groupRelations.Count ||
                group.UnreadOccurrenceCount != unreadCount ||
                group.DistinctTriggerCount != distinctTriggerCount)
            {
                issues.Add($"通知分组 {group.Id} 的事件数、未读数或触发者数不守恒。");
            }
        }

        var states = db.Queryable<NotificationInboxState>().ToList();
        foreach (var userGroups in groups.GroupBy(group => new { group.TenantId, group.UserId }))
        {
            var state = states.SingleOrDefault(item =>
                item.TenantId == userGroups.Key.TenantId && item.UserId == userGroups.Key.UserId);
            var visibleUnreadGroups = userGroups.Where(group => !group.IsDeleted && group.UnreadOccurrenceCount > 0).ToList();
            if (state == null ||
                state.UnreadGroupCount != visibleUnreadGroups.Count ||
                state.UnreadOccurrenceCount != visibleUnreadGroups.Sum(group => group.UnreadOccurrenceCount))
            {
                issues.Add($"用户 {userGroups.Key.TenantId}/{userGroups.Key.UserId} 的通知摘要不守恒。");
            }
        }

        return issues;
    }

    private static readonly IReadOnlyDictionary<string, NotificationColumnDefinition> NotificationColumns =
        new Dictionary<string, NotificationColumnDefinition>(StringComparer.Ordinal)
        {
            [nameof(Notification.Category)] = new("text", "NOT NULL DEFAULT 'System'"),
            [nameof(Notification.TemplateKey)] = new("text", "NOT NULL DEFAULT 'notification.legacy'"),
            [nameof(Notification.TemplateArgumentsJson)] = new("text", "NULL"),
            [nameof(Notification.TargetKind)] = new("text", "NOT NULL DEFAULT 'None'"),
            [nameof(Notification.TargetDataJson)] = new("text", "NULL"),
            [nameof(Notification.TargetSchemaVersion)] = new("integer", "NOT NULL DEFAULT 1"),
            [nameof(Notification.OccurredAtUtc)] = new("datetime", "NULL")
        };

    private static void EnsureNotificationColumns(ISqlSugarClient db, string tableName)
    {
        var idColumn = DatabaseIdentifierResolver.ResolveColumn(db, tableName, nameof(Notification.Id))
                       ?? throw new InvalidOperationException($"{tableName}.Id 不存在。");
        foreach (var (configuredName, definition) in NotificationColumns)
        {
            if (DatabaseIdentifierResolver.ResolveColumn(db, tableName, configuredName) != null)
            {
                continue;
            }

            var physicalName = IsLowercase(idColumn.ColumnName)
                ? configuredName.ToLowerInvariant()
                : configuredName;
            var dataType = configuredName == nameof(Notification.OccurredAtUtc) &&
                           db.CurrentConnectionConfig.DbType == DbType.PostgreSQL
                ? "timestamp with time zone"
                : definition.DataType;
            db.Ado.ExecuteCommand(
                $"ALTER TABLE {Quote(tableName)} ADD COLUMN {Quote(physicalName)} {dataType} {definition.Constraints}");
        }
    }

    private static void EnsureUserNotificationColumns(ISqlSugarClient db)
    {
        if (!db.DbMaintenance.IsAnyTable(UserNotificationTable, false))
        {
            return;
        }

        var idColumn = DatabaseIdentifierResolver.ResolveColumn(db, UserNotificationTable, nameof(UserNotification.Id))
                       ?? throw new InvalidOperationException($"{UserNotificationTable}.Id 不存在。");
        var lowercase = IsLowercase(idColumn.ColumnName);
        if (DatabaseIdentifierResolver.ResolveColumn(
                db,
                UserNotificationTable,
                nameof(UserNotification.InboxGroupId)) == null)
        {
            var columnName = lowercase
                ? nameof(UserNotification.InboxGroupId).ToLowerInvariant()
                : nameof(UserNotification.InboxGroupId);
            var dataType = db.CurrentConnectionConfig.DbType == DbType.PostgreSQL ? "bigint" : "integer";
            db.Ado.ExecuteCommand(
                $"ALTER TABLE {Quote(idColumn.TableName)} ADD COLUMN {Quote(columnName)} {dataType} NOT NULL DEFAULT 0");
        }

        if (DatabaseIdentifierResolver.ResolveColumn(
                db,
                UserNotificationTable,
                nameof(UserNotification.OccurredAtUtc)) == null)
        {
            var columnName = lowercase
                ? nameof(UserNotification.OccurredAtUtc).ToLowerInvariant()
                : nameof(UserNotification.OccurredAtUtc);
            var dataType = db.CurrentConnectionConfig.DbType == DbType.PostgreSQL
                ? "timestamp with time zone"
                : "datetime";
            var defaultValue = db.CurrentConnectionConfig.DbType == DbType.PostgreSQL
                ? "'1970-01-01 00:00:00+00'"
                : "'1970-01-01 00:00:00'";
            db.Ado.ExecuteCommand(
                $"ALTER TABLE {Quote(idColumn.TableName)} ADD COLUMN {Quote(columnName)} {dataType} NOT NULL DEFAULT {defaultValue}");
        }
    }

    private static void BackfillNotificationTable(ISqlSugarClient db, string tableName)
    {
        var rows = db.Queryable<Notification>()
            .AS(tableName)
            .Where(notification =>
                notification.TemplateKey == "notification.legacy" ||
                notification.TemplateKey == null ||
                notification.TemplateKey == string.Empty)
            .ToList();
        var columns = new[]
        {
            nameof(Notification.Id),
            nameof(Notification.Type),
            nameof(Notification.Category),
            nameof(Notification.TemplateKey),
            nameof(Notification.TemplateArgumentsJson),
            nameof(Notification.TargetKind),
            nameof(Notification.TargetDataJson),
            nameof(Notification.TargetSchemaVersion),
            nameof(Notification.OccurredAtUtc)
        }.ToDictionary(
            name => name,
            name => DatabaseIdentifierResolver.ResolveColumn(db, tableName, name)
                    ?? throw new InvalidOperationException($"{tableName}.{name} 不存在。"));

        foreach (var row in rows)
        {
            var canonicalKind = NotificationDefinitionRegistry.TryGet(row.Type, out var definition)
                ? definition!.Kind
                : row.Type;
            var category = definition?.Category ?? NotificationCategory.System;
            var (targetKind, targetDataJson) = ResolveHistoricalTarget(row);
            var occurredAtUtc = NormalizeHistoricalUtc(row.CreateTime);
            db.Ado.ExecuteCommand(
                $"UPDATE {Quote(tableName)} SET " +
                $"{Quote(columns[nameof(Notification.Type)].ColumnName)}=@Type, " +
                $"{Quote(columns[nameof(Notification.Category)].ColumnName)}=@Category, " +
                $"{Quote(columns[nameof(Notification.TemplateKey)].ColumnName)}=@TemplateKey, " +
                $"{Quote(columns[nameof(Notification.TemplateArgumentsJson)].ColumnName)}=@TemplateArgumentsJson, " +
                $"{Quote(columns[nameof(Notification.TargetKind)].ColumnName)}=@TargetKind, " +
                $"{Quote(columns[nameof(Notification.TargetDataJson)].ColumnName)}=@TargetDataJson, " +
                $"{Quote(columns[nameof(Notification.TargetSchemaVersion)].ColumnName)}=1, " +
                $"{Quote(columns[nameof(Notification.OccurredAtUtc)].ColumnName)}=@OccurredAtUtc " +
                $"WHERE {Quote(columns[nameof(Notification.Id)].ColumnName)}=@Id",
                new SugarParameter("@Type", canonicalKind),
                new SugarParameter("@Category", category),
                new SugarParameter("@TemplateKey", "notification.legacy"),
                new SugarParameter("@TemplateArgumentsJson", "{}"),
                new SugarParameter("@TargetKind", targetKind),
                new SugarParameter("@TargetDataJson", targetDataJson),
                new SugarParameter("@OccurredAtUtc", occurredAtUtc),
                new SugarParameter("@Id", row.Id));
        }
    }

    private static (string TargetKind, string? TargetDataJson) ResolveHistoricalTarget(Notification row)
    {
        if (!string.IsNullOrWhiteSpace(row.ExtData))
        {
            try
            {
                using var document = JsonDocument.Parse(row.ExtData);
                var root = document.RootElement;
                var app = TryGetString(root, "app");
                if (string.Equals(app, "forum", StringComparison.OrdinalIgnoreCase))
                {
                    var target = new NotificationTargetData
                    {
                        PostId = TryGetInt64(root, "postId"),
                        PostPublicId = TryGetString(root, "postPublicId"),
                        CommentId = TryGetInt64(root, "commentId")
                    };
                    if (target.PostId > 0 || !string.IsNullOrWhiteSpace(target.PostPublicId))
                    {
                        return (NotificationTargetKind.ForumPost, target.ToJson());
                    }
                }

                if (string.Equals(app, "chat", StringComparison.OrdinalIgnoreCase))
                {
                    var target = new NotificationTargetData
                    {
                        ChannelId = TryGetInt64(root, "channelId"),
                        MessageId = TryGetInt64(root, "messageId")
                    };
                    if (target.ChannelId > 0)
                    {
                        return (NotificationTargetKind.ChatConversation, target.ToJson());
                    }
                }
            }
            catch (JsonException)
            {
                // 历史无法解析时明确降级到 None，不猜测导航。
            }
        }

        var canonicalKind = NotificationDefinitionRegistry.TryGet(row.Type, out var definition)
            ? definition!.Kind
            : row.Type;
        return canonicalKind switch
        {
            NotificationType.PurchaseSucceeded when row.BusinessId > 0 =>
                (NotificationTargetKind.ShopOrder, new NotificationTargetData { OrderId = row.BusinessId }.ToJson()),
            NotificationType.BenefitExpired when row.BusinessId > 0 =>
                (NotificationTargetKind.Inventory, new NotificationTargetData { BenefitId = row.BusinessId }.ToJson()),
            NotificationType.LevelUp when row.BusinessId > 0 =>
                (NotificationTargetKind.Experience, new NotificationTargetData { UserId = row.BusinessId }.ToJson()),
            NotificationType.Followed when row.TriggerId > 0 =>
                (NotificationTargetKind.UserProfile, new NotificationTargetData { UserId = row.TriggerId }.ToJson()),
            _ => (NotificationTargetKind.None, null)
        };
    }

    private static void BackfillGroupsAndRelations(
        ISqlSugarClient db,
        IReadOnlyCollection<string> notificationTables,
        DateTime nowUtc)
    {
        var notifications = notificationTables
            .SelectMany(tableName => db.Queryable<Notification>().AS(tableName).ToList())
            .GroupBy(notification => new { notification.TenantId, notification.Id })
            .ToDictionary(group => (group.Key.TenantId, group.Key.Id), group => group.Single());
        var relations = db.Queryable<UserNotification>().ToList();
        var existingGroupIds = db.Queryable<NotificationInboxGroup>()
            .ToList()
            .Select(group => group.Id)
            .ToHashSet();
        foreach (var relation in relations)
        {
            if (relation.InboxGroupId > 0 && existingGroupIds.Contains(relation.InboxGroupId))
            {
                continue;
            }

            if (!notifications.TryGetValue((relation.TenantId, relation.NotificationId), out var notification))
            {
                throw new InvalidOperationException($"UserNotification {relation.Id} 引用的通知不存在。");
            }

            var group = new NotificationInboxGroup
            {
                Id = SnowFlakeSingle.Instance.NextId(),
                TenantId = relation.TenantId,
                UserId = relation.UserId,
                GroupKey = $"legacy:{relation.NotificationId}:user:{relation.UserId}",
                Category = notification.Category,
                Kind = notification.Type,
                LatestNotificationId = notification.Id,
                OccurrenceCount = 1,
                UnreadOccurrenceCount = !relation.IsRead && !relation.IsDeleted ? 1 : 0,
                DistinctTriggerCount = notification.TriggerId.HasValue ? 1 : 0,
                FirstOccurredAtUtc = notification.OccurredAtUtc,
                LastOccurredAtUtc = notification.OccurredAtUtc,
                IsDeleted = relation.IsDeleted,
                DeletedAtUtc = relation.DeletedAt,
                ReadAtUtc = relation.IsRead ? relation.ReadAt : null,
                CreateTime = nowUtc,
                CreateBy = "DbMigrate"
            };
            db.Insertable(group).ExecuteCommand();
            relation.InboxGroupId = group.Id;
            relation.OccurredAtUtc = notification.OccurredAtUtc;
            db.Updateable(relation).ExecuteCommand();
            existingGroupIds.Add(group.Id);
        }
    }

    private static void RebuildStates(ISqlSugarClient db, DateTime nowUtc)
    {
        var groups = db.Queryable<NotificationInboxGroup>().ToList();
        foreach (var userGroups in groups.GroupBy(group => new { group.TenantId, group.UserId }))
        {
            var unreadGroups = userGroups
                .Where(group => !group.IsDeleted && group.UnreadOccurrenceCount > 0)
                .ToList();
            var state = db.Queryable<NotificationInboxState>()
                .Where(item =>
                    item.TenantId == userGroups.Key.TenantId &&
                    item.UserId == userGroups.Key.UserId)
                .First();
            if (state == null)
            {
                state = new NotificationInboxState
                {
                    Id = SnowFlakeSingle.Instance.NextId(),
                    TenantId = userGroups.Key.TenantId,
                    UserId = userGroups.Key.UserId,
                    Revision = 1,
                    CreateTime = nowUtc,
                    CreateBy = "DbMigrate"
                };
            }

            state.UnreadGroupCount = unreadGroups.Count;
            state.UnreadOccurrenceCount = unreadGroups.Sum(group => group.UnreadOccurrenceCount);
            state.LastChangedAtUtc = nowUtc;
            state.ModifyTime = nowUtc;
            state.ModifyBy = "DbMigrate";
            if (state.Id > 0 && db.Queryable<NotificationInboxState>().Any(item => item.Id == state.Id))
            {
                db.Updateable(state).ExecuteCommand();
            }
            else
            {
                db.Insertable(state).ExecuteCommand();
            }
        }
    }

    private static List<LegacySettingRow> ReadLegacySettings(ISqlSugarClient db)
    {
        if (!db.DbMaintenance.IsAnyTable(SettingTable, false) ||
            DatabaseIdentifierResolver.ResolveColumn(db, SettingTable, nameof(NotificationSetting.Category)) != null)
        {
            return [];
        }

        var columns = ResolveLegacySettingColumns(db);
        var sql = $"SELECT " +
                  $"{Quote(columns["Id"].ColumnName)} AS Id, " +
                  $"{Quote(columns["TenantId"].ColumnName)} AS TenantId, " +
                  $"{Quote(columns["UserId"].ColumnName)} AS UserId, " +
                  $"{Quote(columns["NotificationType"].ColumnName)} AS NotificationType, " +
                  $"{Quote(columns["IsEnabled"].ColumnName)} AS IsEnabled, " +
                  $"{Quote(columns["EnableInApp"].ColumnName)} AS EnableInApp, " +
                  $"{Quote(columns["EnableSound"].ColumnName)} AS EnableSound " +
                  $"FROM {Quote(columns["Id"].TableName)}";
        return db.Ado.SqlQuery<LegacySettingRow>(sql);
    }

    private static bool HasLegacySettingSchema(ISqlSugarClient db)
    {
        return db.DbMaintenance.IsAnyTable(SettingTable, false) &&
               DatabaseIdentifierResolver.ResolveColumn(
                   db,
                   SettingTable,
                   nameof(NotificationSetting.Category)) == null;
    }

    private static void RestoreLegacySettings(
        ISqlSugarClient db,
        IReadOnlyCollection<LegacySettingRow> legacyRows,
        DateTime nowUtc)
    {
        if (legacyRows.Count == 0)
        {
            return;
        }

        db.Deleteable<NotificationSetting>().ExecuteCommand();
        var settings = legacyRows
            .GroupBy(row => new
            {
                row.TenantId,
                row.UserId,
                Category = NotificationDefinitionRegistry.GetRequired(row.NotificationType).Category
            })
            .Select(group =>
            {
                var first = group.First();
                return new NotificationSetting
                {
                    Id = SnowFlakeSingle.Instance.NextId(),
                    TenantId = group.Key.TenantId,
                    UserId = group.Key.UserId,
                    Category = group.Key.Category,
                    InAppEnabled = first.IsEnabled && first.EnableInApp,
                    RealtimePreviewEnabled = first.EnableSound,
                    CreateTime = nowUtc,
                    CreateBy = "DbMigrate"
                };
            }).ToList();
        if (settings.Count > 0)
        {
            db.Insertable(settings).ExecuteCommand();
        }
    }

    private static IReadOnlyList<string> DiagnoseLegacySettings(ISqlSugarClient db)
    {
        var rows = ReadLegacySettings(db);
        if (rows.Count == 0)
        {
            return [];
        }

        var issues = new List<string>();
        var unknownTypes = rows
            .Select(row => row.NotificationType)
            .Where(type => !NotificationDefinitionRegistry.TryGet(type, out _))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (unknownTypes.Count > 0)
        {
            issues.Add($"NotificationSetting 存在未知类型：{string.Join(", ", unknownTypes)}");
        }

        var knownRows = rows.Where(row => NotificationDefinitionRegistry.TryGet(row.NotificationType, out _)).ToList();
        var conflicts = knownRows
            .GroupBy(row => new
            {
                row.TenantId,
                row.UserId,
                Category = NotificationDefinitionRegistry.GetRequired(row.NotificationType).Category
            })
            .Count(group => group
                .Select(row => (row.IsEnabled && row.EnableInApp, row.EnableSound))
                .Distinct()
                .Count() > 1);
        if (conflicts > 0)
        {
            issues.Add($"NotificationSetting 存在 {conflicts} 组同分类冲突偏好");
        }

        return issues;
    }

    private static long CountNonDefaultDeliveryRows(ISqlSugarClient db)
    {
        var status = DatabaseIdentifierResolver.ResolveColumn(db, UserNotificationTable, "DeliveryStatus");
        var delivered = DatabaseIdentifierResolver.ResolveColumn(db, UserNotificationTable, "DeliveredAt");
        var retry = DatabaseIdentifierResolver.ResolveColumn(db, UserNotificationTable, "RetryCount");
        var lastRetry = DatabaseIdentifierResolver.ResolveColumn(db, UserNotificationTable, "LastRetryAt");
        if (status == null || delivered == null || retry == null || lastRetry == null)
        {
            return 0;
        }

        return Convert.ToInt64(db.Ado.GetScalar(
            $"SELECT COUNT(*) FROM {Quote(status.TableName)} WHERE " +
            $"{Quote(status.ColumnName)} <> 'Created' OR " +
            $"{Quote(delivered.ColumnName)} IS NOT NULL OR " +
            $"{Quote(retry.ColumnName)} <> 0 OR " +
            $"{Quote(lastRetry.ColumnName)} IS NOT NULL"), CultureInfo.InvariantCulture);
    }

    private static List<NotificationIdentity> ReadNotificationIdentities(
        ISqlSugarClient db,
        IEnumerable<string> tableNames)
    {
        var result = new List<NotificationIdentity>();
        foreach (var tableName in tableNames)
        {
            var id = DatabaseIdentifierResolver.ResolveColumn(db, tableName, nameof(Notification.Id));
            var tenantId = DatabaseIdentifierResolver.ResolveColumn(db, tableName, nameof(Notification.TenantId));
            var businessKey = DatabaseIdentifierResolver.ResolveColumn(db, tableName, nameof(Notification.BusinessKey));
            if (id == null || tenantId == null || businessKey == null)
            {
                continue;
            }

            result.AddRange(db.Ado.SqlQuery<NotificationIdentity>(
                $"SELECT {Quote(id.ColumnName)} AS Id, " +
                $"{Quote(tenantId.ColumnName)} AS TenantId, " +
                $"{Quote(businessKey.ColumnName)} AS BusinessKey " +
                $"FROM {Quote(tableName)}"));
        }

        return result;
    }

    private static Dictionary<string, DatabaseColumnReference> ResolveLegacySettingColumns(ISqlSugarClient db)
    {
        return new[] { "Id", "TenantId", "UserId", "NotificationType", "IsEnabled", "EnableInApp", "EnableSound" }
            .ToDictionary(
                name => name,
                name => DatabaseIdentifierResolver.ResolveColumn(db, SettingTable, name)
                        ?? throw new InvalidOperationException($"{SettingTable}.{name} 不存在。"));
    }

    private static List<string> GetNotificationTables(ISqlSugarClient db)
    {
        return db.DbMaintenance.GetTableInfoList(false)
            .Select(table => table.Name)
            .Where(tableName => NotificationTablePattern.IsMatch(tableName))
            .OrderBy(tableName => tableName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static long? TryGetInt64(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var numeric))
        {
            return numeric;
        }

        return value.ValueKind == JsonValueKind.String && long.TryParse(
            value.GetString(),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out numeric)
            ? numeric
            : null;
    }

    private static string? TryGetString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim()
            : null;
    }

    private static DateTime NormalizeHistoricalUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static bool IsLowercase(string identifier) =>
        string.Equals(identifier, identifier.ToLowerInvariant(), StringComparison.Ordinal);

    private static string Quote(string identifier) =>
        $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private sealed record NotificationColumnDefinition(string DataType, string Constraints);

    private sealed class NotificationIdentity
    {
        public long Id { get; set; }
        public long TenantId { get; set; }
        public string? BusinessKey { get; set; }
    }

    private sealed class LegacySettingRow
    {
        public long Id { get; set; }
        public long TenantId { get; set; }
        public long UserId { get; set; }
        public string NotificationType { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public bool EnableInApp { get; set; }
        public bool EnableSound { get; set; }
    }
}
