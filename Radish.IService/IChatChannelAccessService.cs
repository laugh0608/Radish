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
    bool IsPeerAvailable = true)
{
    public static ChatChannelAccessResult Unavailable { get; } =
        new(false, null, false, false, false, false, false);
}

/// <summary>集中判定 REST、Hub 与 Chat 附件共享的频道访问边界</summary>
public interface IChatChannelAccessService
{
    /// <summary>获取用户对频道的访问能力；普通管理角色不得穿透 Private 频道成员边界。</summary>
    Task<ChatChannelAccessResult> GetAccessAsync(
        long tenantId,
        long userId,
        long channelId,
        bool canManageChannel = false);

    /// <summary>判断用户能否访问指定 Chat 附件引用；未完成可靠绑定时按消息引用恢复访问。</summary>
    Task<bool> CanAccessChatAttachmentAsync(
        long tenantId,
        long userId,
        long attachmentId,
        long? messageId = null);
}
