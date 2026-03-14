namespace Radish.Model.ViewModels;

/// <summary>
/// 帖子问答视图模型
/// </summary>
public class PostQuestionVo
{
    /// <summary>帖子 ID</summary>
    public long VoPostId { get; set; }

    /// <summary>是否已解决</summary>
    public bool VoIsSolved { get; set; }

    /// <summary>已采纳答案 ID</summary>
    public long? VoAcceptedAnswerId { get; set; }

    /// <summary>回答数</summary>
    public int VoAnswerCount { get; set; }

    /// <summary>回答列表</summary>
    public List<PostAnswerVo> VoAnswers { get; set; } = new();
}

/// <summary>
/// 帖子回答视图模型
/// </summary>
public class PostAnswerVo
{
    /// <summary>回答 ID</summary>
    public long VoAnswerId { get; set; }

    /// <summary>帖子 ID</summary>
    public long VoPostId { get; set; }

    /// <summary>作者 ID</summary>
    public long VoAuthorId { get; set; }

    /// <summary>作者名称</summary>
    public string VoAuthorName { get; set; } = string.Empty;

    /// <summary>作者头像 URL</summary>
    public string? VoAuthorAvatarUrl { get; set; }

    /// <summary>回答内容</summary>
    public string VoContent { get; set; } = string.Empty;

    /// <summary>是否已采纳</summary>
    public bool VoIsAccepted { get; set; }

    /// <summary>创建时间</summary>
    public DateTime VoCreateTime { get; set; }
}
