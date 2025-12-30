using AutoMapper;
using Radish.Common.CacheTool;
using Radish.IRepository;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Serilog;
using SqlSugar;

namespace Radish.Service;

/// <summary>评论服务实现</summary>
public class CommentService : BaseService<Comment, CommentVo>, ICommentService
{
    private readonly IBaseRepository<Comment> _commentRepository;
    private readonly IBaseRepository<UserCommentLike> _userCommentLikeRepository;
    private readonly IBaseRepository<CommentHighlight> _highlightRepository;
    private readonly IPostService _postService;
    private readonly ICaching _caching;

    public CommentService(
        IMapper mapper,
        IBaseRepository<Comment> baseRepository,
        IBaseRepository<UserCommentLike> userCommentLikeRepository,
        IBaseRepository<CommentHighlight> highlightRepository,
        IPostService postService,
        ICaching caching)
        : base(mapper, baseRepository)
    {
        _commentRepository = baseRepository;
        _userCommentLikeRepository = userCommentLikeRepository;
        _highlightRepository = highlightRepository;
        _postService = postService;
        _caching = caching;
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

        // 🚀 事件驱动优化：异步触发神评/沙发检查（不阻塞用户操作）
        _ = Task.Run(async () =>
        {
            try
            {
                if (comment.ParentId == null)
                {
                    // 父评论：检查神评
                    await CheckAndUpdateGodCommentAsync(comment.PostId);
                }
                else
                {
                    // 子评论：检查沙发
                    await CheckAndUpdateSofaAsync(comment.ParentId.Value, comment.PostId);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[CommentService] 点赞后神评/沙发检查失败：CommentId={CommentId}", commentId);
            }
        });

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

        // 6. 填充神评/沙发标识
        if (rootComments.Any())
        {
            await FillHighlightStatusAsync(postId, rootComments);
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

    /// <summary>
    /// 检查并更新帖子的神评
    /// </summary>
    /// <remarks>
    /// 当父评论被点赞/取消点赞时调用，实时更新神评状态
    /// </remarks>
    private async Task CheckAndUpdateGodCommentAsync(long postId)
    {
        try
        {
            // 只查询这一个帖子的父评论（利用索引，超快）
            var topComments = await _commentRepository.DbBase.Queryable<Comment>()
                .Where(c => c.PostId == postId && c.ParentId == null && !c.IsDeleted && c.IsEnabled)
                .OrderBy(c => c.LikeCount, OrderByType.Desc)
                .OrderBy(c => c.CreateTime, OrderByType.Desc)
                .Take(5) // 取前5名用于排名
                .ToListAsync();

            if (!topComments.Any())
            {
                return;
            }

            // 检查是否需要更新
            var existingHighlight = await _highlightRepository.QueryFirstAsync(
                h => h.PostId == postId && h.HighlightType == 1 && h.IsCurrent);

            var currentTopComment = topComments.First();

            // 如果神评没变化，且点赞数也没变，跳过更新
            bool shouldUpdate = existingHighlight == null ||
                               existingHighlight.CommentId != currentTopComment.Id ||
                               existingHighlight.LikeCount != currentTopComment.LikeCount;

            if (!shouldUpdate)
            {
                return;
            }

            // 标记旧记录为非当前
            if (existingHighlight != null)
            {
                await _highlightRepository.UpdateColumnsAsync(
                    h => new CommentHighlight { IsCurrent = false },
                    h => h.PostId == postId && h.HighlightType == 1 && h.IsCurrent);
            }

            // 添加新的神评记录（支持并列第一）
            var newHighlights = new List<CommentHighlight>();
            var rank = 1;
            var topLikeCount = topComments.First().LikeCount;

            foreach (var comment in topComments)
            {
                if (comment.LikeCount < topLikeCount)
                {
                    break; // 只记录点赞数最高的（可能有多个并列）
                }

                newHighlights.Add(new CommentHighlight
                {
                    PostId = postId,
                    CommentId = comment.Id,
                    ParentCommentId = null,
                    HighlightType = 1, // 神评
                    StatDate = DateTime.Today,
                    LikeCount = comment.LikeCount,
                    Rank = rank,
                    ContentSnapshot = comment.Content,
                    AuthorId = comment.AuthorId,
                    AuthorName = comment.AuthorName,
                    IsCurrent = true,
                    TenantId = comment.TenantId,
                    CreateTime = DateTime.Now,
                    CreateBy = "CommentService.RealTime"
                });

                rank++;
            }

            if (newHighlights.Any())
            {
                await _highlightRepository.AddRangeAsync(newHighlights);

                // 🚀 清除缓存（触发下次查询时重新加载）
                var cacheKey = $"god_comments:post:{postId}";
                await _caching.RemoveAsync(cacheKey);

                Log.Information("[CommentService] 实时更新神评：PostId={PostId}, Count={Count}", postId, newHighlights.Count);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[CommentService] 检查神评失败：PostId={PostId}", postId);
        }
    }

    /// <summary>
    /// 检查并更新父评论的沙发
    /// </summary>
    /// <remarks>
    /// 当子评论被点赞/取消点赞时调用，实时更新沙发状态
    /// </remarks>
    private async Task CheckAndUpdateSofaAsync(long parentCommentId, long postId)
    {
        try
        {
            // 只查询这一个父评论的子评论
            var topChildren = await _commentRepository.DbBase.Queryable<Comment>()
                .Where(c => c.ParentId == parentCommentId && !c.IsDeleted && c.IsEnabled)
                .OrderBy(c => c.LikeCount, OrderByType.Desc)
                .OrderBy(c => c.CreateTime, OrderByType.Desc)
                .Take(5)
                .ToListAsync();

            if (!topChildren.Any())
            {
                return;
            }

            // 检查是否需要更新
            var existingHighlight = await _highlightRepository.QueryFirstAsync(
                h => h.ParentCommentId == parentCommentId && h.HighlightType == 2 && h.IsCurrent);

            var currentTopChild = topChildren.First();

            bool shouldUpdate = existingHighlight == null ||
                               existingHighlight.CommentId != currentTopChild.Id ||
                               existingHighlight.LikeCount != currentTopChild.LikeCount;

            if (!shouldUpdate)
            {
                return;
            }

            // 标记旧记录为非当前
            if (existingHighlight != null)
            {
                await _highlightRepository.UpdateColumnsAsync(
                    h => new CommentHighlight { IsCurrent = false },
                    h => h.ParentCommentId == parentCommentId && h.HighlightType == 2 && h.IsCurrent);
            }

            // 添加新的沙发记录
            var newHighlights = new List<CommentHighlight>();
            var rank = 1;
            var topLikeCount = topChildren.First().LikeCount;

            foreach (var child in topChildren)
            {
                if (child.LikeCount < topLikeCount)
                {
                    break;
                }

                newHighlights.Add(new CommentHighlight
                {
                    PostId = postId,
                    CommentId = child.Id,
                    ParentCommentId = parentCommentId,
                    HighlightType = 2, // 沙发
                    StatDate = DateTime.Today,
                    LikeCount = child.LikeCount,
                    Rank = rank,
                    ContentSnapshot = child.Content,
                    AuthorId = child.AuthorId,
                    AuthorName = child.AuthorName,
                    IsCurrent = true,
                    TenantId = child.TenantId,
                    CreateTime = DateTime.Now,
                    CreateBy = "CommentService.RealTime"
                });

                rank++;
            }

            if (newHighlights.Any())
            {
                await _highlightRepository.AddRangeAsync(newHighlights);

                // 🚀 清除缓存（触发下次查询时重新加载）
                var cacheKey = $"sofas:parent:{parentCommentId}";
                await _caching.RemoveAsync(cacheKey);

                Log.Information("[CommentService] 实时更新沙发：ParentId={ParentId}, Count={Count}", parentCommentId, newHighlights.Count);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[CommentService] 检查沙发失败：ParentCommentId={ParentId}", parentCommentId);
        }
    }

    /// <summary>
    /// 填充评论树的神评/沙发标识
    /// </summary>
    /// <param name="postId">帖子ID</param>
    /// <param name="rootComments">根评论列表</param>
    private async Task FillHighlightStatusAsync(long postId, List<CommentVo> rootComments)
    {
        try
        {
            // 1. 查询该帖子的所有当前神评
            var godComments = await _highlightRepository.QueryAsync(
                h => h.PostId == postId && h.HighlightType == 1 && h.IsCurrent);

            var godCommentMap = godComments.ToDictionary(h => h.CommentId, h => h.Rank);

            // 2. 收集所有父评论的ID，批量查询沙发
            var parentCommentIds = rootComments.Select(c => c.Id).ToList();
            var sofas = await _highlightRepository.QueryAsync(
                h => parentCommentIds.Contains(h.ParentCommentId!.Value) && h.HighlightType == 2 && h.IsCurrent);

            var sofaMap = sofas.ToDictionary(h => h.CommentId, h => h.Rank);

            // 3. 递归填充标识
            FillHighlightStatusRecursive(rootComments, godCommentMap, sofaMap);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[CommentService] 填充神评/沙发标识失败：PostId={PostId}", postId);
        }
    }

    /// <summary>
    /// 递归填充神评/沙发标识
    /// </summary>
    private void FillHighlightStatusRecursive(
        List<CommentVo> comments,
        Dictionary<long, int> godCommentMap,
        Dictionary<long, int> sofaMap)
    {
        foreach (var comment in comments)
        {
            // 填充神评标识（仅父评论）
            if (comment.ParentId == null && godCommentMap.TryGetValue(comment.Id, out var godRank))
            {
                comment.IsGodComment = true;
                comment.HighlightRank = godRank;
            }

            // 填充沙发标识（仅子评论）
            if (comment.ParentId != null && sofaMap.TryGetValue(comment.Id, out var sofaRank))
            {
                comment.IsSofa = true;
                comment.HighlightRank = sofaRank;
            }

            // 递归处理子评论
            if (comment.Children?.Any() == true)
            {
                FillHighlightStatusRecursive(comment.Children, godCommentMap, sofaMap);
            }
        }
    }
}
