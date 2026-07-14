using Radish.Shared.CustomEnum;

namespace Radish.Model.ViewModels;

/// <summary>服务端权威商品能力元数据。</summary>
public sealed class ShopProductCapabilityVo
{
    public ProductType VoProductType { get; set; }

    public BenefitType? VoBenefitType { get; set; }

    public ConsumableType? VoConsumableType { get; set; }

    public bool VoCanSell { get; set; }

    public bool VoCanActivate { get; set; }

    public List<string> VoConfigurationRequirements { get; set; } = new();

    public string? VoUnavailableReason { get; set; }
}
