using System.Buffers.Binary;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using Radish.IRepository;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Repository.Base;
using Radish.Repository.UnitOfWorks;
using SqlSugar;

namespace Radish.Repository;

/// <summary>Chat 库频道匿名公开摘要资格的精确租户查询和原子写入边界。</summary>
public sealed class ChannelDiscoverabilityRepository : BaseRepository<Channel>,
    IChannelDiscoverabilityRepository,
    IPublicDiscoverChannelRepository
{
    private static readonly Expression<Func<Channel, bool>> ValidPublicIdentityExpression = channel =>
        channel.Name.Trim() != "" &&
        channel.Slug != "" &&
        !channel.Slug.StartsWith("-") &&
        !channel.Slug.EndsWith("-") &&
        !channel.Slug.Contains("--") &&
        channel.Slug
            .Replace("a", "").Replace("b", "").Replace("c", "").Replace("d", "")
            .Replace("e", "").Replace("f", "").Replace("g", "").Replace("h", "")
            .Replace("i", "").Replace("j", "").Replace("k", "").Replace("l", "")
            .Replace("m", "").Replace("n", "").Replace("o", "").Replace("p", "")
            .Replace("q", "").Replace("r", "").Replace("s", "").Replace("t", "")
            .Replace("u", "").Replace("v", "").Replace("w", "").Replace("x", "")
            .Replace("y", "").Replace("z", "")
            .Replace("0", "").Replace("1", "").Replace("2", "").Replace("3", "")
            .Replace("4", "").Replace("5", "").Replace("6", "").Replace("7", "")
            .Replace("8", "").Replace("9", "").Replace("-", "") == "";

    public ChannelDiscoverabilityRepository(IUnitOfWorkManage unitOfWorkManage) : base(unitOfWorkManage)
    {
    }

    public Task<(IReadOnlyList<Channel> Items, int Total)> QueryPageAsync(ChannelDiscoverabilityPageQuery query)
    {
        return ExecuteDbOperationAsync(async () =>
        {
            RefAsync<int> total = 0;
            var source = DbProtectedClient.Queryable<Channel>()
                .Where(channel =>
                    channel.TenantId == query.TenantId &&
                    (channel.Type == ChannelType.Public ||
                     channel.DiscoverVisibility == ChannelDiscoverVisibility.Summary));
            if (!query.IncludeDeleted)
            {
                source = source.Where(channel => !channel.IsDeleted);
            }

            if (query.DiscoverVisibility.HasValue)
            {
                var visibility = query.DiscoverVisibility.Value;
                source = source.Where(channel => channel.DiscoverVisibility == visibility);
            }

            if (query.IsEnabled.HasValue)
            {
                var isEnabled = query.IsEnabled.Value;
                source = source.Where(channel => channel.IsEnabled == isEnabled);
            }

            if (!string.IsNullOrWhiteSpace(query.Keyword))
            {
                var keyword = query.Keyword;
                source = source.Where(channel =>
                    channel.Name.Contains(keyword) ||
                    channel.Slug.Contains(keyword) ||
                    channel.Description != null && channel.Description.Contains(keyword));
            }

            var items = await source
                .OrderBy(channel => channel.Sort)
                .OrderBy(channel => channel.Id)
                .ToPageListAsync(query.PageIndex, query.PageSize, total);
            return ((IReadOnlyList<Channel>)items, total.Value);
        });
    }

    public Task<Channel?> QueryByIdAsync(long tenantId, long channelId)
    {
        return ExecuteDbOperationAsync(async () => (Channel?)await DbProtectedClient.Queryable<Channel>()
            .Where(channel =>
                channel.Id == channelId &&
                channel.TenantId == tenantId &&
                (channel.Type == ChannelType.Public ||
                 channel.DiscoverVisibility == ChannelDiscoverVisibility.Summary))
            .FirstAsync());
    }

    public Task<(IReadOnlyList<ChannelDiscoverVisibilityEvent> Items, int Total)> QueryHistoryAsync(
        ChannelDiscoverVisibilityHistoryQuery query)
    {
        return ExecuteDbOperationAsync(async () =>
        {
            var channelExists = await DbProtectedClient.Queryable<Channel>()
                .Where(channel =>
                    channel.Id == query.ChannelId &&
                    channel.TenantId == query.TenantId &&
                    (channel.Type == ChannelType.Public ||
                     channel.DiscoverVisibility == ChannelDiscoverVisibility.Summary))
                .AnyAsync();
            if (!channelExists)
            {
                throw new ChannelDiscoverabilityTargetUnavailableException();
            }

            RefAsync<int> total = 0;
            var items = await DbProtectedClient.Queryable<ChannelDiscoverVisibilityEvent>()
                .Where(change => change.TenantId == query.TenantId && change.ChannelId == query.ChannelId)
                .OrderByDescending(change => change.ResultVersion)
                .OrderByDescending(change => change.Id)
                .ToPageListAsync(query.PageIndex, query.PageSize, total);
            return ((IReadOnlyList<ChannelDiscoverVisibilityEvent>)items, total.Value);
        });
    }

    public Task<ChannelDiscoverVisibilityWriteResult> SetVisibilityAsync(
        ChannelDiscoverVisibilityChangeCommand command)
    {
        return ExecuteDbOperationAsync(() => SetVisibilityCoreAsync(command));
    }

    public Task<IReadOnlyList<PublicDiscoverSourceProjection>> QueryChannelSummariesAsync(
        PublicDiscoverSourceWindow window,
        DateTime recentWindowStartedAtUtc)
    {
        return ExecuteDbOperationAsync(async () =>
        {
            var query = BuildDiscoverableChannelQuery(window.TenantId)
                .Where(channel => (channel.LastMessageTime ?? channel.CreateTime) <= window.SnapshotCutoffUtc);
            query = ApplyChannelCursor(query, window);
            var channels = await query
                .OrderByDescending(channel => channel.LastMessageTime ?? channel.CreateTime)
                .OrderByDescending(channel => channel.Id)
                .Take(window.Take)
                .ToListAsync();
            channels = channels
                .Where(channel => ChannelDiscoverabilityPolicy.GetSummaryEligibilityIssues(channel).Count == 0)
                .ToList();
            if (channels.Count == 0)
            {
                return (IReadOnlyList<PublicDiscoverSourceProjection>)[];
            }

            var channelIds = channels.Select(channel => channel.Id).ToList();
            var recentCounts = await DbProtectedClient.Queryable<ChannelMessage>()
                .Where(message =>
                    message.TenantId == window.TenantId &&
                    channelIds.Contains(message.ChannelId) &&
                    !message.IsDeleted &&
                    message.CreateTime >= recentWindowStartedAtUtc &&
                    message.CreateTime <= window.SnapshotCutoffUtc)
                .GroupBy(message => message.ChannelId)
                .Select(message => new ChannelRecentMessageCount
                {
                    ChannelId = message.ChannelId,
                    Count = SqlFunc.AggregateCount(message.Id)
                })
                .ToListAsync();
            var countMap = recentCounts.ToDictionary(item => item.ChannelId, item => item.Count);
            return (IReadOnlyList<PublicDiscoverSourceProjection>)channels.Select(channel => new PublicDiscoverSourceProjection
            {
                SourceId = channel.Id,
                Kind = PublicDiscoverItemKind.ChannelSummary,
                OccurredAtUtc = channel.LastMessageTime ?? channel.CreateTime,
                Title = channel.Name,
                Summary = channel.Description,
                TargetKind = PublicDiscoverTargetKind.Messages,
                ChannelId = channel.Id,
                RequiresAuthentication = true,
                MetricKind = PublicDiscoverMetricKind.RecentReplies,
                MetricValue = countMap.GetValueOrDefault(channel.Id)
            }).ToList();
        });
    }

    public Task<PublicDiscoverChannelPulseCounts> QueryPulseAsync(
        long tenantId,
        DateTime windowStartedAtUtc,
        DateTime windowEndedAtUtc)
    {
        return ExecuteDbOperationAsync(async () =>
        {
            var discoverableQuery = BuildDiscoverableChannelQuery(tenantId);
            var discoverableCount = await discoverableQuery.CountAsync();
            var recentItemCount = await BuildDiscoverableChannelQuery(tenantId)
                .Where(channel =>
                    (channel.LastMessageTime ?? channel.CreateTime) >= windowStartedAtUtc &&
                    (channel.LastMessageTime ?? channel.CreateTime) <= windowEndedAtUtc)
                .CountAsync();
            return new PublicDiscoverChannelPulseCounts(discoverableCount, recentItemCount);
        });
    }

    private async Task<ChannelDiscoverVisibilityWriteResult> SetVisibilityCoreAsync(
        ChannelDiscoverVisibilityChangeCommand command)
    {
        DbProtectedClient.Ado.BeginTran();
        try
        {
            await AcquirePostgreSqlLockAsync(command.TenantId, command.ChannelId);
            var channel = await DbProtectedClient.Queryable<Channel>()
                .Where(candidate =>
                    candidate.Id == command.ChannelId &&
                    candidate.TenantId == command.TenantId &&
                    (candidate.Type == ChannelType.Public ||
                     candidate.DiscoverVisibility == ChannelDiscoverVisibility.Summary))
                .FirstAsync();
            if (channel == null)
            {
                throw new ChannelDiscoverabilityTargetUnavailableException();
            }

            if (channel.DiscoverVisibility == command.DiscoverVisibility)
            {
                DbProtectedClient.Ado.CommitTran();
                return new ChannelDiscoverVisibilityWriteResult(channel, false);
            }

            if (channel.DiscoverVisibilityVersion != command.ExpectedVersion)
            {
                throw new ChannelDiscoverabilityStateConflictException();
            }

            if (command.DiscoverVisibility == ChannelDiscoverVisibility.Summary)
            {
                var eligibilityIssues = ChannelDiscoverabilityPolicy.GetSummaryEligibilityIssues(channel);
                if (eligibilityIssues.Count > 0)
                {
                    throw new ChannelDiscoverabilityIneligibleException(eligibilityIssues);
                }
            }

            var resultVersion = command.ExpectedVersion + 1;
            var affected = await DbProtectedClient.Updateable<Channel>()
                .SetColumns(candidate => new Channel
                {
                    DiscoverVisibility = command.DiscoverVisibility,
                    DiscoverVisibilityVersion = resultVersion,
                    ModifyTime = command.NowUtc,
                    ModifyBy = command.ActorName,
                    ModifyId = command.ActorUserId
                })
                .Where(candidate =>
                    candidate.Id == command.ChannelId &&
                    candidate.TenantId == command.TenantId &&
                    candidate.DiscoverVisibility == channel.DiscoverVisibility &&
                    candidate.DiscoverVisibilityVersion == command.ExpectedVersion)
                .ExecuteCommandAsync();
            if (affected != 1)
            {
                throw new ChannelDiscoverabilityStateConflictException();
            }

            await DbProtectedClient.Insertable(new ChannelDiscoverVisibilityEvent
            {
                Id = SnowFlakeSingle.Instance.NextId(),
                TenantId = command.TenantId,
                ChannelId = command.ChannelId,
                FromVisibility = channel.DiscoverVisibility,
                ToVisibility = command.DiscoverVisibility,
                ExpectedVersion = command.ExpectedVersion,
                ResultVersion = resultVersion,
                Reason = command.Reason,
                ActorUserId = command.ActorUserId,
                ActorName = command.ActorName,
                CreateTime = command.NowUtc
            }).ExecuteCommandAsync();

            channel.DiscoverVisibility = command.DiscoverVisibility;
            channel.DiscoverVisibilityVersion = resultVersion;
            channel.ModifyTime = command.NowUtc;
            channel.ModifyBy = command.ActorName;
            channel.ModifyId = command.ActorUserId;
            DbProtectedClient.Ado.CommitTran();
            return new ChannelDiscoverVisibilityWriteResult(channel, true);
        }
        catch
        {
            DbProtectedClient.Ado.RollbackTran();
            throw;
        }
    }

    private async Task AcquirePostgreSqlLockAsync(long tenantId, long channelId)
    {
        if (DbProtectedClient.CurrentConnectionConfig.DbType != DbType.PostgreSQL)
        {
            return;
        }

        var source = $"radish-channel-discoverability:{tenantId}:{channelId}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(source));
        var lockKey = BinaryPrimitives.ReadInt64BigEndian(hash);
        await DbProtectedClient.Ado.ExecuteCommandAsync(
            "SELECT pg_advisory_xact_lock(@LockKey)",
            new SugarParameter("@LockKey", lockKey));
    }

    private ISugarQueryable<Channel> BuildDiscoverableChannelQuery(long tenantId)
    {
        return DbProtectedClient.Queryable<Channel>()
            .Where(channel =>
                channel.TenantId == tenantId &&
                channel.Type == ChannelType.Public &&
                channel.IsEnabled &&
                !channel.IsDeleted &&
                channel.DiscoverVisibility == ChannelDiscoverVisibility.Summary)
            .Where(ValidPublicIdentityExpression);
    }

    private static ISugarQueryable<Channel> ApplyChannelCursor(
        ISugarQueryable<Channel> query,
        PublicDiscoverSourceWindow window)
    {
        if (!window.LastOccurredAtUtc.HasValue ||
            !window.LastKindOrder.HasValue ||
            !window.LastSourceId.HasValue)
        {
            return query;
        }

        var lastOccurredAtUtc = window.LastOccurredAtUtc.Value;
        if (PublicDiscoverKindOrder.ChannelSummary > window.LastKindOrder.Value)
        {
            return query.Where(channel => (channel.LastMessageTime ?? channel.CreateTime) <= lastOccurredAtUtc);
        }

        if (PublicDiscoverKindOrder.ChannelSummary < window.LastKindOrder.Value)
        {
            return query.Where(channel => (channel.LastMessageTime ?? channel.CreateTime) < lastOccurredAtUtc);
        }

        var lastSourceId = window.LastSourceId.Value;
        return query.Where(channel =>
            (channel.LastMessageTime ?? channel.CreateTime) < lastOccurredAtUtc ||
            (channel.LastMessageTime ?? channel.CreateTime) == lastOccurredAtUtc &&
            channel.Id < lastSourceId);
    }

    private sealed class ChannelRecentMessageCount
    {
        public long ChannelId { get; set; }

        public long Count { get; set; }
    }
}
