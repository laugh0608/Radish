namespace Radish.Model.ViewModels;

/// <summary>
/// 交易结果视图模型
/// </summary>
public class TransactionResultVo
{
    /// <summary>
    /// 交易流水号
    /// </summary>
    public string VoTransactionNo { get; set; } = string.Empty;

    /// <summary>
    /// 错误代码
    /// </summary>
    public string? VoErrorCode { get; set; }

    /// <summary>
    /// 是否需要升级支付口令
    /// </summary>
    public bool VoRequiresPasscodeUpgrade { get; set; }
}
