using Radish.Model;
using Radish.Model.ViewModels;

namespace Radish.IService;

/// <summary>评论服务接口</summary>
public interface ICommentService : IBaseService<Comment, CommentVo>
{
    /// <summary>
    /// 获取帖子的评论树
    /// </summary>
    /// <param name="postId">帖子 Id</param>
    /// <returns>评论树（顶级评论及其子评论）</returns>
    Task<List<CommentVo>> GetCommentTreeAsync(long postId);

    /// <summary>
    /// 添加评论
    /// </summary>
    /// <param name="comment">评论实体</param>
    /// <returns>评论 Id</returns>
    Task<long> AddCommentAsync(Comment comment);

    /// <summary>
    /// 更新评论点赞次数
    /// </summary>
    /// <param name="commentId">评论 Id</param>
    /// <param name="increment">增量（1 或 -1）</param>
    Task UpdateLikeCountAsync(long commentId, int increment);

    /// <summary>
    /// 更新评论回复次数
    /// </summary>
    /// <param name="commentId">评论 Id</param>
    /// <param name="increment">增量（可为负数）</param>
    Task UpdateReplyCountAsync(long commentId, int increment);
}
