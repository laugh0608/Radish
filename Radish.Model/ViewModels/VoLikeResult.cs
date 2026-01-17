namespace Radish.Model.ViewModels;

/// <summary>
/// 点赞结果ViewModel
/// </summary>
public class VoLikeResult
{
    /// <summary>
    /// 是否已点赞
    /// </summary>
    public bool IsLiked { get; set; }

    /// <summary>
    /// 点赞数量
    /// </summary>
    public int LikeCount { get; set; }
}