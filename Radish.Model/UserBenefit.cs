using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using Radish.Shared.CustomEnum;
using SqlSugar;

namespace Radish.Model;

/// <summary>用户权益实体</summary>
/// <remarks>
/// 记录用户拥有的权益（徽章、头像框、称号等）
/// 使用雪花 ID 作为主键
/// </remarks>
[SugarTable("shop_user_benefit")]
[SugarIndex("idx_benefit_user", nameof(UserId), OrderByType.Asc)]
[SugarIndex("idx_benefit_type", nameof(BenefitType), OrderByType.Asc)]
[SugarIndex("idx_benefit_active", nameof(UserId), OrderByType.Asc, nameof(IsActive), OrderByType.Asc)]
[SugarIndex("idx_benefit_expires", nameof(ExpiresAt), OrderByType.Asc)]
public class UserBenefit : RootEntityTKey<long>, IHasUserId
{
    /// <summary>初始化默认用户权益实例</summary>
    public UserBenefit()
    {
        InitializeDefaults();
    }

    /// <summary>统一设置默认值</summary>
    private void InitializeDefaults()
    {
        UserId = 0;
        BenefitType = Shared.CustomEnum.BenefitType.Badge;
        BenefitValue = string.Empty;
        IsActive = false;
        IsExpired = false;
        TenantId = 0;
        CreateTime = DateTime.Now;
        CreateBy = "System";
        CreateId = 0;
    }

    #region 用户信息

    /// <summary>用户 ID</summary>
    [SugarColumn(IsNullable = false, ColumnDescription = "用户ID")]
    public long UserId { get; set; } = 0;

    #endregion

    #region 权益信息

    /// <summary>权益类型</summary>
    [SugarColumn(IsNullable = false, ColumnDescription = "权益类型")]
    public BenefitType BenefitType { get; set; }

    /// <summary>权益值</summary>
    /// <remarks>
    /// 根据类型不同含义不同：
    /// - 徽章：徽章资源标识符
    /// - 头像框：头像框资源标识符
    /// - 称号：称号文本
    /// - 主题：主题标识符
    /// - 经验加成：加成百分比（如 50 表示 +50%）
    /// - 萝卜币加成：加成百分比
    /// </remarks>
    [SugarColumn(Length = 500, IsNullable = false, ColumnDescription = "权益值")]
    public string BenefitValue { get; set; } = string.Empty;

    /// <summary>权益名称</summary>
    /// <remarks>显示名称，如"金色徽章"、"VIP头像框"</remarks>
    [SugarColumn(Length = 200, IsNullable = true, ColumnDescription = "权益名称")]
    public string? BenefitName { get; set; }

    /// <summary>权益图标</summary>
    [SugarColumn(Length = 500, IsNullable = true, ColumnDescription = "权益图标")]
    public string? BenefitIcon { get; set; }

    #endregion

    #region 来源信息

    /// <summary>来源订单 ID</summary>
    /// <remarks>通过购买获得时关联的订单 ID</remarks>
    [SugarColumn(IsNullable = true, ColumnDescription = "来源订单ID")]
    public long? SourceOrderId { get; set; }

    /// <summary>来源商品 ID</summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "来源商品ID")]
    public long? SourceProductId { get; set; }

    /// <summary>来源类型</summary>
    /// <remarks>Purchase/System/Activity/Gift</remarks>
    [SugarColumn(Length = 50, IsNullable = false, ColumnDescription = "来源类型")]
    public string SourceType { get; set; } = "Purchase";

    #endregion

    #region 有效期

    /// <summary>有效期类型</summary>
    [SugarColumn(IsNullable = false, ColumnDescription = "有效期类型")]
    public DurationType DurationType { get; set; } = DurationType.Permanent;

    /// <summary>生效时间</summary>
    [SugarColumn(IsNullable = false, ColumnDescription = "生效时间")]
    public DateTime EffectiveAt { get; set; } = DateTime.Now;

    /// <summary>到期时间</summary>
    /// <remarks>永久权益为 null</remarks>
    [SugarColumn(IsNullable = true, ColumnDescription = "到期时间")]
    public DateTime? ExpiresAt { get; set; }

    /// <summary>是否已过期</summary>
    /// <remarks>定时任务更新或查询时计算</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "是否已过期")]
    public bool IsExpired { get; set; } = false;

    #endregion

    #region 状态

    /// <summary>是否激活使用中</summary>
    /// <remarks>同类型权益只能激活一个</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "是否激活")]
    public bool IsActive { get; set; } = false;

    /// <summary>激活时间</summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "激活时间")]
    public DateTime? ActivatedAt { get; set; }

    #endregion

    #region 租户信息

    /// <summary>租户 Id</summary>
    [SugarColumn(IsNullable = false, ColumnDescription = "租户ID")]
    public long TenantId { get; set; } = 0;

    #endregion

    #region 审计信息

    /// <summary>创建时间</summary>
    [SugarColumn(IsNullable = false, IsOnlyIgnoreUpdate = true, ColumnDescription = "创建时间")]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime CreateTime { get; set; } = DateTime.Now;

    /// <summary>创建者名称</summary>
    [SugarColumn(Length = 50, IsNullable = false, ColumnDescription = "创建者名称")]
    public string CreateBy { get; set; } = "System";

    /// <summary>创建者 Id</summary>
    [SugarColumn(IsNullable = false, ColumnDescription = "创建者ID")]
    public long CreateId { get; set; } = 0;

    /// <summary>修改时间</summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "修改时间")]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? ModifyTime { get; set; }

    /// <summary>修改者名称</summary>
    [SugarColumn(Length = 50, IsNullable = true, ColumnDescription = "修改者名称")]
    public string? ModifyBy { get; set; }

    /// <summary>修改者 Id</summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "修改者ID")]
    public long? ModifyId { get; set; }

    #endregion
}
