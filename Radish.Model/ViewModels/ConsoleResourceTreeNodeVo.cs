namespace Radish.Model.ViewModels;

/// <summary>
/// Console 资源树节点
/// </summary>
public class ConsoleResourceTreeNodeVo
{
    public long VoId { get; set; }
    public string VoTitle { get; set; } = string.Empty;
    public string VoResourceKey { get; set; } = string.Empty;
    public string VoResourceType { get; set; } = string.Empty;
    public bool VoChecked { get; set; }
    public bool VoIndeterminate { get; set; }
    public List<ResourceApiBindingVo> VoApiBindings { get; set; } = new();
    public List<ConsoleResourceTreeNodeVo> VoChildren { get; set; } = new();
}
