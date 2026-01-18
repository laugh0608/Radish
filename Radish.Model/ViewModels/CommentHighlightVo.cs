using System;

namespace Radish.Model.ViewModels;

/// <summary>
/// 神评/沙发视图模型
/// </summary>
public class CommentHighlightVo
{
    /// <summary>记录 ID</summary>
    public long VoId { get; set; }

    /// <summary>帖子 ID</summary>
    public long VoPostId { get; set; }

    /// <summary>评论 ID</summary>
    public long VoCommentId { get; set; }

    /// <summary>父评论 ID（沙发专用）</summary>
    public long? VoParentCommentId { get; set; }

    /// <summary>高亮类型：1=神评，2=沙发</summary>
    public int VoHighlightType { get; set; }

    /// <summary>统计日期</summary>
    public DateTime VoStatDate { get; set; }

    /// <summary>点赞数快照</summary>
    public int VoLikeCount { get; set; }

    /// <summary>排名</summary>
    public int VoRank { get; set; }

    /// <summary>评论内容快照</summary>
    public string? VoContentSnapshot { get; set; }

    /// <summary>作者 ID</summary>
    public long VoAuthorId { get; set; }

    /// <summary>作者名称</summary>
    public string VoAuthorName { get; set; } = string.Empty;

    /// <summary>是否当前有效</summary>
    public bool VoIsCurrent { get; set; }

    /// <summary>创建时间</summary>
    public DateTime VoCreateTime { get; set; }
}
