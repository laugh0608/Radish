using Radish.Shared.CustomEnum;

namespace Radish.Model.ViewModels;

/// <summary>订单视图模型</summary>
public class OrderVo
{
    /// <summary>订单 ID</summary>
    public long Id { get; set; }

    /// <summary>订单号</summary>
    public string OrderNo { get; set; } = string.Empty;

    /// <summary>用户 ID</summary>
    public long UserId { get; set; }

    /// <summary>用户名</summary>
    /// <remarks>运行时填充</remarks>
    public string? UserName { get; set; }

    /// <summary>商品 ID</summary>
    public long ProductId { get; set; }

    /// <summary>商品名称</summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>商品图标</summary>
    public string? ProductIcon { get; set; }

    /// <summary>商品类型</summary>
    public ProductType ProductType { get; set; }

    /// <summary>商品类型显示名称</summary>
    public string ProductTypeDisplay => ProductType switch
    {
        ProductType.Benefit => "权益",
        ProductType.Consumable => "消耗品",
        ProductType.Physical => "实物",
        _ => "未知"
    };

    /// <summary>权益类型</summary>
    public BenefitType? BenefitType { get; set; }

    /// <summary>消耗品类型</summary>
    public ConsumableType? ConsumableType { get; set; }

    /// <summary>购买数量</summary>
    public int Quantity { get; set; }

    /// <summary>单价（胡萝卜）</summary>
    public long UnitPrice { get; set; }

    /// <summary>总价（胡萝卜）</summary>
    public long TotalPrice { get; set; }

    /// <summary>订单状态</summary>
    public OrderStatus Status { get; set; }

    /// <summary>订单状态显示名称</summary>
    public string StatusDisplay => Status switch
    {
        OrderStatus.Pending => "待支付",
        OrderStatus.Paid => "已支付",
        OrderStatus.Completed => "已完成",
        OrderStatus.Cancelled => "已取消",
        OrderStatus.Refunded => "已退款",
        OrderStatus.Failed => "发放失败",
        _ => "未知"
    };

    /// <summary>权益到期时间</summary>
    public DateTime? BenefitExpiresAt { get; set; }

    /// <summary>有效期显示文本</summary>
    public string? DurationDisplay { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }

    /// <summary>支付时间</summary>
    public DateTime? PaidTime { get; set; }

    /// <summary>完成时间</summary>
    public DateTime? CompletedTime { get; set; }

    /// <summary>取消时间</summary>
    public DateTime? CancelledTime { get; set; }

    /// <summary>取消原因</summary>
    public string? CancelReason { get; set; }

    /// <summary>失败原因</summary>
    public string? FailReason { get; set; }
}

/// <summary>订单列表项视图模型</summary>
public class OrderListItemVo
{
    /// <summary>订单 ID</summary>
    public long Id { get; set; }

    /// <summary>订单号</summary>
    public string OrderNo { get; set; } = string.Empty;

    /// <summary>商品名称</summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>商品图标</summary>
    public string? ProductIcon { get; set; }

    /// <summary>购买数量</summary>
    public int Quantity { get; set; }

    /// <summary>总价（胡萝卜）</summary>
    public long TotalPrice { get; set; }

    /// <summary>订单状态</summary>
    public OrderStatus Status { get; set; }

    /// <summary>订单状态显示名称</summary>
    public string StatusDisplay => Status switch
    {
        OrderStatus.Pending => "待支付",
        OrderStatus.Paid => "已支付",
        OrderStatus.Completed => "已完成",
        OrderStatus.Cancelled => "已取消",
        OrderStatus.Refunded => "已退款",
        OrderStatus.Failed => "发放失败",
        _ => "未知"
    };

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }
}

/// <summary>创建订单 DTO</summary>
public class CreateOrderDto
{
    /// <summary>商品 ID</summary>
    public long ProductId { get; set; }

    /// <summary>购买数量</summary>
    public int Quantity { get; set; } = 1;

    /// <summary>用户备注</summary>
    public string? UserRemark { get; set; }
}

/// <summary>购买结果 DTO</summary>
public class PurchaseResultDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; set; }

    /// <summary>订单 ID</summary>
    public long? OrderId { get; set; }

    /// <summary>订单号</summary>
    public string? OrderNo { get; set; }

    /// <summary>错误信息</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>用户权益 ID</summary>
    /// <remarks>购买成功后发放的权益 ID</remarks>
    public long? UserBenefitId { get; set; }

    /// <summary>扣除的萝卜币数量</summary>
    public long? DeductedCoins { get; set; }

    /// <summary>剩余萝卜币余额</summary>
    public long? RemainingBalance { get; set; }
}
