namespace Radish.Model.ViewModels;

/// <summary>
/// 萝卜币交易记录视图模型
/// </summary>
public class CoinTransactionVo
{
    /// <summary>
    /// 交易 ID
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 交易流水号
    /// </summary>
    public string TransactionNo { get; set; } = string.Empty;

    /// <summary>
    /// 发起方用户 ID
    /// </summary>
    /// <remarks>NULL 表示系统发起</remarks>
    public long? FromUserId { get; set; }

    /// <summary>
    /// 发起方用户名
    /// </summary>
    /// <remarks>用于前端显示，避免前端再次查询</remarks>
    public string? FromUserName { get; set; }

    /// <summary>
    /// 接收方用户 ID
    /// </summary>
    /// <remarks>NULL 表示系统接收</remarks>
    public long? ToUserId { get; set; }

    /// <summary>
    /// 接收方用户名
    /// </summary>
    /// <remarks>用于前端显示，避免前端再次查询</remarks>
    public string? ToUserName { get; set; }

    /// <summary>
    /// 交易金额（胡萝卜）
    /// </summary>
    public long Amount { get; set; }

    /// <summary>
    /// 交易金额（白萝卜，格式化显示）
    /// </summary>
    public string AmountDisplay { get; set; } = "0.000";

    /// <summary>
    /// 手续费（胡萝卜）
    /// </summary>
    public long Fee { get; set; }

    /// <summary>
    /// 手续费（白萝卜，格式化显示）
    /// </summary>
    public string FeeDisplay { get; set; } = "0.000";

    /// <summary>
    /// 理论金额（精确计算结果）
    /// </summary>
    /// <remarks>用于审计和对账，仅在有比例计算时才有值</remarks>
    public decimal? TheoreticalAmount { get; set; }

    /// <summary>
    /// 舍入差额（理论金额 - 实际金额）
    /// </summary>
    /// <remarks>用于财务对账，记录舍入产生的差额</remarks>
    public decimal? RoundingDiff { get; set; }

    /// <summary>
    /// 交易类型
    /// </summary>
    /// <remarks>
    /// SYSTEM_GRANT: 系统赠送
    /// LIKE_REWARD: 点赞奖励
    /// COMMENT_REWARD: 评论奖励
    /// TRANSFER: 用户转账
    /// TIP: 打赏
    /// CONSUME: 消费
    /// REFUND: 退款
    /// PENALTY: 惩罚扣除
    /// ADMIN_ADJUST: 管理员调整
    /// </remarks>
    public string TransactionType { get; set; } = string.Empty;

    /// <summary>
    /// 交易类型显示名称
    /// </summary>
    /// <remarks>用于前端显示中文名称</remarks>
    public string TransactionTypeDisplay { get; set; } = string.Empty;

    /// <summary>
    /// 交易状态
    /// </summary>
    /// <remarks>PENDING: 待处理, SUCCESS: 成功, FAILED: 失败</remarks>
    public string Status { get; set; } = "PENDING";

    /// <summary>
    /// 交易状态显示名称
    /// </summary>
    public string StatusDisplay { get; set; } = string.Empty;

    /// <summary>
    /// 业务类型
    /// </summary>
    public string? BusinessType { get; set; }

    /// <summary>
    /// 业务 ID
    /// </summary>
    public long? BusinessId { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreateTime { get; set; }
}
