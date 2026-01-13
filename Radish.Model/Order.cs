using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using Radish.Shared.CustomEnum;
using SqlSugar;

namespace Radish.Model;

/// <summary>订单实体</summary>
/// <remarks>
/// 商城订单记录，记录用户购买商品的完整信息
/// 使用雪花 ID 作为主键
/// </remarks>
[SugarTable("shop_order")]
[SugarIndex("idx_order_no", nameof(OrderNo), OrderByType.Asc)]
[SugarIndex("idx_order_user", nameof(UserId), OrderByType.Asc, nameof(CreateTime), OrderByType.Desc)]
[SugarIndex("idx_order_status", nameof(Status), OrderByType.Asc)]
[SugarIndex("idx_order_product", nameof(ProductId), OrderByType.Asc)]
public class Order : RootEntityTKey<long>, IHasUserId
{
    /// <summary>初始化默认订单实例</summary>
    public Order()
    {
        InitializeDefaults();
    }

    /// <summary>统一设置默认值</summary>
    private void InitializeDefaults()
    {
        OrderNo = string.Empty;
        UserId = 0;
        ProductId = 0;
        ProductName = string.Empty;
        Quantity = 1;
        UnitPrice = 0;
        TotalPrice = 0;
        Status = OrderStatus.Pending;
        TenantId = 0;
        CreateTime = DateTime.Now;
        CreateBy = "System";
        CreateId = 0;
    }

    #region 订单基础信息

    /// <summary>订单号</summary>
    /// <remarks>格式：ORD_{SnowflakeId}</remarks>
    [SugarColumn(Length = 64, IsNullable = false, ColumnDescription = "订单号")]
    public string OrderNo { get; set; } = string.Empty;

    /// <summary>用户 ID</summary>
    [SugarColumn(IsNullable = false, ColumnDescription = "用户ID")]
    public long UserId { get; set; } = 0;

    #endregion

    #region 商品信息（快照）

    /// <summary>商品 ID</summary>
    [SugarColumn(IsNullable = false, ColumnDescription = "商品ID")]
    public long ProductId { get; set; } = 0;

    /// <summary>商品名称（快照）</summary>
    /// <remarks>下单时的商品名称，防止商品修改后订单信息不一致</remarks>
    [SugarColumn(Length = 200, IsNullable = false, ColumnDescription = "商品名称")]
    public string ProductName { get; set; } = string.Empty;

    /// <summary>商品图标（快照）</summary>
    [SugarColumn(Length = 500, IsNullable = true, ColumnDescription = "商品图标")]
    public string? ProductIcon { get; set; }

    /// <summary>商品类型（快照）</summary>
    [SugarColumn(IsNullable = false, ColumnDescription = "商品类型")]
    public ProductType ProductType { get; set; }

    /// <summary>权益类型（快照）</summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "权益类型")]
    public BenefitType? BenefitType { get; set; }

    /// <summary>消耗品类型（快照）</summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "消耗品类型")]
    public ConsumableType? ConsumableType { get; set; }

    /// <summary>权益值（快照）</summary>
    [SugarColumn(Length = 500, IsNullable = true, ColumnDescription = "权益值")]
    public string? BenefitValue { get; set; }

    /// <summary>有效期类型（快照）</summary>
    [SugarColumn(IsNullable = false, ColumnDescription = "有效期类型")]
    public DurationType DurationType { get; set; }

    /// <summary>有效期天数（快照）</summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "有效期天数")]
    public int? DurationDays { get; set; }

    #endregion

    #region 价格信息

    /// <summary>购买数量</summary>
    [SugarColumn(IsNullable = false, ColumnDescription = "购买数量")]
    public int Quantity { get; set; } = 1;

    /// <summary>单价（胡萝卜）</summary>
    /// <remarks>下单时的商品单价</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "单价")]
    public long UnitPrice { get; set; } = 0;

    /// <summary>总价（胡萝卜）</summary>
    /// <remarks>Quantity × UnitPrice</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "总价")]
    public long TotalPrice { get; set; } = 0;

    #endregion

    #region 订单状态

    /// <summary>订单状态</summary>
    [SugarColumn(IsNullable = false, ColumnDescription = "订单状态")]
    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    /// <summary>支付时间</summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "支付时间")]
    public DateTime? PaidTime { get; set; }

    /// <summary>完成时间</summary>
    /// <remarks>权益发放完成的时间</remarks>
    [SugarColumn(IsNullable = true, ColumnDescription = "完成时间")]
    public DateTime? CompletedTime { get; set; }

    /// <summary>取消时间</summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "取消时间")]
    public DateTime? CancelledTime { get; set; }

    /// <summary>取消原因</summary>
    [SugarColumn(Length = 500, IsNullable = true, ColumnDescription = "取消原因")]
    public string? CancelReason { get; set; }

    /// <summary>失败原因</summary>
    /// <remarks>权益发放失败时的错误信息</remarks>
    [SugarColumn(Length = 1000, IsNullable = true, ColumnDescription = "失败原因")]
    public string? FailReason { get; set; }

    #endregion

    #region 权益发放信息

    /// <summary>用户权益 ID</summary>
    /// <remarks>权益发放成功后关联的 UserBenefit 记录 ID</remarks>
    [SugarColumn(IsNullable = true, ColumnDescription = "用户权益ID")]
    public long? UserBenefitId { get; set; }

    /// <summary>权益到期时间</summary>
    /// <remarks>计算后的权益到期时间</remarks>
    [SugarColumn(IsNullable = true, ColumnDescription = "权益到期时间")]
    public DateTime? BenefitExpiresAt { get; set; }

    #endregion

    #region 萝卜币交易关联

    /// <summary>萝卜币交易 ID</summary>
    /// <remarks>关联的 CoinTransaction 记录 ID</remarks>
    [SugarColumn(IsNullable = true, ColumnDescription = "萝卜币交易ID")]
    public long? CoinTransactionId { get; set; }

    #endregion

    #region 备注

    /// <summary>用户备注</summary>
    [SugarColumn(Length = 500, IsNullable = true, ColumnDescription = "用户备注")]
    public string? UserRemark { get; set; }

    /// <summary>管理员备注</summary>
    [SugarColumn(Length = 500, IsNullable = true, ColumnDescription = "管理员备注")]
    public string? AdminRemark { get; set; }

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
