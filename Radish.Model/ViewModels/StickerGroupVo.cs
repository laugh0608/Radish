namespace Radish.Model.ViewModels;

/// <summary>表情包分组视图模型</summary>
public class StickerGroupVo
{
    public long VoId { get; set; }
    public string VoCode { get; set; } = string.Empty;
    public string VoName { get; set; } = string.Empty;
    public string? VoDescription { get; set; }
    public string? VoCoverImageUrl { get; set; }
    public StickerGroupType VoGroupType { get; set; }
    public bool VoIsEnabled { get; set; }
    public int VoSort { get; set; }
    public long VoTenantId { get; set; }
    public int VoStickerCount { get; set; }
    public DateTime VoCreateTime { get; set; }
    public DateTime? VoModifyTime { get; set; }
    public List<StickerVo> VoStickers { get; set; } = new();
}
