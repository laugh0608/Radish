using Radish.Model.DtoModels;
using Radish.Model.ViewModels;

namespace Radish.IService;

/// <summary>已读游标写入后的服务端广播判定。</summary>
public sealed record ChannelReadStateAdvanceResult(
    ChannelReadStateVo State,
    bool ReceiptsChanged);

/// <summary>聊天轻量阅读回执权威服务。</summary>
public interface IChatReadReceiptService
{
    Task<ChannelReadStateAdvanceResult> AdvanceAsync(
        long tenantId,
        long userId,
        string userName,
        AdvanceChannelReadStateDto request);

    Task<ChatReadReceiptSummariesVo> GetSummariesAsync(
        long tenantId,
        long userId,
        GetChatReadReceiptSummariesDto request);

    Task<ChatReadReceiptReaderPageVo> GetReadersAsync(
        long tenantId,
        long userId,
        long channelId,
        long messageId,
        string? cursor,
        int pageSize);
}
