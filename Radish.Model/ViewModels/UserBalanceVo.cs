namespace Radish.Model.ViewModels;

/// <summary>
/// 用户余额视图模型
/// </summary>
public class UserBalanceVo
{
    /// <summary>
    /// 用户 ID
    /// </summary>
    public long VoUserId { get; set; }

    /// <summary>
    /// 可用余额（胡萝卜）
    /// </summary>
    public long VoBalance { get; set; }

    /// <summary>
    /// 可用余额（白萝卜，格式化显示）
    /// </summary>
    /// <remarks>1 白萝卜 = 1000 胡萝卜，保留 3 位小数</remarks>
    public string VoBalanceDisplay { get; set; } = "0.000";

    /// <summary>
    /// 冻结余额（胡萝卜）
    /// </summary>
    public long VoFrozenBalance { get; set; }

    /// <summary>
    /// 冻结余额（白萝卜，格式化显示）
    /// </summary>
    public string VoFrozenBalanceDisplay { get; set; } = "0.000";

    /// <summary>
    /// 累计获得（胡萝卜）
    /// </summary>
    public long VoTotalEarned { get; set; }

    /// <summary>
    /// 累计消费（胡萝卜）
    /// </summary>
    public long VoTotalSpent { get; set; }

    /// <summary>
    /// 累计转入（胡萝卜）
    /// </summary>
    public long VoTotalTransferredIn { get; set; }

    /// <summary>
    /// 累计转出（胡萝卜）
    /// </summary>
    public long VoTotalTransferredOut { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime VoCreateTime { get; set; }

    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTime? VoModifyTime { get; set; }
}
