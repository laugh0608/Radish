namespace Radish.Model.ViewModels;

/// <summary>频道在线成员视图模型</summary>
public class ChannelMemberVo
{
    public long VoUserId { get; set; }

    public string VoUserName { get; set; } = string.Empty;

    public string? VoUserAvatarUrl { get; set; }

    public bool VoIsOnline { get; set; }
}
