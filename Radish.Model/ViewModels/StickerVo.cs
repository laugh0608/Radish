namespace Radish.Model.ViewModels;

/// <summary>表情视图模型</summary>
public class StickerVo
{
    public long VoId { get; set; }
    public long VoGroupId { get; set; }
    public string VoCode { get; set; } = string.Empty;
    public string VoName { get; set; } = string.Empty;
    public long? VoAttachmentId { get; set; }
    public string VoImageUrl { get; set; } = string.Empty;
    public string? VoThumbnailUrl { get; set; }
    public bool VoIsAnimated { get; set; }
    public bool VoAllowInline { get; set; }
    public int VoUseCount { get; set; }
    public int VoSort { get; set; }
    public bool VoIsEnabled { get; set; }
    public DateTime VoCreateTime { get; set; }
    public DateTime? VoModifyTime { get; set; }
}
