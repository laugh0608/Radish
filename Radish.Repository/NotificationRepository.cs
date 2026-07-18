using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Radish.Common;
using Radish.IRepository;
using Radish.Model;
using Radish.Repository.Base;
using Radish.Repository.UnitOfWorks;
using SqlSugar;

namespace Radish.Repository;

/// <summary>通知事件、用户分组和权威摘要的 Message 库事务边界。</summary>
public sealed class NotificationRepository : BaseRepository<Notification>, INotificationInboxRepository
{
    public NotificationRepository(IUnitOfWorkManage unitOfWorkManage) : base(unitOfWorkManage)
    {
    }

    public async Task<IReadOnlyDictionary<string, NotificationSetting>> GetPreferencesAsync(
        long tenantId,
        long userId)
    {
        var settings = await DbProtectedClient.Queryable<NotificationSetting>()
            .Where(setting => setting.TenantId == tenantId && setting.UserId == userId)
            .ToListAsync();
        return settings.ToDictionary(setting => setting.Category, StringComparer.Ordinal);
    }

    public Task<NotificationInboxPersistResult> PersistAsync(
        Notification notification,
        IReadOnlyList<NotificationInboxRecipient> recipients,
        DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(notification);
        if (recipients.Count == 0)
        {
            return Task.FromResult(new NotificationInboxPersistResult(notification.Id, false, []));
        }

        var normalizedRecipients = recipients
            .Where(recipient => recipient.UserId > 0)
            .GroupBy(recipient => recipient.UserId)
            .Select(group => group.First())
            .OrderBy(recipient => recipient.UserId)
            .ToList();
        if (normalizedRecipients.Count == 0)
        {
            throw new ArgumentException("通知接收者必须包含有效用户。", nameof(recipients));
        }

        return ExecuteDbOperationAsync(async () =>
        {
            DbProtectedClient.Ado.BeginTran();
            try
            {
                await AcquireTransactionLockAsync($"event:{notification.TenantId}:{notification.BusinessKey}");
                foreach (var recipient in normalizedRecipients)
                {
                    await AcquireTransactionLockAsync($"inbox:{notification.TenantId}:{recipient.UserId}");
                }

                var storedNotification = await QueryExistingNotificationAsync(notification);
                var eventCreated = storedNotification == null;
                if (storedNotification == null)
                {
                    await DbProtectedClient.Insertable(notification).SplitTable().ExecuteCommandAsync();
                    storedNotification = notification;
                }

                var changes = new List<NotificationInboxRecipientChange>();
                foreach (var recipient in normalizedRecipients)
                {
                    var existingRelation = await DbProtectedClient.Queryable<UserNotification>()
                        .Where(relation =>
                            relation.TenantId == storedNotification.TenantId &&
                            relation.UserId == recipient.UserId &&
                            relation.NotificationId == storedNotification.Id)
                        .FirstAsync();
                    if (existingRelation != null)
                    {
                        continue;
                    }

                    var groupKey = BuildGroupKey(storedNotification, recipient.UserId);
                    var group = await DbProtectedClient.Queryable<NotificationInboxGroup>()
                        .Where(item =>
                            item.TenantId == storedNotification.TenantId &&
                            item.UserId == recipient.UserId &&
                            item.GroupKey == groupKey)
                        .FirstAsync();
                    if (group == null)
                    {
                        group = CreateEmptyGroup(storedNotification, recipient.UserId, groupKey, nowUtc);
                        await DbProtectedClient.Insertable(group).ExecuteCommandAsync();
                    }

                    var isNewTrigger = await IsNewTriggerAsync(group, storedNotification);

                    var relation = new UserNotification(
                        new UserNotificationInitializationOptions(recipient.UserId, storedNotification.Id)
                        {
                            InboxGroupId = group.Id,
                            OccurredAtUtc = storedNotification.OccurredAtUtc,
                            TenantId = storedNotification.TenantId
                        })
                    {
                        Id = SnowFlakeSingle.Instance.NextId(),
                        CreateTime = nowUtc
                    };
                    await DbProtectedClient.Insertable(relation).ExecuteCommandAsync();

                    ApplyIncomingEvent(group, storedNotification, isNewTrigger, nowUtc);
                    await DbProtectedClient.Updateable(group).ExecuteCommandAsync();
                    var summary = await RebuildStateAsync(
                        storedNotification.TenantId,
                        recipient.UserId,
                        nowUtc,
                        incrementRevision: true);
                    changes.Add(new NotificationInboxRecipientChange(
                        recipient.UserId,
                        group.Id,
                        recipient.RealtimePreviewAllowed,
                        summary));
                }

                DbProtectedClient.Ado.CommitTran();
                return new NotificationInboxPersistResult(storedNotification.Id, eventCreated, changes);
            }
            catch
            {
                DbProtectedClient.Ado.RollbackTran();
                throw;
            }
        });
    }

    public Task<NotificationInboxSummarySnapshot> GetSummaryAsync(long tenantId, long userId)
    {
        return ReadSummaryAsync(tenantId, userId);
    }

    public async Task<IReadOnlyList<long>> GetGroupIdsByNotificationIdsAsync(
        long tenantId,
        long userId,
        IReadOnlyCollection<long> notificationIds)
    {
        var ids = notificationIds.Where(id => id > 0).Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return await DbProtectedClient.Queryable<UserNotification>()
            .Where(relation =>
                relation.TenantId == tenantId &&
                relation.UserId == userId &&
                !relation.IsDeleted &&
                ids.Contains(relation.NotificationId))
            .Select(relation => relation.InboxGroupId)
            .Distinct()
            .ToListAsync();
    }

    public async Task<NotificationInboxQueryResult> QueryAsync(
        long tenantId,
        long userId,
        string? category,
        bool onlyUnread,
        DateTime? beforeOccurredAtUtc,
        long? beforeGroupId,
        int skip,
        int take)
    {
        var effectiveTake = Math.Clamp(take, 1, 50);
        var query = DbProtectedClient.Queryable<NotificationInboxGroup>()
            .Where(group => group.TenantId == tenantId && group.UserId == userId && !group.IsDeleted);
        if (!string.IsNullOrWhiteSpace(category))
        {
            var normalizedCategory = category.Trim();
            query = query.Where(group => group.Category == normalizedCategory);
        }

        if (onlyUnread)
        {
            query = query.Where(group => group.UnreadOccurrenceCount > 0);
        }

        if (beforeOccurredAtUtc.HasValue && beforeGroupId.HasValue)
        {
            var beforeTime = beforeOccurredAtUtc.Value;
            var beforeId = beforeGroupId.Value;
            query = query.Where(group =>
                group.LastOccurredAtUtc < beforeTime ||
                (group.LastOccurredAtUtc == beforeTime && group.Id < beforeId));
        }


        var totalCountQuery = DbProtectedClient.Queryable<NotificationInboxGroup>()
            .Where(group => group.TenantId == tenantId && group.UserId == userId && !group.IsDeleted);
        if (!string.IsNullOrWhiteSpace(category))
        {
            var normalizedCategory = category.Trim();
            totalCountQuery = totalCountQuery.Where(group => group.Category == normalizedCategory);
        }

        if (onlyUnread)
        {
            totalCountQuery = totalCountQuery.Where(group => group.UnreadOccurrenceCount > 0);
        }

        var totalCount = await totalCountQuery.CountAsync();

        var groups = await query
            .OrderBy(group => group.LastOccurredAtUtc, OrderByType.Desc)
            .OrderBy(group => group.Id, OrderByType.Desc)
            .Skip(Math.Max(0, skip))
            .Take(effectiveTake + 1)
            .ToListAsync();
        var hasMore = groups.Count > effectiveTake;
        if (hasMore)
        {
            groups.RemoveAt(groups.Count - 1);
        }

        var notificationIds = groups.Select(group => group.LatestNotificationId).Distinct().ToList();
        var notifications = notificationIds.Count == 0
            ? []
            : await DbProtectedClient.Queryable<Notification>()
                .SplitTable()
                .Where(notification => notificationIds.Contains(notification.Id))
                .ToListAsync();
        var notificationById = notifications.ToDictionary(notification => notification.Id);
        var snapshots = groups
            .Where(group => notificationById.ContainsKey(group.LatestNotificationId))
            .Select(group => new NotificationInboxGroupSnapshot(
                group,
                notificationById[group.LatestNotificationId]))
            .ToList();

        return new NotificationInboxQueryResult(
            snapshots,
            hasMore,
            totalCount,
            await ReadSummaryAsync(tenantId, userId));
    }

    public Task<NotificationInboxMutationResult> MarkGroupsAsReadAsync(
        long tenantId,
        long userId,
        IReadOnlyCollection<long> groupIds,
        DateTime nowUtc)
    {
        var normalizedGroupIds = groupIds.Where(id => id > 0).Distinct().ToList();
        return MutateReadStateAsync(tenantId, userId, normalizedGroupIds, null, nowUtc);
    }

    public Task<NotificationInboxMutationResult> MarkAllAsReadAsync(
        long tenantId,
        long userId,
        string? category,
        DateTime nowUtc)
    {
        return MutateReadStateAsync(tenantId, userId, null, category?.Trim(), nowUtc);
    }

    public Task<NotificationInboxMutationResult> DeleteGroupAsync(
        long tenantId,
        long userId,
        long groupId,
        DateTime nowUtc)
    {
        return ExecuteDbOperationAsync(async () =>
        {
            DbProtectedClient.Ado.BeginTran();
            try
            {
                await AcquireTransactionLockAsync($"inbox:{tenantId}:{userId}");
                var group = await DbProtectedClient.Queryable<NotificationInboxGroup>()
                    .Where(item =>
                        item.Id == groupId &&
                        item.TenantId == tenantId &&
                        item.UserId == userId &&
                        !item.IsDeleted)
                    .FirstAsync();
                if (group == null)
                {
                    var unchanged = await ReadSummaryAsync(tenantId, userId);
                    DbProtectedClient.Ado.CommitTran();
                    return new NotificationInboxMutationResult(0, unchanged);
                }

                var affectedRelations = await DbProtectedClient.Updateable<UserNotification>()
                    .SetColumns(relation => new UserNotification
                    {
                        IsDeleted = true,
                        DeletedAt = nowUtc
                    })
                    .Where(relation =>
                        relation.TenantId == tenantId &&
                        relation.UserId == userId &&
                        relation.InboxGroupId == groupId &&
                        !relation.IsDeleted)
                    .ExecuteCommandAsync();
                group.IsDeleted = true;
                group.DeletedAtUtc = nowUtc;
                group.UnreadOccurrenceCount = 0;
                group.ModifyTime = nowUtc;
                await DbProtectedClient.Updateable(group).ExecuteCommandAsync();

                var summary = await RebuildStateAsync(tenantId, userId, nowUtc, incrementRevision: true);
                DbProtectedClient.Ado.CommitTran();
                return new NotificationInboxMutationResult(Math.Max(1, affectedRelations), summary);
            }
            catch
            {
                DbProtectedClient.Ado.RollbackTran();
                throw;
            }
        });
    }

    public Task<IReadOnlyList<NotificationSetting>> UpsertPreferencesAsync(
        long tenantId,
        long userId,
        IReadOnlyList<NotificationSetting> preferences,
        DateTime nowUtc)
    {
        return ExecuteDbOperationAsync<IReadOnlyList<NotificationSetting>>(async () =>
        {
            DbProtectedClient.Ado.BeginTran();
            try
            {
                await AcquireTransactionLockAsync($"inbox:{tenantId}:{userId}");
                foreach (var preference in preferences)
                {
                    var existing = await DbProtectedClient.Queryable<NotificationSetting>()
                        .Where(setting =>
                            setting.TenantId == tenantId &&
                            setting.UserId == userId &&
                            setting.Category == preference.Category)
                        .FirstAsync();
                    if (existing == null)
                    {
                        preference.Id = SnowFlakeSingle.Instance.NextId();
                        preference.TenantId = tenantId;
                        preference.UserId = userId;
                        preference.CreateTime = nowUtc;
                        await DbProtectedClient.Insertable(preference).ExecuteCommandAsync();
                    }
                    else
                    {
                        existing.InAppEnabled = preference.InAppEnabled;
                        existing.RealtimePreviewEnabled = preference.RealtimePreviewEnabled;
                        existing.ModifyTime = nowUtc;
                        existing.ModifyBy = preference.ModifyBy;
                        existing.ModifyId = preference.ModifyId;
                        await DbProtectedClient.Updateable(existing).ExecuteCommandAsync();
                    }
                }

                var result = await DbProtectedClient.Queryable<NotificationSetting>()
                    .Where(setting => setting.TenantId == tenantId && setting.UserId == userId)
                    .ToListAsync();
                DbProtectedClient.Ado.CommitTran();
                return result;
            }
            catch
            {
                DbProtectedClient.Ado.RollbackTran();
                throw;
            }
        });
    }

    public Task<NotificationInboxCleanupResult> CleanupAsync(
        DateTime nowUtc,
        int batchSize,
        int softRelationLimitPerUser)
    {
        var effectiveBatchSize = Math.Clamp(batchSize, 1, 1000);
        var effectiveSoftLimit = Math.Max(1, softRelationLimitPerUser);
        return ExecuteDbOperationAsync(async () =>
        {
            DbProtectedClient.Ado.BeginTran();
            try
            {
                var candidateGroups = await QueryCleanupCandidatesAsync(nowUtc, effectiveBatchSize);
                var userKeys = candidateGroups
                    .Select(group => (group.TenantId, group.UserId))
                    .Distinct()
                    .OrderBy(key => key.TenantId)
                    .ThenBy(key => key.UserId)
                    .ToList();
                foreach (var key in userKeys)
                {
                    await AcquireTransactionLockAsync($"inbox:{key.TenantId}:{key.UserId}");
                }

                var candidateIds = candidateGroups.Select(group => group.Id).ToList();
                var currentGroups = candidateIds.Count == 0
                    ? []
                    : await DbProtectedClient.Queryable<NotificationInboxGroup>()
                        .Where(group => candidateIds.Contains(group.Id))
                        .ToListAsync();
                var eligibleGroups = currentGroups
                    .Where(group => IsCleanupEligible(group, nowUtc))
                    .Take(effectiveBatchSize)
                    .ToList();
                var eligibleGroupIds = eligibleGroups.Select(group => group.Id).ToList();
                var notificationIds = eligibleGroupIds.Count == 0
                    ? []
                    : await DbProtectedClient.Queryable<UserNotification>()
                        .Where(relation => eligibleGroupIds.Contains(relation.InboxGroupId))
                        .Select(relation => relation.NotificationId)
                        .Distinct()
                        .ToListAsync();

                var deletedRelationCount = 0;
                var deletedGroupCount = 0;
                if (eligibleGroupIds.Count > 0)
                {
                    deletedRelationCount = await DbProtectedClient.Deleteable<UserNotification>()
                        .Where(relation => eligibleGroupIds.Contains(relation.InboxGroupId))
                        .ExecuteCommandAsync();
                    deletedGroupCount = await DbProtectedClient.Deleteable<NotificationInboxGroup>()
                        .Where(group => eligibleGroupIds.Contains(group.Id))
                        .ExecuteCommandAsync();

                    foreach (var key in eligibleGroups
                                 .Where(group => !group.IsDeleted)
                                 .Select(group => (group.TenantId, group.UserId))
                                 .Distinct())
                    {
                        await RebuildStateAsync(key.TenantId, key.UserId, nowUtc, incrementRevision: true);
                    }
                }

                var remainingNotificationIds = notificationIds.Count == 0
                    ? []
                    : await DbProtectedClient.Queryable<UserNotification>()
                        .Where(relation => notificationIds.Contains(relation.NotificationId))
                        .Select(relation => relation.NotificationId)
                        .Distinct()
                        .ToListAsync();
                var orphanNotificationIds = notificationIds
                    .Except(remainingNotificationIds)
                    .ToList();
                var orphanNotifications = orphanNotificationIds.Count == 0
                    ? []
                    : await DbProtectedClient.Queryable<Notification>()
                        .SplitTable()
                        .Where(notification => orphanNotificationIds.Contains(notification.Id))
                        .ToListAsync();
                var deletedNotificationCount = orphanNotifications.Count == 0
                    ? 0
                    : await DbProtectedClient.Deleteable(orphanNotifications)
                        .SplitTable()
                        .ExecuteCommandAsync();

                var capacityRows = await DbProtectedClient.Queryable<UserNotification>()
                    .GroupBy(relation => new { relation.TenantId, relation.UserId })
                    .Having(relation => SqlFunc.AggregateCount(relation.Id) > effectiveSoftLimit)
                    .Select(relation => new
                    {
                        relation.TenantId,
                        relation.UserId,
                        RelationCount = SqlFunc.AggregateCount(relation.Id)
                    })
                    .ToListAsync();
                var warnings = capacityRows
                    .Select(row => new NotificationInboxCapacityWarning(
                        row.TenantId,
                        row.UserId,
                        row.RelationCount))
                    .ToList();

                DbProtectedClient.Ado.CommitTran();
                return new NotificationInboxCleanupResult(
                    deletedRelationCount,
                    deletedGroupCount,
                    deletedNotificationCount,
                    warnings);
            }
            catch
            {
                DbProtectedClient.Ado.RollbackTran();
                throw;
            }
        });
    }

    private async Task<IReadOnlyList<NotificationInboxGroup>> QueryCleanupCandidatesAsync(
        DateTime nowUtc,
        int batchSize)
    {
        var deletedCutoff = nowUtc.AddDays(-30);
        var candidates = await DbProtectedClient.Queryable<NotificationInboxGroup>()
            .Where(group =>
                group.IsDeleted &&
                group.DeletedAtUtc.HasValue &&
                group.DeletedAtUtc.Value <= deletedCutoff)
            .OrderBy(group => group.DeletedAtUtc, OrderByType.Asc)
            .Take(batchSize)
            .ToListAsync();
        if (candidates.Count >= batchSize)
        {
            return candidates;
        }

        var remaining = batchSize - candidates.Count;
        foreach (var definitionGroup in NotificationDefinitionRegistry.All
                     .GroupBy(definition => definition.RetentionDays)
                     .OrderBy(group => group.Key))
        {
            if (remaining == 0)
            {
                break;
            }

            var retentionKinds = definitionGroup.Select(definition => definition.Kind).ToList();
            var retentionCutoff = nowUtc.AddDays(-definitionGroup.Key);
            var retainedGroups = await DbProtectedClient.Queryable<NotificationInboxGroup>()
                .Where(group =>
                    !group.IsDeleted &&
                    group.UnreadOccurrenceCount == 0 &&
                    group.LastOccurredAtUtc <= retentionCutoff &&
                    retentionKinds.Contains(group.Kind))
                .OrderBy(group => group.LastOccurredAtUtc, OrderByType.Asc)
                .Take(remaining)
                .ToListAsync();
            candidates.AddRange(retainedGroups);
            remaining = batchSize - candidates.Count;
        }

        if (remaining > 0)
        {
            var knownKinds = NotificationDefinitionRegistry.All.Select(definition => definition.Kind).ToList();
            var legacyCutoff = nowUtc.AddDays(-365);
            var legacyGroups = await DbProtectedClient.Queryable<NotificationInboxGroup>()
                .Where(group =>
                    !group.IsDeleted &&
                    group.UnreadOccurrenceCount == 0 &&
                    group.LastOccurredAtUtc <= legacyCutoff &&
                    !knownKinds.Contains(group.Kind))
                .OrderBy(group => group.LastOccurredAtUtc, OrderByType.Asc)
                .Take(remaining)
                .ToListAsync();
            candidates.AddRange(legacyGroups);
        }

        return candidates;
    }

    private static bool IsCleanupEligible(NotificationInboxGroup group, DateTime nowUtc)
    {
        if (group.IsDeleted)
        {
            return group.DeletedAtUtc.HasValue && group.DeletedAtUtc.Value <= nowUtc.AddDays(-30);
        }

        if (group.UnreadOccurrenceCount > 0)
        {
            return false;
        }

        var retentionDays = NotificationDefinitionRegistry.TryGet(group.Kind, out var definition)
            ? definition!.RetentionDays
            : 365;
        return group.LastOccurredAtUtc <= nowUtc.AddDays(-retentionDays);
    }

    private async Task<Notification?> QueryExistingNotificationAsync(Notification candidate)
    {
        var businessKey = candidate.BusinessKey;
        return await DbProtectedClient.Queryable<Notification>()
            .SplitTable()
            .Where(notification =>
                notification.TenantId == candidate.TenantId &&
                (notification.Id == candidate.Id || notification.BusinessKey == businessKey))
            .FirstAsync();
    }

    private Task<NotificationInboxMutationResult> MutateReadStateAsync(
        long tenantId,
        long userId,
        IReadOnlyCollection<long>? groupIds,
        string? category,
        DateTime nowUtc)
    {
        return ExecuteDbOperationAsync(async () =>
        {
            DbProtectedClient.Ado.BeginTran();
            try
            {
                await AcquireTransactionLockAsync($"inbox:{tenantId}:{userId}");
                var query = DbProtectedClient.Queryable<NotificationInboxGroup>()
                    .Where(group =>
                        group.TenantId == tenantId &&
                        group.UserId == userId &&
                        !group.IsDeleted &&
                        group.UnreadOccurrenceCount > 0);
                if (groupIds != null)
                {
                    if (groupIds.Count == 0)
                    {
                        var unchanged = await ReadSummaryAsync(tenantId, userId);
                        DbProtectedClient.Ado.CommitTran();
                        return new NotificationInboxMutationResult(0, unchanged);
                    }

                    var ids = groupIds.ToList();
                    query = query.Where(group => ids.Contains(group.Id));
                }

                if (!string.IsNullOrWhiteSpace(category))
                {
                    query = query.Where(group => group.Category == category);
                }

                var groups = await query.ToListAsync();
                var affected = 0;
                foreach (var group in groups)
                {
                    affected += await DbProtectedClient.Updateable<UserNotification>()
                        .SetColumns(relation => new UserNotification
                        {
                            IsRead = true,
                            ReadAt = nowUtc
                        })
                        .Where(relation =>
                            relation.TenantId == tenantId &&
                            relation.UserId == userId &&
                            relation.InboxGroupId == group.Id &&
                            !relation.IsDeleted &&
                            !relation.IsRead &&
                            relation.OccurredAtUtc <= group.LastOccurredAtUtc)
                        .ExecuteCommandAsync();

                    group.UnreadOccurrenceCount = await DbProtectedClient.Queryable<UserNotification>()
                        .Where(relation =>
                            relation.TenantId == tenantId &&
                            relation.UserId == userId &&
                            relation.InboxGroupId == group.Id &&
                            !relation.IsDeleted &&
                            !relation.IsRead)
                        .CountAsync();
                    group.ReadAtUtc = group.UnreadOccurrenceCount == 0 ? nowUtc : null;
                    group.ModifyTime = nowUtc;
                    await DbProtectedClient.Updateable(group).ExecuteCommandAsync();
                }

                var summary = affected > 0
                    ? await RebuildStateAsync(tenantId, userId, nowUtc, incrementRevision: true)
                    : await ReadSummaryAsync(tenantId, userId);
                DbProtectedClient.Ado.CommitTran();
                return new NotificationInboxMutationResult(affected, summary);
            }
            catch
            {
                DbProtectedClient.Ado.RollbackTran();
                throw;
            }
        });
    }

    private async Task<NotificationInboxSummarySnapshot> RebuildStateAsync(
        long tenantId,
        long userId,
        DateTime nowUtc,
        bool incrementRevision)
    {
        var unreadGroups = await DbProtectedClient.Queryable<NotificationInboxGroup>()
            .Where(group =>
                group.TenantId == tenantId &&
                group.UserId == userId &&
                !group.IsDeleted &&
                group.UnreadOccurrenceCount > 0)
            .ToListAsync();
        var state = await DbProtectedClient.Queryable<NotificationInboxState>()
            .Where(item => item.TenantId == tenantId && item.UserId == userId)
            .FirstAsync();
        if (state == null)
        {
            state = new NotificationInboxState
            {
                Id = SnowFlakeSingle.Instance.NextId(),
                TenantId = tenantId,
                UserId = userId,
                Revision = incrementRevision ? 1 : 0,
                CreateTime = nowUtc,
                LastChangedAtUtc = nowUtc
            };
            ApplyCounts(state, unreadGroups, nowUtc);
            await DbProtectedClient.Insertable(state).ExecuteCommandAsync();
        }
        else
        {
            if (incrementRevision)
            {
                state.Revision++;
            }

            ApplyCounts(state, unreadGroups, nowUtc);
            await DbProtectedClient.Updateable(state).ExecuteCommandAsync();
        }

        return ToSummary(state, unreadGroups);
    }

    private async Task<NotificationInboxSummarySnapshot> ReadSummaryAsync(long tenantId, long userId)
    {
        var state = await DbProtectedClient.Queryable<NotificationInboxState>()
            .Where(item => item.TenantId == tenantId && item.UserId == userId)
            .FirstAsync();
        var unreadGroups = await DbProtectedClient.Queryable<NotificationInboxGroup>()
            .Where(group =>
                group.TenantId == tenantId &&
                group.UserId == userId &&
                !group.IsDeleted &&
                group.UnreadOccurrenceCount > 0)
            .ToListAsync();
        if (state == null)
        {
            return new NotificationInboxSummarySnapshot(
                0,
                unreadGroups.Count,
                unreadGroups.Sum(group => group.UnreadOccurrenceCount),
                CountUnreadGroupsByCategory(unreadGroups),
                DateTime.UnixEpoch);
        }

        return ToSummary(state, unreadGroups);
    }

    private async Task AcquireTransactionLockAsync(string identity)
    {
        if (DbProtectedClient.CurrentConnectionConfig.DbType != DbType.PostgreSQL)
        {
            return;
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        var lockKey = BinaryPrimitives.ReadInt64BigEndian(hash);
        await DbProtectedClient.Ado.ExecuteCommandAsync(
            "SELECT pg_advisory_xact_lock(@LockKey)",
            new SugarParameter("@LockKey", lockKey));
    }

    private static NotificationInboxGroup CreateEmptyGroup(
        Notification notification,
        long userId,
        string groupKey,
        DateTime nowUtc)
    {
        return new NotificationInboxGroup
        {
            Id = SnowFlakeSingle.Instance.NextId(),
            TenantId = notification.TenantId,
            UserId = userId,
            GroupKey = groupKey,
            Category = notification.Category,
            Kind = notification.Type,
            LatestNotificationId = notification.Id,
            FirstOccurredAtUtc = notification.OccurredAtUtc,
            LastOccurredAtUtc = notification.OccurredAtUtc,
            CreateTime = nowUtc
        };
    }

    private static void ApplyIncomingEvent(
        NotificationInboxGroup group,
        Notification notification,
        bool isNewTrigger,
        DateTime nowUtc)
    {
        group.OccurrenceCount++;
        group.UnreadOccurrenceCount++;
        if (isNewTrigger)
        {
            group.DistinctTriggerCount++;
        }
        group.FirstOccurredAtUtc = group.OccurrenceCount == 1
            ? notification.OccurredAtUtc
            : MinUtc(group.FirstOccurredAtUtc, notification.OccurredAtUtc);
        if (notification.OccurredAtUtc >= group.LastOccurredAtUtc)
        {
            group.LatestNotificationId = notification.Id;
            group.LastOccurredAtUtc = notification.OccurredAtUtc;
        }

        group.IsDeleted = false;
        group.DeletedAtUtc = null;
        group.ReadAtUtc = null;
        group.ModifyTime = nowUtc;
        group.ModifyBy = "System";
        group.ModifyId = 0;
    }

    private async Task<bool> IsNewTriggerAsync(
        NotificationInboxGroup group,
        Notification notification)
    {
        if (!notification.TriggerId.HasValue)
        {
            return false;
        }

        if (group.OccurrenceCount == 0)
        {
            return true;
        }

        var existingNotificationIds = await DbProtectedClient.Queryable<UserNotification>()
            .Where(relation =>
                relation.TenantId == group.TenantId &&
                relation.UserId == group.UserId &&
                relation.InboxGroupId == group.Id)
            .Select(relation => relation.NotificationId)
            .ToListAsync();
        if (existingNotificationIds.Count == 0)
        {
            return true;
        }

        var triggerId = notification.TriggerId.Value;
        return !await DbProtectedClient.Queryable<Notification>()
            .SplitTable()
            .Where(item =>
                existingNotificationIds.Contains(item.Id) &&
                item.TriggerId == triggerId)
            .AnyAsync();
    }

    private static string BuildGroupKey(Notification notification, long userId)
    {
        var definition = NotificationDefinitionRegistry.GetRequired(notification.Type);
        if (!definition.AggregationWindow.HasValue)
        {
            return $"event:{notification.Id}";
        }

        var target = NotificationTargetData.FromJson(notification.TargetDataJson);
        var windowTicks = definition.AggregationWindow.Value.Ticks;
        var bucket = (notification.OccurredAtUtc.Ticks - DateTime.UnixEpoch.Ticks) / windowTicks;
        var identity = string.Join('|',
            notification.TenantId,
            userId,
            notification.Type,
            target.ToCanonicalIdentity(notification.TargetKind),
            bucket);
        return $"aggregate:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant()}";
    }

    private static void ApplyCounts(
        NotificationInboxState state,
        IReadOnlyCollection<NotificationInboxGroup> unreadGroups,
        DateTime nowUtc)
    {
        state.UnreadGroupCount = unreadGroups.Count;
        state.UnreadOccurrenceCount = unreadGroups.Sum(group => group.UnreadOccurrenceCount);
        state.LastChangedAtUtc = nowUtc;
        state.ModifyTime = nowUtc;
        state.ModifyBy = "System";
        state.ModifyId = 0;
    }

    private static NotificationInboxSummarySnapshot ToSummary(
        NotificationInboxState state,
        IReadOnlyCollection<NotificationInboxGroup> unreadGroups)
    {
        return new NotificationInboxSummarySnapshot(
            state.Revision,
            state.UnreadGroupCount,
            state.UnreadOccurrenceCount,
            CountUnreadGroupsByCategory(unreadGroups),
            state.LastChangedAtUtc);
    }

    private static IReadOnlyDictionary<string, long> CountUnreadGroupsByCategory(
        IEnumerable<NotificationInboxGroup> unreadGroups)
    {
        return unreadGroups
            .GroupBy(group => group.Category, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.LongCount(), StringComparer.Ordinal);
    }

    private static DateTime MinUtc(DateTime left, DateTime right) => left <= right ? left : right;
}
