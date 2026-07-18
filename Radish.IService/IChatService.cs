using Radish.IService.Base;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;

namespace Radish.IService;

/// <summary>消息发送结果；控制器据此避免幂等重放再次推送实时事件</summary>
public sealed record ChatMessageSendResult(
    ChannelMessageVo Message,
    bool WasCreated,
    IReadOnlyList<long>? ConversationChangedUserIds = null);

/// <summary>聊天室服务接口</summary>
public interface IChatService : IBaseService<Channel, ChannelVo>
{
    /// <summary>获取频道列表（含当前用户未读状态）</summary>
    Task<List<ChannelVo>> GetChannelListAsync(
        long tenantId,
        long userId,
        ChatChannelListView view = ChatChannelListView.Active);

    /// <summary>获取频道详情（含当前用户未读状态）</summary>
    Task<ChannelVo?> GetChannelDetailAsync(long tenantId, long userId, long channelId);

    /// <summary>获取频道历史消息（支持按锚点向前或向后分页）</summary>
    Task<List<ChannelMessageVo>> GetHistoryAsync(long tenantId, long userId, long channelId, long? beforeMessageId, long? afterMessageId, int pageSize = 50);

    /// <summary>获取目标消息附近的窗口消息</summary>
    Task<ChannelMessageWindowVo?> GetMessageWindowAsync(
        long tenantId,
        long userId,
        long channelId,
        long messageId,
        int beforeCount = 25,
        int afterCount = 25);

    /// <summary>发送消息</summary>
    Task<ChatMessageSendResult> SendMessageAsync(
        long tenantId,
        long userId,
        string userName,
        SendChannelMessageDto request,
        bool canManageChannel = false);

    /// <summary>撤回消息，成功返回所属频道 Id</summary>
    Task<long?> RecallMessageAsync(long tenantId, long userId, string userName, long messageId, bool canRecallOthers);

    /// <summary>频道加入（确保成员关系存在）</summary>
    Task JoinChannelAsync(long tenantId, long userId, long channelId, string operatorName);

    /// <summary>频道离开（P0 仅处理在线态，不修改成员记录）</summary>
    Task LeaveChannelAsync(long tenantId, long userId, long channelId);

    /// <summary>标记频道已读并返回未读状态</summary>
    Task<ChannelUnreadStateVo> MarkChannelAsReadAsync(long tenantId, long userId, long channelId, string operatorName);

    /// <summary>获取频道未读状态</summary>
    Task<ChannelUnreadStateVo> GetChannelUnreadStateAsync(long tenantId, long userId, long channelId);

    /// <summary>获取频道应接收未读变化推送的用户 Id 列表</summary>
    Task<List<long>> GetChannelAudienceUserIdsAsync(long tenantId, long channelId);

    /// <summary>获取频道在线成员列表</summary>
    Task<List<ChannelMemberVo>> GetOnlineMembersAsync(long tenantId, long userId, long channelId);
}
