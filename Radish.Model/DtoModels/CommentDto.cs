using System.ComponentModel.DataAnnotations;

namespace Radish.Model.DtoModels;

/// <summary>
/// 创建评论请求对象
/// </summary>
public class CreateCommentDto
{
    /// <summary>评论内容</summary>
    [Required(ErrorMessage = "评论内容不能为空")]
    [StringLength(2000, ErrorMessage = "评论内容不能超过2000个字符")]
    public string Content { get; set; } = string.Empty;

    /// <summary>客户端提交意图 ID，用于网络重试和重复提交保护</summary>
    [StringLength(80, ErrorMessage = "clientSubmissionId 长度不能超过 80 个字符")]
    public string? ClientSubmissionId { get; set; }

    /// <summary>帖子 ID</summary>
    [Required(ErrorMessage = "帖子ID不能为空")]
    public long PostId { get; set; }

    /// <summary>父评论 ID（回复评论时使用）</summary>
    public long? ParentId { get; set; }

    /// <summary>被回复评论 ID</summary>
    public long? ReplyToCommentId { get; set; }

    /// <summary>被回复评论摘要</summary>
    [StringLength(160, ErrorMessage = "被回复评论摘要不能超过160个字符")]
    public string? ReplyToCommentSnapshot { get; set; }

    /// <summary>被回复用户 ID（@某人时使用）</summary>
    public long? ReplyToUserId { get; set; }

    /// <summary>被回复用户名称</summary>
    [StringLength(50, ErrorMessage = "用户名称不能超过50个字符")]
    public string? ReplyToUserName { get; set; }
}

/// <summary>恢复帖子或评论版本请求。</summary>
public sealed class RestoreForumContentRevisionDto
{
    [Range(1, long.MaxValue, ErrorMessage = "targetId 必须大于0")]
    public long TargetId { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "revisionId 必须大于0")]
    public long RevisionId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "expectedContentRevision 必须大于0")]
    public int ExpectedContentRevision { get; set; }

    [Required(ErrorMessage = "clientSubmissionId 不能为空")]
    [StringLength(80, MinimumLength = 1, ErrorMessage = "clientSubmissionId 长度必须在 1 到 80 个字符之间")]
    public string ClientSubmissionId { get; set; } = string.Empty;
}

/// <summary>
/// 更新评论请求对象
/// </summary>
public class UpdateCommentDto
{
    /// <summary>评论 ID</summary>
    [Required(ErrorMessage = "评论ID不能为空")]
    public long CommentId { get; set; }

    /// <summary>新的评论内容</summary>
    [Required(ErrorMessage = "评论内容不能为空")]
    [StringLength(2000, ErrorMessage = "评论内容不能超过2000个字符")]
    public string Content { get; set; } = string.Empty;

    /// <summary>提交时看到的当前内容版本号，用于防止并发覆盖</summary>
    [Range(1, int.MaxValue, ErrorMessage = "expectedContentRevision 必须大于0")]
    public int ExpectedContentRevision { get; set; }

    /// <summary>客户端提交意图 ID，用于网络重试和重复提交保护</summary>
    [StringLength(80, ErrorMessage = "clientSubmissionId 长度不能超过 80 个字符")]
    public string? ClientSubmissionId { get; set; }
}
