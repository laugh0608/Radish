using System.ComponentModel.DataAnnotations;

namespace Radish.Model.DtoModels;

/// <summary>创建帖子轻回应请求</summary>
public class CreatePostQuickReplyDto
{
    /// <summary>帖子 Id</summary>
    [Range(1, long.MaxValue, ErrorMessage = "postId 必须大于0")]
    public long PostId { get; set; }

    /// <summary>轻回应内容</summary>
    [Required(ErrorMessage = "轻回应内容不能为空")]
    [StringLength(24, ErrorMessage = "轻回应内容不能超过24个字符")]
    public string Content { get; set; } = string.Empty;
}
