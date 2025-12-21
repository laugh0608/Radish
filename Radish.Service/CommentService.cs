using AutoMapper;
using Radish.IRepository;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using SqlSugar;

namespace Radish.Service;

/// <summary>评论服务实现</summary>
public class CommentService : BaseService<Comment, CommentVo>, ICommentService
{
    private readonly IBaseRepository<Comment> _commentRepository;
    private readonly IBaseRepository<UserCommentLike> _userCommentLikeRepository;
    private readonly IPostService _postService;

    public CommentService(
        IMapper mapper,
        IBaseRepository<Comment> baseRepository,
        IBaseRepository<UserCommentLike> userCommentLikeRepository,
        IPostService postService)
        : base(mapper, baseRepository)
    {
        _commentRepository = baseRepository;
        _userCommentLikeRepository = userCommentLikeRepository;
        _postService = postService;
    }

    /// <summary>
    /// 获取帖子的评论树
    /// </summary>
    public async Task<List<CommentVo>> GetCommentTreeAsync(long postId)
    {
        // 获取所有评论
        var comments = await QueryAsync(c => c.PostId == postId && c.IsEnabled && !c.IsDeleted);

        // 构建树形结构
        var commentMap = comments.ToDictionary(c => c.Id);
        var rootComments = new List<CommentVo>();

        foreach (var comment in comments)
        {
            if (comment.ParentId == null)
            {
                // 顶级评论
                rootComments.Add(comment);
            }
            else if (commentMap.TryGetValue(comment.ParentId.Value, out var parent))
            {
                // 子评论
                parent.Children ??= new List<CommentVo>();
                parent.Children.Add(comment);
            }
        }

        // 按时间排序
        return rootComments.OrderByDescending(c => c.IsTop)
                          .ThenBy(c => c.CreateTime)
                          .ToList();
    }

    /// <summary>
    /// 添加评论
    /// </summary>
    public async Task<long> AddCommentAsync(Comment comment)
    {
        // 1. 计算层级和路径
        if (comment.ParentId.HasValue)
        {
            var parentComment = await _commentRepository.QueryByIdAsync(comment.ParentId.Value);
            if (parentComment != null)
            {
                comment.Level = parentComment.Level + 1;
                comment.Path = string.IsNullOrEmpty(parentComment.Path)
                    ? $"{parentComment.Id}"
                    : $"{parentComment.Path}-{parentComment.Id}";
                comment.RootId = parentComment.RootId ?? parentComment.Id;

                // 更新父评论的回复数
                await UpdateReplyCountAsync(parentComment.Id, 1);
            }
        }

        // 2. 插入评论
        var commentId = await AddAsync(comment);

        // 3. 更新帖子的评论数
        await _postService.UpdateCommentCountAsync(comment.PostId, 1);

        return commentId;
    }

    /// <summary>
    /// 更新评论点赞次数
    /// </summary>
    public async Task UpdateLikeCountAsync(long commentId, int increment)
    {
        var comment = await _commentRepository.QueryByIdAsync(commentId);
        if (comment != null)
        {
            comment.LikeCount = Math.Max(0, comment.LikeCount + increment);
            await _commentRepository.UpdateAsync(comment);
        }
    }

    /// <summary>
    /// 更新评论回复次数
    /// </summary>
    public async Task UpdateReplyCountAsync(long commentId, int increment)
    {
        var comment = await _commentRepository.QueryByIdAsync(commentId);
        if (comment != null)
        {
            comment.ReplyCount = Math.Max(0, comment.ReplyCount + increment);
            await _commentRepository.UpdateAsync(comment);
        }
    }

    /// <summary>
    /// 切换评论点赞状态（点赞/取消点赞）
    /// </summary>
    public async Task<CommentLikeResultDto> ToggleLikeAsync(long userId, long commentId)
    {
        // 1. 检查评论是否存在
        var comment = await _commentRepository.QueryByIdAsync(commentId);
        if (comment == null || comment.IsDeleted)
        {
            throw new InvalidOperationException("评论不存在或已被删除");
        }

        // 2. 检查是否已点赞
        var existingLikes = await _userCommentLikeRepository.QueryAsync(
            x => x.UserId == userId && x.CommentId == commentId);

        bool isLiked;
        int likeCountDelta;

        if (existingLikes.Any())
        {
            // 取消点赞
            await _userCommentLikeRepository.DeleteByIdAsync(existingLikes.First().Id);
            isLiked = false;
            likeCountDelta = -1;
        }
        else
        {
            // 添加点赞
            var newLike = new UserCommentLike
            {
                UserId = userId,
                CommentId = commentId,
                PostId = comment.PostId,
                LikedAt = DateTime.UtcNow
            };
            await _userCommentLikeRepository.AddAsync(newLike);
            isLiked = true;
            likeCountDelta = 1;
        }

        // 3. 更新评论的点赞计数
        comment.LikeCount = Math.Max(0, comment.LikeCount + likeCountDelta);
        await _commentRepository.UpdateAsync(comment);

        return new CommentLikeResultDto
        {
            IsLiked = isLiked,
            LikeCount = comment.LikeCount
        };
    }

    /// <summary>
    /// 批量查询用户对评论的点赞状态
    /// </summary>
    public async Task<Dictionary<long, bool>> GetUserLikeStatusAsync(long userId, List<long> commentIds)
    {
        if (!commentIds.Any())
        {
            return new Dictionary<long, bool>();
        }

        var likedComments = await _userCommentLikeRepository.QueryAsync(
            x => x.UserId == userId && commentIds.Contains(x.CommentId));

        var likedSet = likedComments.Select(x => x.CommentId).ToHashSet();

        return commentIds.ToDictionary(id => id, id => likedSet.Contains(id));
    }

    /// <summary>
    /// 获取帖子的评论树（带点赞状态和排序）
    /// </summary>
    public async Task<List<CommentVo>> GetCommentTreeWithLikeStatusAsync(long postId, long? userId = null, string sortBy = "newest")
    {
        // 1. 获取所有评论
        var comments = await QueryAsync(c => c.PostId == postId && c.IsEnabled && !c.IsDeleted);

        // 2. 构建2级树形结构（父评论 + 所有子评论都挂在根评论下）
        var commentMap = comments.ToDictionary(c => c.Id);
        var rootComments = new List<CommentVo>();

        foreach (var comment in comments)
        {
            if (comment.ParentId == null)
            {
                // 顶级评论
                rootComments.Add(comment);
            }
            else
            {
                // 子评论：找到根评论，挂到根评论的Children下
                var rootId = comment.RootId ?? comment.ParentId!.Value;

                if (commentMap.TryGetValue(rootId, out var root))
                {
                    root.Children ??= new List<CommentVo>();
                    root.Children.Add(comment);

                    // 填充 ChildrenTotal
                    root.ChildrenTotal = (root.ChildrenTotal ?? 0) + 1;
                }
            }
        }

        // 3. 根据排序方式排序父评论和子评论
        if (sortBy == "newest")
        {
            // 最新排序：按创建时间降序
            rootComments = rootComments
                .OrderByDescending(c => c.IsTop)
                .ThenByDescending(c => c.CreateTime)
                .ToList();

            // 子评论也按时间降序
            foreach (var root in rootComments)
            {
                if (root.Children?.Any() == true)
                {
                    root.Children = root.Children
                        .OrderByDescending(c => c.CreateTime)
                        .ToList();
                }
            }
        }
        else if (sortBy == "hottest")
        {
            // 最热排序：按点赞数降序
            rootComments = rootComments
                .OrderByDescending(c => c.IsTop)
                .ThenByDescending(c => c.LikeCount)
                .ThenByDescending(c => c.CreateTime)
                .ToList();

            // 子评论也按点赞数降序
            foreach (var root in rootComments)
            {
                if (root.Children?.Any() == true)
                {
                    root.Children = root.Children
                        .OrderByDescending(c => c.LikeCount)
                        .ThenByDescending(c => c.CreateTime)
                        .ToList();
                }
            }
        }
        else
        {
            // 默认排序：按创建时间升序（oldest first）
            rootComments = rootComments
                .OrderByDescending(c => c.IsTop)
                .ThenBy(c => c.CreateTime)
                .ToList();

            // 子评论也按时间升序
            foreach (var root in rootComments)
            {
                if (root.Children?.Any() == true)
                {
                    root.Children = root.Children
                        .OrderBy(c => c.CreateTime)
                        .ToList();
                }
            }
        }

        // 4. 如果用户已登录，批量查询点赞状态
        if (userId.HasValue && rootComments.Any())
        {
            var allCommentIds = GetAllCommentIds(rootComments);
            var likeStatus = await GetUserLikeStatusAsync(userId.Value, allCommentIds);

            // 5. 递归填充点赞状态
            FillLikeStatus(rootComments, likeStatus);
        }

        return rootComments;
    }

    /// <summary>
    /// 递归获取评论树中的所有评论ID
    /// </summary>
    private List<long> GetAllCommentIds(List<CommentVo> comments)
    {
        var ids = new List<long>();
        foreach (var comment in comments)
        {
            ids.Add(comment.Id);
            if (comment.Children?.Any() == true)
            {
                ids.AddRange(GetAllCommentIds(comment.Children));
            }
        }
        return ids;
    }

    /// <summary>
    /// 递归填充评论树的点赞状态
    /// </summary>
    private void FillLikeStatus(List<CommentVo> comments, Dictionary<long, bool> likeStatus)
    {
        foreach (var comment in comments)
        {
            comment.IsLiked = likeStatus.GetValueOrDefault(comment.Id, false);
            if (comment.Children?.Any() == true)
            {
                FillLikeStatus(comment.Children, likeStatus);
            }
        }
    }

    /// <summary>
    /// 分页获取子评论（按点赞数降序排列）
    /// </summary>
    public async Task<(List<CommentVo> comments, int total)> GetChildCommentsPageAsync(
        long parentId,
        int pageIndex,
        int pageSize,
        long? userId = null)
    {
        // 使用Repository的二级排序方法查询子评论
        var (comments, total) = await _commentRepository.QueryPageAsync(
            whereExpression: c => c.ParentId == parentId && !c.IsDeleted,
            pageIndex: pageIndex,
            pageSize: pageSize,
            orderByExpression: c => c.LikeCount,
            orderByType: OrderByType.Desc,
            thenByExpression: c => c.CreateTime,
            thenByType: OrderByType.Desc
        );

        // 转换为ViewModel
        var commentVos = base.Mapper.Map<List<CommentVo>>(comments);

        // 如果用户已登录，填充点赞状态
        if (userId.HasValue && commentVos.Any())
        {
            var commentIds = commentVos.Select(c => c.Id).ToList();
            var likeStatus = await GetUserLikeStatusAsync(userId.Value, commentIds);

            foreach (var comment in commentVos)
            {
                comment.IsLiked = likeStatus.GetValueOrDefault(comment.Id, false);
            }
        }

        return (commentVos, total);
    }

    /// <summary>
    /// 更新评论内容
    /// </summary>
    public async Task<(bool success, string message)> UpdateCommentAsync(long commentId, string newContent, long userId, string userName)
    {
        // 1. 查询评论
        var comment = await _commentRepository.QueryByIdAsync(commentId);
        if (comment == null || comment.IsDeleted)
        {
            return (false, "评论不存在或已被删除");
        }

        // 2. 检查作者权限
        if (comment.AuthorId != userId)
        {
            return (false, "只有作者本人可以编辑评论");
        }

        // 3. 检查时间窗口（5分钟 = 300秒）
        var timeSinceCreation = DateTime.Now - comment.CreateTime;
        if (timeSinceCreation.TotalSeconds > 300)
        {
            return (false, "评论发布超过5分钟，无法编辑");
        }

        // 4. 验证内容
        if (string.IsNullOrWhiteSpace(newContent))
        {
            return (false, "评论内容不能为空");
        }

        // 5. 更新评论
        comment.Content = newContent.Trim();
        comment.ModifyTime = DateTime.Now;
        comment.ModifyBy = userName;
        comment.ModifyId = userId;
        await _commentRepository.UpdateAsync(comment);

        return (true, "编辑成功");
    }
}
