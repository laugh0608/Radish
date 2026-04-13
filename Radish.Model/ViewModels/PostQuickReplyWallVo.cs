namespace Radish.Model.ViewModels;

/// <summary>帖子轻回应墙视图模型</summary>
public class PostQuickReplyWallVo
{
    public List<PostQuickReplyVo> VoItems { get; set; } = [];

    public int VoTotal { get; set; }
}
