namespace Radish.Model.ViewModels;

/// <summary>文件名编码清洗结果</summary>
public class StickerNormalizeCodeVo
{
    public string VoOriginalFileName { get; set; } = string.Empty;
    public string VoNormalizedCode { get; set; } = string.Empty;
    public bool VoIsChanged { get; set; }
    public List<string> VoChangeReasons { get; set; } = new();
}
