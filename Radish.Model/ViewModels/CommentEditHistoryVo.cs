namespace Radish.Model.ViewModels;

/// <summary>
/// 评论编辑历史视图模型
/// </summary>
public class CommentEditHistoryVo
{
    /// <summary>记录 Id</summary>
    public long VoId { get; set; }

    /// <summary>评论 Id</summary>
    public long VoCommentId { get; set; }

    /// <summary>所属帖子 Id</summary>
    public long VoPostId { get; set; }

    /// <summary>编辑序号</summary>
    public int VoEditSequence { get; set; }

    /// <summary>编辑前内容</summary>
    public string VoOldContent { get; set; } = string.Empty;

    /// <summary>编辑后内容</summary>
    public string VoNewContent { get; set; } = string.Empty;

    /// <summary>编辑人 Id</summary>
    public long VoEditorId { get; set; }

    /// <summary>编辑人名称</summary>
    public string VoEditorName { get; set; } = string.Empty;

    /// <summary>编辑时间</summary>
    public DateTime VoEditedAt { get; set; }

    /// <summary>创建时间</summary>
    public DateTime VoCreateTime { get; set; }
}

