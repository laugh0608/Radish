namespace Radish.Model.ViewModels;

/// <summary>频道视图模型</summary>
public class ChannelVo
{
    public long VoId { get; set; }

    public long? VoCategoryId { get; set; }

    public string VoName { get; set; } = string.Empty;

    public string VoSlug { get; set; } = string.Empty;

    public string? VoDescription { get; set; }

    public string? VoIconEmoji { get; set; }

    public ChannelType VoType { get; set; }

    public int VoSort { get; set; }

    public int VoUnreadCount { get; set; }

    public bool VoHasMention { get; set; }

    public ChannelMessageVo? VoLastMessage { get; set; }
}
