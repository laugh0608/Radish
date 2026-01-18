namespace Radish.Model.DtoModels;

/// <summary>帖子点赞操作结果DTO</summary>
public class PostLikeResultDto
{
    /// <summary>是否已点赞</summary>
    public bool IsLiked { get; set; }

    /// <summary>当前点赞总数</summary>
    public int LikeCount { get; set; }
}
