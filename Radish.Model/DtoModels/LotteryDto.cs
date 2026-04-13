using System.ComponentModel.DataAnnotations;

namespace Radish.Model.DtoModels;

/// <summary>创建帖子抽奖 DTO</summary>
public class CreateLotteryDto
{
    /// <summary>奖品名称</summary>
    [Required(ErrorMessage = "奖品名称不能为空")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "奖品名称长度必须在1-100个字符之间")]
    public string PrizeName { get; set; } = string.Empty;

    /// <summary>奖品说明</summary>
    [StringLength(500, ErrorMessage = "奖品说明长度不能超过500个字符")]
    public string? PrizeDescription { get; set; }

    /// <summary>截止/自动开奖时间</summary>
    public DateTime? DrawTime { get; set; }

    /// <summary>中奖人数</summary>
    [Range(1, 20, ErrorMessage = "中奖人数必须在1到20之间")]
    public int WinnerCount { get; set; } = 1;
}

/// <summary>手动开奖 DTO</summary>
public class DrawLotteryDto
{
    /// <summary>帖子 ID</summary>
    [Required(ErrorMessage = "帖子ID不能为空")]
    [Range(1, long.MaxValue, ErrorMessage = "帖子ID必须大于0")]
    public long PostId { get; set; }
}
