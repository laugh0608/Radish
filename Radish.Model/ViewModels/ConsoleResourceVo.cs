namespace Radish.Model.ViewModels;

/// <summary>
/// Console 资源视图对象
/// </summary>
public class ConsoleResourceVo
{
    public long VoId { get; set; }
    public long VoParentId { get; set; }
    public string VoResourceKey { get; set; } = string.Empty;
    public string VoResourceName { get; set; } = string.Empty;
    public string VoResourceType { get; set; } = string.Empty;
    public string VoModuleKey { get; set; } = string.Empty;
    public string? VoRoutePath { get; set; }
    public string? VoIcon { get; set; }
    public int VoOrderSort { get; set; }
    public bool VoShowInSidebar { get; set; }
    public bool VoShowInSearch { get; set; }
    public string? VoDescription { get; set; }
    public bool VoIsEnabled { get; set; }
}
