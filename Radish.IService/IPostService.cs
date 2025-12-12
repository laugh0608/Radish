using Radish.Model;
using Radish.Model.ViewModels;

namespace Radish.IService;

/// <summary>帖子服务接口</summary>
public interface IPostService : IBaseService<Post, PostVo>
{
    /// <summary>
    /// 获取帖子详情（包含分类名称和标签）
    /// </summary>
    /// <param name="postId">帖子 Id</param>
    /// <returns>帖子详情</returns>
    Task<PostVo?> GetPostDetailAsync(long postId);

    /// <summary>
    /// 发布帖子
    /// </summary>
    /// <param name="post">帖子实体</param>
    /// <param name="tagNames">标签名称列表</param>
    /// <returns>帖子 Id</returns>
    Task<long> PublishPostAsync(Post post, List<string>? tagNames = null);

    /// <summary>
    /// 更新帖子浏览次数
    /// </summary>
    /// <param name="postId">帖子 Id</param>
    Task IncrementViewCountAsync(long postId);

    /// <summary>
    /// 更新帖子点赞次数
    /// </summary>
    /// <param name="postId">帖子 Id</param>
    /// <param name="increment">增量（1 或 -1）</param>
    Task UpdateLikeCountAsync(long postId, int increment);

    /// <summary>
    /// 更新帖子评论次数
    /// </summary>
    /// <param name="postId">帖子 Id</param>
    /// <param name="increment">增量（可为负数）</param>
    Task UpdateCommentCountAsync(long postId, int increment);
}
