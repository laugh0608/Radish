using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>宠物状态变化流水</summary>
[SugarTable("PetStatLog")]
[SugarIndex("idx_petstatlog_user_time", nameof(UserId), OrderByType.Asc, nameof(CreateTime), OrderByType.Desc)]
[SugarIndex("idx_petstatlog_pet_time", nameof(PetProfileId), OrderByType.Asc, nameof(CreateTime), OrderByType.Desc)]
[SugarIndex("idx_petstatlog_idempotency", nameof(IdempotencyKey), OrderByType.Asc, IsUnique = true)]
public class PetStatLog : RootEntityTKey<long>, ITenantEntity, IDeleteFilter
{
    /// <summary>用户 ID</summary>
    [SugarColumn(IsNullable = false)]
    public long UserId { get; set; }

    /// <summary>宠物主档 ID</summary>
    [SugarColumn(IsNullable = false)]
    public long PetProfileId { get; set; }

    /// <summary>宠物公开 ID 快照</summary>
    [SugarColumn(Length = 36, IsNullable = false)]
    public string PetPublicId { get; set; } = string.Empty;

    /// <summary>动作类型</summary>
    [SugarColumn(Length = 20, IsNullable = false)]
    public string ActionType { get; set; } = string.Empty;

    /// <summary>来源</summary>
    [SugarColumn(Length = 40, IsNullable = false)]
    public string Source { get; set; } = "care";

    /// <summary>幂等键</summary>
    [SugarColumn(Length = 80, IsNullable = false)]
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>变更前饱食度</summary>
    [SugarColumn(IsNullable = false)]
    public int BeforeSatiety { get; set; }

    /// <summary>变更后饱食度</summary>
    [SugarColumn(IsNullable = false)]
    public int AfterSatiety { get; set; }

    /// <summary>变更前清洁度</summary>
    [SugarColumn(IsNullable = false)]
    public int BeforeCleanliness { get; set; }

    /// <summary>变更后清洁度</summary>
    [SugarColumn(IsNullable = false)]
    public int AfterCleanliness { get; set; }

    /// <summary>变更前精力</summary>
    [SugarColumn(IsNullable = false)]
    public int BeforeEnergy { get; set; }

    /// <summary>变更后精力</summary>
    [SugarColumn(IsNullable = false)]
    public int AfterEnergy { get; set; }

    /// <summary>成长值变化</summary>
    [SugarColumn(IsNullable = false)]
    public long GrowthDelta { get; set; }

    /// <summary>结果消息</summary>
    [SugarColumn(Length = 200, IsNullable = false)]
    public string Message { get; set; } = string.Empty;

    /// <summary>租户 ID</summary>
    [SugarColumn(IsNullable = false)]
    public long TenantId { get; set; } = 0;

    /// <summary>是否软删除</summary>
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
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    /// <summary>创建者</summary>
    [SugarColumn(Length = 50, IsNullable = false)]
    public string CreateBy { get; set; } = "System";

    /// <summary>创建者 ID</summary>
    [SugarColumn(IsNullable = false)]
    public long CreateId { get; set; } = 0;
}
