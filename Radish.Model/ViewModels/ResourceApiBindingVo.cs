namespace Radish.Model.ViewModels;

/// <summary>
/// 资源与接口映射预览
/// </summary>
public class ResourceApiBindingVo
{
    public long VoResourceId { get; set; }
    public string VoResourceKey { get; set; } = string.Empty;
    public long VoApiModuleId { get; set; }
    public string VoApiModuleName { get; set; } = string.Empty;
    public string VoLinkUrl { get; set; } = string.Empty;
    public string VoRelationType { get; set; } = string.Empty;
}
