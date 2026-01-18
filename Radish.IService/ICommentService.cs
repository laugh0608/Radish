using Radish.Model.DTOs;
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

    /// <summary>
    /// 切换评论点赞状态（点赞/取消点赞）
    /// </summary>
    /// <param name="userId">用户 Id</param>
    /// <param name="commentId">评论 Id</param>
    /// <returns>点赞操作结果（当前状态和最新点赞数）</returns>
    Task<CommentLikeResultDto> ToggleLikeAsync(long userId, long commentId);

    /// <summary>
    /// 批量查询用户对评论的点赞状态
    /// </summary>
    /// <param name="userId">用户 Id</param>
    /// <param name="commentIds">评论 Id 列表</param>
    /// <returns>点赞状态字典（评论Id → 是否已点赞）</returns>
    Task<Dictionary<long, bool>> GetUserLikeStatusAsync(long userId, List<long> commentIds);

    /// <summary>
    /// 获取帖子的评论树（带点赞状态）
    /// </summary>
    /// <param name="postId">帖子 Id</param>
    /// <param name="userId">用户 Id（可选，用于填充点赞状态）</param>
    /// <param name="sortBy">排序方式：newest=最新，hottest=最热（默认：newest）</param>
    /// <returns>评论树（包含点赞状态）</returns>
    Task<List<CommentVo>> GetCommentTreeWithLikeStatusAsync(long postId, long? userId = null, string sortBy = "newest");

    /// <summary>
    /// 分页获取子评论
    /// </summary>
    /// <param name="parentId">父评论 Id</param>
    /// <param name="pageIndex">页码（从1开始）</param>
    /// <param name="pageSize">每页数量</param>
    /// <param name="userId">用户 Id（可选，用于填充点赞状态）</param>
    /// <returns>子评论列表和总数</returns>
    Task<(List<CommentVo> comments, int total)> GetChildCommentsPageAsync(long parentId, int pageIndex, int pageSize, long? userId = null);

    /// <summary>
    /// 更新评论内容
    /// </summary>
    /// <param name="commentId">评论 Id</param>
    /// <param name="newContent">新的评论内容</param>
    /// <param name="userId">操作用户 Id</param>
    /// <param name="userName">操作用户名称</param>
    /// <returns>更新结果（是否成功，错误信息）</returns>
    Task<(bool success, string message)> UpdateCommentAsync(long commentId, string newContent, long userId, string userName);
}
