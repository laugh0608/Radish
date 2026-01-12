namespace Radish.Shared.CustomEnum;

/// <summary>商品类型枚举</summary>
/// <remarks>
/// 定义商城支持的商品类型
/// 每种类型有不同的购买和使用逻辑
/// </remarks>
public enum ProductType
{
    /// <summary>虚拟权益</summary>
    /// <remarks>购买后立即生效，有有效期。例如：专属徽章、头像框、称号、主题皮肤</remarks>
    Benefit = 1,

    /// <summary>虚拟消耗品</summary>
    /// <remarks>存入背包，使用时消耗。例如：改名卡、置顶卡、高亮卡、经验卡</remarks>
    Consumable = 2,

    /// <summary>实物商品（暂不实现）</summary>
    /// <remarks>需要发货。例如：周边、礼品</remarks>
    Physical = 99
}

/// <summary>权益类型枚举</summary>
/// <remarks>
/// 定义用户购买商品后获得的权益效果类型
/// </remarks>
public enum BenefitType
{
    /// <summary>专属徽章</summary>
    /// <remarks>显示在用户名旁</remarks>
    Badge = 1,

    /// <summary>头像框</summary>
    /// <remarks>装饰用户头像</remarks>
    AvatarFrame = 2,

    /// <summary>专属称号</summary>
    /// <remarks>显示在用户名下方</remarks>
    Title = 3,

    /// <summary>主题皮肤</summary>
    /// <remarks>个性化界面主题</remarks>
    Theme = 4,

    /// <summary>功能解锁</summary>
    /// <remarks>解锁特定功能</remarks>
    FeatureUnlock = 5,

    /// <summary>经验加成</summary>
    /// <remarks>经验值获取加成</remarks>
    ExpBoost = 6,

    /// <summary>萝卜币加成</summary>
    /// <remarks>萝卜币获取加成</remarks>
    CoinBoost = 7
}

/// <summary>消耗品类型枚举</summary>
/// <remarks>
/// 定义一次性使用的道具类型
/// </remarks>
public enum ConsumableType
{
    /// <summary>改名卡</summary>
    /// <remarks>修改用���昵称</remarks>
    RenameCard = 1,

    /// <summary>帖子置顶卡</summary>
    /// <remarks>置顶帖子 N 小时</remarks>
    PostPinCard = 2,

    /// <summary>帖子高亮卡</summary>
    /// <remarks>帖子标题高亮 N 小时</remarks>
    PostHighlightCard = 3,

    /// <summary>经验卡</summary>
    /// <remarks>立即获得 N 点经验值</remarks>
    ExpCard = 4,

    /// <summary>萝卜币红包</summary>
    /// <remarks>立即获得 N 萝卜币</remarks>
    CoinCard = 5,

    /// <summary>双倍经验卡</summary>
    /// <remarks>N 小时内经验值 ×2</remarks>
    DoubleExpCard = 6,

    /// <summary>抽奖券（预留）</summary>
    /// <remarks>参与抽奖活动</remarks>
    LotteryTicket = 99
}

/// <summary>订单状态枚举</summary>
/// <remarks>
/// 定义订单的生命周期状态
/// </remarks>
public enum OrderStatus
{
    /// <summary>待支付</summary>
    /// <remarks>订单已创建，等待支付</remarks>
    Pending = 0,

    /// <summary>已支付</summary>
    /// <remarks>支付成功，等待发放权益</remarks>
    Paid = 1,

    /// <summary>已完成</summary>
    /// <remarks>权益已发放，订单完成</remarks>
    Completed = 2,

    /// <summary>已取消</summary>
    /// <remarks>用户取消或超时取消</remarks>
    Cancelled = 3,

    /// <summary>已退款</summary>
    /// <remarks>已退款（虚拟商品暂不支持）</remarks>
    Refunded = 4,

    /// <summary>发放失败</summary>
    /// <remarks>权益发放失败，需人工处理</remarks>
    Failed = 5
}

/// <summary>库存类型枚举</summary>
/// <remarks>
/// 定义商品库存管理方式
/// </remarks>
public enum StockType
{
    /// <summary>无限库存</summary>
    /// <remarks>不限制购买数量</remarks>
    Unlimited = 0,

    /// <summary>限量库存</summary>
    /// <remarks>有固定库存数量，使用乐观锁防止超卖</remarks>
    Limited = 1
}

/// <summary>有效期类型枚举</summary>
/// <remarks>
/// 定义权益和消耗品的有效期计算方式
/// </remarks>
public enum DurationType
{
    /// <summary>永久有效</summary>
    Permanent = 0,

    /// <summary>固定天数</summary>
    /// <remarks>从购买/激活时开始计算</remarks>
    Days = 1,

    /// <summary>固定到期时间</summary>
    /// <remarks>活动商品，指定到期日期</remarks>
    FixedDate = 2
}
