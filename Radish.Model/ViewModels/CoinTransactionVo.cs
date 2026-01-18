namespace Radish.Model.ViewModels;

/// <summary>
/// 萝卜币交易记录视图模型
/// </summary>
public class CoinTransactionVo
{
    /// <summary>
    /// 交易 ID
    /// </summary>
    public long VoId { get; set; }

    /// <summary>
    /// 交易流水号
    /// </summary>
    public string VoTransactionNo { get; set; } = string.Empty;

    /// <summary>
    /// 发起方用户 ID
    /// </summary>
    /// <remarks>NULL 表示系统发起</remarks>
    public long? VoFromUserId { get; set; }

    /// <summary>
    /// 发起方用户名
    /// </summary>
    /// <remarks>用于前端显示，避免前端再次查询</remarks>
    public string? VoFromUserName { get; set; }

    /// <summary>
    /// 接收方用户 ID
    /// </summary>
    /// <remarks>NULL 表示系统接收</remarks>
    public long? VoToUserId { get; set; }

    /// <summary>
    /// 接收方用户名
    /// </summary>
    /// <remarks>用于前端显示，避免前端再次查询</remarks>
    public string? VoToUserName { get; set; }

    /// <summary>
    /// 交易金额（胡萝卜）
    /// </summary>
    public long VoAmount { get; set; }

    /// <summary>
    /// 交易金额（白萝卜，格式化显示）
    /// </summary>
    public string VoAmountDisplay { get; set; } = "0.000";

    /// <summary>
    /// 手续费（胡萝卜）
    /// </summary>
    public long VoFee { get; set; }

    /// <summary>
    /// 手续费（白萝卜，格式化显示）
    /// </summary>
    public string VoFeeDisplay { get; set; } = "0.000";

    /// <summary>
    /// 理论金额（精确计算结果）
    /// </summary>
    /// <remarks>用于审计和对账，仅在有比例计算时才有值</remarks>
    public decimal? VoTheoreticalAmount { get; set; }

    /// <summary>
    /// 舍入差额（理论金额 - 实际金额）
    /// </summary>
    /// <remarks>用于财务对账，记录舍入产生的差额</remarks>
    public decimal? VoRoundingDiff { get; set; }

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
    public string VoTransactionType { get; set; } = string.Empty;

    /// <summary>
    /// 交易类型显示名称
    /// </summary>
    /// <remarks>用于前端显示中文名称</remarks>
    public string VoTransactionTypeDisplay { get; set; } = string.Empty;

    /// <summary>
    /// 交易状态
    /// </summary>
    /// <remarks>PENDING: 待处理, SUCCESS: 成功, FAILED: 失败</remarks>
    public string VoStatus { get; set; } = "PENDING";

    /// <summary>
    /// 交易状态显示名称
    /// </summary>
    public string VoStatusDisplay { get; set; } = string.Empty;

    /// <summary>
    /// 业务类型
    /// </summary>
    public string? VoBusinessType { get; set; }

    /// <summary>
    /// 业务 ID
    /// </summary>
    public long? VoBusinessId { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? VoRemark { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime VoCreateTime { get; set; }
}
