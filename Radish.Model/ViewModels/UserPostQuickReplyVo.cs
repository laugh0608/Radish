namespace Radish.Model.ViewModels;

/// <summary>个人主页轻回应列表项</summary>
public class UserPostQuickReplyVo
{
    public long VoId { get; set; }

    public long VoPostId { get; set; }

    public string VoPostTitle { get; set; } = string.Empty;

    public string VoContent { get; set; } = string.Empty;

    public DateTime VoCreateTime { get; set; }
}
