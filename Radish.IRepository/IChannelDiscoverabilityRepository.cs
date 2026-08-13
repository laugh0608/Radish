using Radish.Model;

namespace Radish.IRepository;

public sealed record ChannelDiscoverabilityPageQuery(
    long TenantId,
    int PageIndex,
    int PageSize,
    string Keyword,
    ChannelDiscoverVisibility? DiscoverVisibility,
    bool? IsEnabled,
    bool IncludeDeleted);

public sealed record ChannelDiscoverVisibilityHistoryQuery(
    long TenantId,
    long ChannelId,
    int PageIndex,
    int PageSize);

public sealed record ChannelDiscoverVisibilityChangeCommand(
    long TenantId,
    long ChannelId,
    ChannelDiscoverVisibility DiscoverVisibility,
    int ExpectedVersion,
    string Reason,
    long ActorUserId,
    string ActorName,
    DateTime NowUtc);

public sealed record ChannelDiscoverVisibilityWriteResult(Channel Channel, bool Changed);

public sealed class ChannelDiscoverabilityTargetUnavailableException : Exception;

public sealed class ChannelDiscoverabilityStateConflictException : Exception;

public sealed class ChannelDiscoverabilityIneligibleException : Exception
{
    public ChannelDiscoverabilityIneligibleException(IReadOnlyList<string> issues)
    {
        Issues = issues;
    }

    public IReadOnlyList<string> Issues { get; }
}

public interface IChannelDiscoverabilityRepository
{
    Task<(IReadOnlyList<Channel> Items, int Total)> QueryPageAsync(ChannelDiscoverabilityPageQuery query);

    Task<Channel?> QueryByIdAsync(long tenantId, long channelId);

    Task<(IReadOnlyList<ChannelDiscoverVisibilityEvent> Items, int Total)> QueryHistoryAsync(
        ChannelDiscoverVisibilityHistoryQuery query);

    Task<ChannelDiscoverVisibilityWriteResult> SetVisibilityAsync(
        ChannelDiscoverVisibilityChangeCommand command);
}
