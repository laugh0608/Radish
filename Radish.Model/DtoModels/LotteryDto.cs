using System.ComponentModel.DataAnnotations;

namespace Radish.Model.DtoModels;

/// <summary>创建帖子抽奖 DTO</summary>
public class CreateLotteryDto
{
    /// <summary>奖品名称</summary>
    public string PrizeName { get; set; } = string.Empty;

    /// <summary>奖品说明</summary>
    public string? PrizeDescription { get; set; }

    /// <summary>截止/自动开奖时间</summary>
    public DateTime? DrawTime { get; set; }

    /// <summary>中奖人数</summary>
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
