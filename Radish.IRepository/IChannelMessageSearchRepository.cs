using Radish.Model;

namespace Radish.IRepository;

/// <summary>服务端权威 Chat 搜索查询条件。</summary>
public sealed record ChannelMessageSearchQuery(
    long TenantId,
    IReadOnlyCollection<long> ChannelIds,
    string NormalizedKeyword,
    DateTime? FromUtc,
    DateTime? ToUtc,
    long SnapshotMaxMessageId,
    DateTime? LastCreateTimeUtc,
    long? LastMessageId,
    int Take);

/// <summary>Chat 历史消息字面检索仓储。</summary>
public interface IChannelMessageSearchRepository
{
    Task<long> GetSnapshotMaxMessageIdAsync(long tenantId, IReadOnlyCollection<long> channelIds);

    Task<List<ChannelMessage>> SearchAsync(ChannelMessageSearchQuery query);
}
