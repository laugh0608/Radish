namespace Radish.Model.ViewModels;

/// <summary>回应汇总视图模型</summary>
public class ReactionSummaryVo
{
    public string VoEmojiType { get; set; } = string.Empty;
    public string VoEmojiValue { get; set; } = string.Empty;
    public int VoCount { get; set; }
    public bool VoIsReacted { get; set; }
    public string? VoThumbnailUrl { get; set; }
}
