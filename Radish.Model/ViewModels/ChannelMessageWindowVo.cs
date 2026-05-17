namespace Radish.Model.ViewModels;

/// <summary>频道消息窗口视图模型</summary>
public class ChannelMessageWindowVo
{
    public long VoChannelId { get; set; }

    public long VoAnchorMessageId { get; set; }

    public List<ChannelMessageVo> VoMessages { get; set; } = new();

    public bool VoHasMoreBefore { get; set; }

    public bool VoHasMoreAfter { get; set; }
}
