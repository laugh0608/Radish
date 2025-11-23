namespace Radish.Model.ViewModels;

public class BusinessTableVo
{
    public long VoTenantId { get; set; }
    public string VoName { get; set; } = string.Empty;
    public decimal VoAmount { get; set; }
}