namespace Radish.Model.ViewModels;

/// <summary>表情编码可用性校验结果</summary>
public class StickerCodeCheckVo
{
    public bool VoAvailable { get; set; }
    public string VoCode { get; set; } = string.Empty;
    public long? VoGroupId { get; set; }
}
