namespace Radish.Model.ViewModels;

/// <summary>
/// 未读消息数量ViewModel
/// </summary>
public class VoUnreadMessageCount
{
    /// <summary>
    /// 用户ID
    /// </summary>
    public long VoUserId { get; set; }

    /// <summary>
    /// 未读消息数量
    /// </summary>
    public int VoUnreadCount { get; set; }
}