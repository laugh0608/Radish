using Radish.Model;

namespace Radish.IService;

/// <summary>聊天室频道统一访问判定结果</summary>
public sealed record ChatChannelAccessResult(
    bool Exists,
    ChannelType? ChannelType,
    bool CanView,
    bool CanSend,
    bool CanJoinRealtime,
    bool CanViewMembers,
    bool IsDirectConversation,
    DirectConversationRequestStatus? DirectRequestStatus = null,
    long? DirectRequestedByUserId = null,
    long? DirectBlockedByUserId = null,
    long? DirectPeerUserId = null,
    long? DirectConversationId = null,
    bool HasMessages = false,
    bool IsPeerAvailable = true,
    MemberRole? ChannelMemberRole = null,
    bool CanManageChannel = false)
{
    public static ChatChannelAccessResult Unavailable { get; } =
        new(false, null, false, false, false, false, false);

    /// <summary>是否允许在当前频道状态下新增或取消消息回应。</summary>
    public bool CanReact => CanView && ChannelType switch
    {
        Model.ChannelType.Public => true,
        Model.ChannelType.Announcement => true,
        Model.ChannelType.Private when !IsDirectConversation => true,
        Model.ChannelType.Private =>
            DirectRequestStatus == DirectConversationRequestStatus.Accepted &&
            DirectBlockedByUserId == null &&
            IsPeerAvailable,
        _ => false
    };

    /// <summary>是否允许管理当前频道的共享消息置顶。</summary>
    public bool CanPinMessages => CanView && ChannelType switch
    {
        Model.ChannelType.Public or Model.ChannelType.Announcement =>
            CanManageChannel || ChannelMemberRole is MemberRole.Moderator or MemberRole.Owner,
        Model.ChannelType.Private when !IsDirectConversation =>
            ChannelMemberRole is MemberRole.Moderator or MemberRole.Owner,
        Model.ChannelType.Private =>
            DirectRequestStatus == DirectConversationRequestStatus.Accepted &&
            DirectBlockedByUserId == null &&
            IsPeerAvailable,
        _ => false
    };
}

/// <summary>本次请求中当前用户可读取的频道及必要展示元数据。</summary>
public sealed record ReadableChatChannelSnapshotItem(
    long ChannelId,
    string ChannelName,
    string? ChannelIcon,
    ChannelType ChannelType,
    ChatChannelAccessResult Access);

/// <summary>集中判定 REST、Hub 与 Chat 附件共享的频道访问边界</summary>
public interface IChatChannelAccessService
{
    /// <summary>获取用户对频道的访问能力；普通管理角色不得穿透 Private 频道成员边界。</summary>
    Task<ChatChannelAccessResult> GetAccessAsync(
        long tenantId,
        long userId,
        long channelId,
        bool canManageChannel = false);

    /// <summary>批量计算当前用户可读取频道；与单频道访问复用同一规则。</summary>
    Task<IReadOnlyList<ReadableChatChannelSnapshotItem>> GetReadableChannelSnapshotAsync(
        long tenantId,
        long userId);

    /// <summary>判断用户能否访问指定 Chat 附件引用；未完成可靠绑定时按消息引用恢复访问。</summary>
    Task<bool> CanAccessChatAttachmentAsync(
        long tenantId,
        long userId,
        long attachmentId,
        long? messageId = null);
}
