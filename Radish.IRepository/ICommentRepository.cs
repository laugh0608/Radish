using Radish.Model;

namespace Radish.IRepository;

/// <summary>评论仓储接口</summary>
public interface ICommentRepository
{
    /// <summary>
    /// 切换评论点赞关系，并返回本次真实状态变化。
    /// </summary>
    /// <param name="userId">操作用户 Id</param>
    /// <param name="commentId">评论 Id</param>
    /// <returns>评论点赞持久化结果</returns>
    Task<CommentLikePersistenceResult> ToggleCommentLikeAsync(long userId, long commentId);

    /// <summary>
    /// 按帖子批量查询最近互动用户对应的最新评论实体
    /// </summary>
    /// <param name="postAuthorMap">帖子 Id 与帖子作者 Id 的映射</param>
    /// <param name="takePerPost">每帖最多返回的互动用户数</param>
    /// <returns>最新评论实体列表</returns>
    Task<List<Comment>> QueryLatestInteractorCommentsByPostIdsAsync(
        IReadOnlyDictionary<long, long> postAuthorMap,
        int takePerPost);
}

/// <summary>评论点赞持久化结果。</summary>
public sealed record CommentLikePersistenceResult(
    long CommentId,
    long TenantId,
    long PostId,
    long? ParentId,
    long AuthorId,
    string Content,
    bool IsLiked,
    int LikeCount,
    int Delta);
