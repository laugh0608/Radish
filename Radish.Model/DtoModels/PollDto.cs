using System.ComponentModel.DataAnnotations;

namespace Radish.Model.DtoModels;

/// <summary>
/// 创建帖子投票 DTO
/// </summary>
public class CreatePollDto
{
    /// <summary>投票问题</summary>
    public string Question { get; set; } = string.Empty;

    /// <summary>截止时间（可空）</summary>
    public DateTime? EndTime { get; set; }

    /// <summary>投票选项列表</summary>
    public List<PollOptionDto> Options { get; set; } = new();
}

/// <summary>
/// 投票选项 DTO
/// </summary>
public class PollOptionDto
{
    /// <summary>选项文本</summary>
    public string OptionText { get; set; } = string.Empty;

    /// <summary>排序号（可空，默认按输入顺序）</summary>
    public int? SortOrder { get; set; }
}

/// <summary>
/// 提交投票 DTO
/// </summary>
public class VotePollDto
{
    /// <summary>帖子 ID</summary>
    [Required(ErrorMessage = "帖子ID不能为空")]
    [Range(1, long.MaxValue, ErrorMessage = "帖子ID必须大于0")]
    public long PostId { get; set; }

    /// <summary>投票选项 ID</summary>
    [Required(ErrorMessage = "投票选项ID不能为空")]
    [Range(1, long.MaxValue, ErrorMessage = "投票选项ID必须大于0")]
    public long OptionId { get; set; }
}

/// <summary>
/// 结束投票 DTO
/// </summary>
public class ClosePollDto
{
    /// <summary>帖子 ID</summary>
    [Required(ErrorMessage = "帖子ID不能为空")]
    [Range(1, long.MaxValue, ErrorMessage = "帖子ID必须大于0")]
    public long PostId { get; set; }
}
