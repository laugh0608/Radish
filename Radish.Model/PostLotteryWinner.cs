using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>帖子抽奖中奖记录</summary>
[SugarTable("PostLotteryWinner")]
[SugarIndex("idx_postlotterywinner_lottery", nameof(LotteryId), OrderByType.Asc)]
[SugarIndex("idx_postlotterywinner_lottery_user", nameof(LotteryId), OrderByType.Asc, nameof(UserId), OrderByType.Asc, IsUnique = true)]
public class PostLotteryWinner : RootEntityTKey<long>, ITenantEntity, IDeleteFilter
{
    /// <summary>抽奖 ID</summary>
    [SugarColumn(IsNullable = false)]
    public long LotteryId { get; set; }

    /// <summary>帖子 ID</summary>
    [SugarColumn(IsNullable = false)]
    public long PostId { get; set; }

    /// <summary>中奖用户 ID</summary>
    [SugarColumn(IsNullable = false)]
    public long UserId { get; set; }

    /// <summary>中奖用户名</summary>
    [SugarColumn(Length = 100, IsNullable = false)]
    public string UserName { get; set; } = string.Empty;

    /// <summary>参与的父评论 ID</summary>
    [SugarColumn(IsNullable = true)]
    public long? CommentId { get; set; }

    /// <summary>评论内容快照</summary>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? CommentContentSnapshot { get; set; }

    /// <summary>开奖时间</summary>
    [SugarColumn(IsNullable = false)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime DrawnAt { get; set; } = DateTime.UtcNow;

    /// <summary>租户 ID</summary>
    [SugarColumn(IsNullable = false)]
    public long TenantId { get; set; } = 0;

    /// <summary>软删除标记</summary>
    [SugarColumn(IsNullable = false)]
    public bool IsDeleted { get; set; } = false;

    /// <summary>删除时间</summary>
    [SugarColumn(IsNullable = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? DeletedAt { get; set; }

    /// <summary>删除人</summary>
    [SugarColumn(Length = 50, IsNullable = true)]
    public string? DeletedBy { get; set; }

    /// <summary>创建时间</summary>
    [SugarColumn(IsNullable = false, IsOnlyIgnoreUpdate = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime CreateTime { get; set; } = DateTime.Now;

    /// <summary>创建人</summary>
    [SugarColumn(Length = 50, IsNullable = false)]
    public string CreateBy { get; set; } = "System";

    /// <summary>创建人 ID</summary>
    [SugarColumn(IsNullable = false)]
    public long CreateId { get; set; }

    /// <summary>修改时间</summary>
    [SugarColumn(IsNullable = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? ModifyTime { get; set; }

    /// <summary>修改人</summary>
    [SugarColumn(Length = 50, IsNullable = true)]
    public string? ModifyBy { get; set; }

    /// <summary>修改人 ID</summary>
    [SugarColumn(IsNullable = true)]
    public long? ModifyId { get; set; }
}
