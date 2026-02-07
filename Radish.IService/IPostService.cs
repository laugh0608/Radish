using Radish.IService.Base;
using Radish.Model;
using Radish.Model.DtoModels;
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
    /// <param name="allowCreateTag">是否允许自动创建新标签（通常仅管理员可用）</param>
    /// <returns>帖子 Id</returns>
    Task<long> PublishPostAsync(Post post, List<string>? tagNames = null, bool allowCreateTag = true);

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

    /// <summary>
    /// 切换帖子点赞状态（点赞/取消点赞）
    /// </summary>
    /// <param name="userId">用户 Id</param>
    /// <param name="postId">帖子 Id</param>
    /// <returns>点赞操作结果（当前状态和最新点赞数）</returns>
    Task<PostLikeResultDto> ToggleLikeAsync(long userId, long postId);
}
