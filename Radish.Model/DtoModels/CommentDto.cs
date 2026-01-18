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

    /// <summary>帖子 ID</summary>
    [Required(ErrorMessage = "帖子ID不能为空")]
    public long PostId { get; set; }

    /// <summary>父评论 ID（回复评论时使用）</summary>
    public long? ParentId { get; set; }

    /// <summary>被回复用户 ID（@某人时使用）</summary>
    public long? ReplyToUserId { get; set; }

    /// <summary>被回复用户名称</summary>
    [StringLength(50, ErrorMessage = "用户名称不能超过50个字符")]
    public string? ReplyToUserName { get; set; }
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
}