using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>帖子抽奖主体</summary>
[SugarTable("PostLottery")]
[SugarIndex("idx_postlottery_post", nameof(PostId), OrderByType.Asc, IsUnique = true)]
[SugarIndex("idx_postlottery_tenant_draw", nameof(TenantId), OrderByType.Asc, nameof(IsDrawn), OrderByType.Asc)]
public class PostLottery : RootEntityTKey<long>, ITenantEntity, IDeleteFilter
{
    /// <summary>帖子 ID</summary>
    [SugarColumn(IsNullable = false)]
    public long PostId { get; set; }

    /// <summary>奖品名称</summary>
    [SugarColumn(Length = 100, IsNullable = false)]
    public string PrizeName { get; set; } = string.Empty;

    /// <summary>奖品说明</summary>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? PrizeDescription { get; set; }

    /// <summary>可开奖时间</summary>
    [SugarColumn(IsNullable = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? DrawTime { get; set; }

    /// <summary>实际开奖时间</summary>
    [SugarColumn(IsNullable = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? DrawnAt { get; set; }

    /// <summary>计划中奖人数</summary>
    [SugarColumn(IsNullable = false)]
    public int WinnerCount { get; set; } = 1;

    /// <summary>参与人数快照</summary>
    [SugarColumn(IsNullable = false)]
    public int ParticipantCount { get; set; } = 0;

    /// <summary>是否已开奖</summary>
    [SugarColumn(IsNullable = false)]
    public bool IsDrawn { get; set; } = false;

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
