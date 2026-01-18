namespace Radish.Model.ViewModels;

/// <summary>
/// 商品购买检查结果视图模型
/// </summary>
public class ProductBuyCheckResultVo
{
    /// <summary>
    /// 是否可以购买
    /// </summary>
    public bool VoCanBuy { get; set; }

    /// <summary>
    /// 不能购买的原因（当CanBuy为false时）
    /// </summary>
    public string? VoReason { get; set; }
}