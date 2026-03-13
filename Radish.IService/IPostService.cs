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
    /// <param name="viewerUserId">查看者用户 Id（可空，用于补充用户态信息）</param>
    /// <returns>帖子详情</returns>
    Task<PostVo?> GetPostDetailAsync(long postId, long? viewerUserId = null);

    /// <summary>
    /// 批量回填帖子列表所需的轻量元数据
    /// </summary>
    /// <param name="posts">帖子列表</param>
    Task FillPostListMetadataAsync(List<PostVo> posts);

    /// <summary>
    /// 发布帖子
    /// </summary>
    /// <param name="post">帖子实体</param>
    /// <param name="poll">附带投票（可空）</param>
    /// <param name="tagNames">标签名称列表</param>
    /// <param name="allowCreateTag">是否允许自动创建新标签（通常仅管理员可用）</param>
    /// <returns>帖子 Id</returns>
    Task<long> PublishPostAsync(Post post, CreatePollDto? poll = null, List<string>? tagNames = null, bool allowCreateTag = true);

    /// <summary>
    /// 更新帖子及标签
    /// </summary>
    /// <param name="postId">帖子 Id</param>
    /// <param name="title">帖子标题</param>
    /// <param name="content">帖子内容</param>
    /// <param name="categoryId">分类 Id（可空）</param>
    /// <param name="tagNames">标签名称列表</param>
    /// <param name="allowCreateTag">是否允许自动创建新标签（通常仅管理员可用）</param>
    /// <param name="operatorId">操作者 Id</param>
    /// <param name="operatorName">操作者名称</param>
    /// <param name="isAdmin">是否管理员</param>
    Task UpdatePostAsync(
        long postId,
        string title,
        string content,
        long? categoryId,
        List<string>? tagNames,
        bool allowCreateTag,
        long operatorId,
        string operatorName,
        bool isAdmin = false);

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

    /// <summary>
    /// 分页获取帖子编辑历史
    /// </summary>
    /// <param name="postId">帖子 Id</param>
    /// <param name="pageIndex">页码（从 1 开始）</param>
    /// <param name="pageSize">每页大小</param>
    /// <returns>历史记录和总数</returns>
    Task<(List<PostEditHistoryVo> histories, int total)> GetPostEditHistoryPageAsync(long postId, int pageIndex, int pageSize);
}
