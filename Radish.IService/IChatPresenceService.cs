namespace Radish.IService;

/// <summary>聊天室在线状态服务</summary>
public interface IChatPresenceService
{
    /// <summary>记录连接加入频道</summary>
    void JoinChannel(string connectionId, long tenantId, long channelId, long userId);

    /// <summary>记录连接离开频道</summary>
    void LeaveChannel(string connectionId, long tenantId, long channelId, long userId);

    /// <summary>连接断开，移除其所有频道在线状态</summary>
    void RemoveConnection(string connectionId, long userId);

    /// <summary>获取频道在线用户 Id 列表</summary>
    List<long> GetOnlineUserIds(long tenantId, long channelId);
}
