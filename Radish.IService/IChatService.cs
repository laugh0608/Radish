using Radish.IService.Base;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;

namespace Radish.IService;

/// <summary>聊天室服务接口</summary>
public interface IChatService : IBaseService<Channel, ChannelVo>
{
    /// <summary>获取频道列表（含当前用户未读状态）</summary>
    Task<List<ChannelVo>> GetChannelListAsync(long tenantId, long userId);

    /// <summary>获取频道详情（含当前用户未读状态）</summary>
    Task<ChannelVo?> GetChannelDetailAsync(long tenantId, long userId, long channelId);

    /// <summary>获取频道历史消息</summary>
    Task<List<ChannelMessageVo>> GetHistoryAsync(long tenantId, long userId, long channelId, long? beforeMessageId, int pageSize = 50);

    /// <summary>发送消息</summary>
    Task<ChannelMessageVo> SendMessageAsync(long tenantId, long userId, string userName, SendChannelMessageDto request);

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
    Task<List<ChannelMemberVo>> GetOnlineMembersAsync(long tenantId, long channelId);
}
