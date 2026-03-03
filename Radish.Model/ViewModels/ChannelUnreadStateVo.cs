namespace Radish.Model.ViewModels;

/// <summary>频道未读状态视图模型</summary>
public class ChannelUnreadStateVo
{
    public long VoChannelId { get; set; }

    public int VoUnreadCount { get; set; }

    public bool VoHasMention { get; set; }
}
