using System.ComponentModel.DataAnnotations;

namespace Radish.Model.DtoModels;

/// <summary>
/// 设置帖子置顶状态请求 DTO
/// </summary>
public class SetPostTopDto
{
    /// <summary>帖子 ID</summary>
    [Required(ErrorMessage = "帖子ID不能为空")]
    [Range(1, long.MaxValue, ErrorMessage = "帖子ID必须大于0")]
    public long PostId { get; set; }

    /// <summary>是否置顶</summary>
    public bool IsTop { get; set; }
}
