using Radish.IRepository;
using Radish.Model;
using Radish.Repository.Base;
using Radish.Repository.UnitOfWorks;
using SqlSugar;

namespace Radish.Repository;

/// <summary>SQLite / PostgreSQL 一致的 Chat 字面搜索实现。</summary>
public sealed class ChannelMessageSearchRepository : BaseRepository<ChannelMessage>, IChannelMessageSearchRepository
{
    public ChannelMessageSearchRepository(IUnitOfWorkManage unitOfWorkManage)
        : base(unitOfWorkManage)
    {
    }

    public async Task<long> GetSnapshotMaxMessageIdAsync(
        long tenantId,
        IReadOnlyCollection<long> channelIds)
    {
        var normalizedChannelIds = channelIds.Where(id => id > 0).Distinct().ToList();
        if (normalizedChannelIds.Count == 0)
        {
            return 0;
        }

        return await ExecuteDbOperationAsync(async () =>
        {
            var query = CreateSearchBaseQuery(tenantId, normalizedChannelIds);
            return await query.AnyAsync()
                ? await query.MaxAsync(message => message.Id)
                : 0;
        });
    }

    public async Task<List<ChannelMessage>> SearchAsync(ChannelMessageSearchQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        var channelIds = query.ChannelIds.Where(id => id > 0).Distinct().ToList();
        if (channelIds.Count == 0 || query.SnapshotMaxMessageId <= 0 || query.Take <= 0)
        {
            return [];
        }

        return await ExecuteDbOperationAsync(async () =>
        {
            var messages = CreateSearchBaseQuery(query.TenantId, channelIds)
                .Where(message => message.Id <= query.SnapshotMaxMessageId);

            if (query.FromUtc.HasValue)
            {
                messages = messages.Where(message => message.CreateTime >= query.FromUtc.Value);
            }

            if (query.ToUtc.HasValue)
            {
                messages = messages.Where(message => message.CreateTime < query.ToUtc.Value);
            }

            if (query.LastCreateTimeUtc.HasValue && query.LastMessageId.HasValue)
            {
                messages = messages.Where(message =>
                    message.CreateTime < query.LastCreateTimeUtc.Value ||
                    message.CreateTime == query.LastCreateTimeUtc.Value && message.Id < query.LastMessageId.Value);
            }

            messages = ApplyLiteralContains(messages, query.NormalizedKeyword);
            return await messages
                .OrderBy(message => new { message.CreateTime, message.Id }, OrderByType.Desc)
                .Take(query.Take)
                .ToListAsync();
        });
    }

    private ISugarQueryable<ChannelMessage> CreateSearchBaseQuery(
        long tenantId,
        IReadOnlyCollection<long> channelIds)
    {
        return DbProtectedClient.Queryable<ChannelMessage>()
            .Where(message =>
                (message.TenantId == tenantId || message.TenantId == 0) &&
                channelIds.Contains(message.ChannelId) &&
                !message.IsDeleted &&
                (message.Type == MessageType.Text || message.Type == MessageType.Image) &&
                message.SearchText != null);
    }

    private ISugarQueryable<ChannelMessage> ApplyLiteralContains(
        ISugarQueryable<ChannelMessage> query,
        string normalizedKeyword)
    {
        return DbProtectedClient.CurrentConnectionConfig.DbType switch
        {
            DbType.Sqlite => query.Where(
                "instr(\"SearchText\", @keyword) > 0",
                new { keyword = normalizedKeyword }),
            DbType.PostgreSQL => query.Where(
                "strpos(searchtext, @keyword) > 0",
                new { keyword = normalizedKeyword }),
            var dbType => throw new NotSupportedException(
                $"Chat 消息搜索尚未定义 {dbType} 的字面包含语义。")
        };
    }
}
