using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using Radish.Shared.CustomEnum;
using SqlSugar;

namespace Radish.Model;

/// <summary>用户每种持续权益的当前唯一选择。</summary>
[SugarTable("ShopUserActiveBenefit")]
[SugarIndex(
    "idx_active_benefit_tenant_user_type",
    nameof(TenantId), OrderByType.Asc,
    nameof(UserId), OrderByType.Asc,
    nameof(BenefitType), OrderByType.Asc,
    IsUnique = true)]
[SugarIndex(
    "idx_active_benefit_tenant_benefit",
    nameof(TenantId), OrderByType.Asc,
    nameof(BenefitId), OrderByType.Asc)]
public sealed class UserActiveBenefit : RootEntityTKey<long>, IHasUserId, ITenantEntity
{
    [SugarColumn(IsNullable = false, ColumnDescription = "租户ID")]
    public long TenantId { get; set; }

    [SugarColumn(IsNullable = false, ColumnDescription = "用户ID")]
    public long UserId { get; set; }

    [SugarColumn(IsNullable = false, ColumnDescription = "权益类型")]
    public BenefitType BenefitType { get; set; }

    [SugarColumn(IsNullable = false, ColumnDescription = "当前权益ID")]
    public long BenefitId { get; set; }

    [SugarColumn(IsNullable = false, ColumnDescription = "选择时间")]
    public DateTime SelectedAt { get; set; } = DateTime.UtcNow;

    [SugarColumn(IsNullable = false, IsOnlyIgnoreUpdate = true, ColumnDescription = "创建时间")]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    [SugarColumn(Length = 50, IsNullable = false, ColumnDescription = "创建者名称")]
    public string CreateBy { get; set; } = "System";

    [SugarColumn(IsNullable = false, ColumnDescription = "创建者ID")]
    public long CreateId { get; set; }

    [SugarColumn(IsNullable = true, ColumnDescription = "修改时间")]
    public DateTime? ModifyTime { get; set; }

    [SugarColumn(Length = 50, IsNullable = true, ColumnDescription = "修改者名称")]
    public string? ModifyBy { get; set; }

    [SugarColumn(IsNullable = true, ColumnDescription = "修改者ID")]
    public long? ModifyId { get; set; }
}
