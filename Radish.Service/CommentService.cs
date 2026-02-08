using AutoMapper;
using Microsoft.Extensions.Options;
using Radish.Common.CacheTool;
using Radish.Common.OptionTool;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Service.Base;
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
    private readonly ICoinRewardService _coinRewardService;
    private readonly INotificationService _notificationService;
    private readonly INotificationDedupService _dedupService;
    private readonly IExperienceService _experienceService;
    private readonly CommentHighlightOptions _highlightOptions;

    public CommentService(
        IMapper mapper,
        IBaseRepository<Comment> baseRepository,
        IBaseRepository<UserCommentLike> userCommentLikeRepository,
        IBaseRepository<CommentHighlight> highlightRepository,
        IPostService postService,
        ICaching caching,
        ICoinRewardService coinRewardService,
        INotificationService notificationService,
        INotificationDedupService dedupService,
        IExperienceService experienceService,
        IOptions<CommentHighlightOptions> highlightOptions)
        : base(mapper, baseRepository)
    {
        _commentRepository = baseRepository;
        _userCommentLikeRepository = userCommentLikeRepository;
        _highlightRepository = highlightRepository;
        _postService = postService;
        _caching = caching;
        _coinRewardService = coinRewardService;
        _notificationService = notificationService;
        _dedupService = dedupService;
        _experienceService = experienceService;
        _highlightOptions = highlightOptions.Value;
    }

    /// <summary>
    /// 获取帖子的评论树
    /// </summary>
    public async Task<List<CommentVo>> GetCommentTreeAsync(long postId)
    {
        // 获取所有评论
        var comments = await QueryAsync(c => c.PostId == postId && c.IsEnabled && !c.IsDeleted);

        // 构建树形结构
        var commentMap = comments.ToDictionary(c => c.VoId);
        var rootComments = new List<CommentVo>();

        foreach (var comment in comments)
        {
            if (comment.VoParentId == null)
            {
                // 顶级评论
                rootComments.Add(comment);
            }
            else if (commentMap.TryGetValue(comment.VoParentId.Value, out var parent))
            {
                // 子评论
                parent.VoChildren ??= new List<CommentVo>();
                parent.VoChildren.Add(comment);
            }
        }

        // 按时间排序
        return rootComments.OrderByDescending(c => c.VoIsTop)
                          .ThenBy(c => c.VoCreateTime)
                          .ToList();
    }

    /// <summary>
    /// 添加评论
    /// </summary>
    public async Task<long> AddCommentAsync(Comment comment)
    {
        long? parentAuthorId = null;

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

                // 记录父评论作者ID，用于奖励发放
                parentAuthorId = parentComment.AuthorId;

                // 更新父评论的回复数
                await UpdateReplyCountAsync(parentComment.Id, 1);
            }
        }

        // 2. 插入评论
        var commentId = await AddAsync(comment);

        // 3. 更新帖子的评论数
        await _postService.UpdateCommentCountAsync(comment.PostId, 1);

        // 4. 🎁 发放评论奖励（异步，不阻塞）
        _ = Task.Run(async () =>
        {
            try
            {
                // 4.1 评论发布萝卜币奖励 +1 胡萝卜
                await _coinRewardService.GrantCommentRewardAsync(commentId, comment.AuthorId, comment.PostId);
                Log.Information("评论发布萝卜币奖励发放成功：CommentId={CommentId}, AuthorId={AuthorId}", commentId, comment.AuthorId);

                // 4.2 评论发布经验值奖励 +5 经验
                Serilog.Log.Information("准备发放评论经验值：CommentId={CommentId}, AuthorId={AuthorId}", commentId, comment.AuthorId);

                var expGrantResult = await _experienceService.GrantExperienceAsync(
                    userId: comment.AuthorId,
                    amount: 5,
                    expType: "COMMENT_CREATE",
                    businessType: "Comment",
                    businessId: commentId,
                    remark: "发布评论");

                if (expGrantResult)
                {
                    Serilog.Log.Information("评论经验值发放成功：CommentId={CommentId}, AuthorId={AuthorId}, Amount=5",
                        commentId, comment.AuthorId);
                }
                else
                {
                    Serilog.Log.Warning("评论经验值发放失败：CommentId={CommentId}, AuthorId={AuthorId}",
                        commentId, comment.AuthorId);
                }

                // 4.3 检查是否首次评论，发放额外奖励
                var userCommentCount = await _commentRepository.QueryCountAsync(c =>
                    c.AuthorId == comment.AuthorId && !c.IsDeleted);

                Serilog.Log.Information("用户评论数量统计：AuthorId={AuthorId}, CommentCount={CommentCount}",
                    comment.AuthorId, userCommentCount);

                if (userCommentCount == 1) // 首次评论
                {
                    Serilog.Log.Information("检测到首次评论，准备发放额外奖励：AuthorId={AuthorId}", comment.AuthorId);

                    var firstCommentResult = await _experienceService.GrantExperienceAsync(
                        userId: comment.AuthorId,
                        amount: 10,
                        expType: "FIRST_COMMENT",
                        businessType: "Comment",
                        businessId: commentId,
                        remark: "首次评论奖励");

                    if (firstCommentResult)
                    {
                        Serilog.Log.Information("首次评论经验值奖励发放成功：CommentId={CommentId}, AuthorId={AuthorId}, Amount=10",
                            commentId, comment.AuthorId);
                    }
                    else
                    {
                        Serilog.Log.Warning("首次评论经验值奖励发放失败：CommentId={CommentId}, AuthorId={AuthorId}",
                            commentId, comment.AuthorId);
                    }
                }

                // 4.4 评论被回复奖励（如果是回复评论）
                if (comment.ParentId.HasValue && parentAuthorId.HasValue)
                {
                    await _coinRewardService.GrantCommentReplyRewardAsync(
                        comment.ParentId.Value,
                        parentAuthorId.Value,
                        commentId);
                    Log.Information("评论被回复奖励发放成功：ParentCommentId={ParentCommentId}, ParentAuthorId={ParentAuthorId}",
                        comment.ParentId.Value, parentAuthorId.Value);

                    // 4.5 发送评论回复通知（不给自己发通知）
                    if (parentAuthorId.Value != comment.AuthorId)
                    {
                        try
                        {
                            await _notificationService.CreateNotificationAsync(new CreateNotificationDto
                            {
                                Type = NotificationType.CommentReplied,
                                Title = "评论回复",
                                Content = comment.Content,
                                Priority = (int)NotificationPriority.Normal,
                                BusinessType = BusinessType.Comment,
                                BusinessId = commentId,
                                TriggerId = comment.AuthorId,
                                TriggerName = comment.AuthorName,
                                TriggerAvatar = null, // 头像字段可以后续从用户表查询
                                ReceiverUserIds = new List<long> { parentAuthorId.Value }
                            });
                            Log.Information("评论回复通知发送成功：CommentId={CommentId}, 接收者={ReceiverId}",
                                commentId, parentAuthorId.Value);
                        }
                        catch (Exception notifyEx)
                        {
                            Log.Error(notifyEx, "发送评论回复通知失败：CommentId={CommentId}, 接收者={ReceiverId}",
                                commentId, parentAuthorId.Value);
                        }
                    }
                }
                // 4.6 评论帖子通知（顶级评论，不给自己发通知）
                if (!comment.ParentId.HasValue)
                {
                    try
                    {
                        var post = await _postService.GetPostDetailAsync(comment.PostId);
                        if (post != null && post.VoAuthorId != comment.AuthorId)
                        {
                            await _notificationService.CreateNotificationAsync(new CreateNotificationDto
                            {
                                Type = NotificationType.CommentReplied,
                                Title = "帖子被评论",
                                Content = comment.Content,
                                Priority = (int)NotificationPriority.Normal,
                                BusinessType = BusinessType.Post,
                                BusinessId = comment.PostId,
                                TriggerId = comment.AuthorId,
                                TriggerName = comment.AuthorName,
                                TriggerAvatar = null,
                                ReceiverUserIds = new List<long> { post.VoAuthorId }
                            });
                            Log.Information("帖子评论通知发送成功：PostId={PostId}, CommentId={CommentId}, 接收者={ReceiverId}",
                                comment.PostId, commentId, post.VoAuthorId);
                        }
                    }
                    catch (Exception notifyEx)
                    {
                        Log.Error(notifyEx, "发送帖子评论通知失败：PostId={PostId}, CommentId={CommentId}",
                            comment.PostId, commentId);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "发放评论奖励失败：CommentId={CommentId}, AuthorId={AuthorId}, Message={Message}, StackTrace={StackTrace}",
                    commentId, comment.AuthorId, ex.Message, ex.StackTrace);
            }
        });

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

        // 2. 检查是否已点赞（排除软删除的记录）
        var existingLikes = await _userCommentLikeRepository.QueryAsync(
            x => x.UserId == userId && x.CommentId == commentId && !x.IsDeleted);

        // 同时检查是否有被软删除的点赞记录
        var deletedLikes = await _userCommentLikeRepository.QueryAsync(
            x => x.UserId == userId && x.CommentId == commentId && x.IsDeleted);

        bool isLiked;
        int likeCountDelta;

        if (existingLikes.Any())
        {
            // 取消点赞（软删除）
            await _userCommentLikeRepository.UpdateColumnsAsync(
                l => new UserCommentLike { IsDeleted = true },
                l => l.Id == existingLikes.First().Id);
            isLiked = false;
            likeCountDelta = -1;
        }
        else if (deletedLikes.Any())
        {
            // 恢复之前的点赞记录
            await _userCommentLikeRepository.UpdateColumnsAsync(
                l => new UserCommentLike {
                    IsDeleted = false,
                    LikedAt = DateTime.UtcNow // 更新点赞时间
                },
                l => l.Id == deletedLikes.First().Id);
            isLiked = true;
            likeCountDelta = 1;
        }
        else
        {
            // 添加新的点赞记录
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

        // 4. 🎁 发放点赞奖励（仅在点赞时，不在取消点赞时发放）
        if (isLiked)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    // 4.1 发放萝卜币奖励
                    var rewardResult = await _coinRewardService.GrantCommentLikeRewardAsync(
                        commentId,
                        comment.AuthorId,
                        userId);

                    if (rewardResult.IsSuccess)
                    {
                        Log.Information("评论点赞萝卜币奖励发放成功：CommentId={CommentId}, 作者={AuthorId} (+{AuthorReward}), 点赞者={LikerId}",
                            commentId, comment.AuthorId, userId);
                    }

                    // 4.2 发放经验值奖励
                    Serilog.Log.Information("准备发放评论点赞经验值：CommentId={CommentId}, 作者={AuthorId}, 点赞者={LikerId}",
                        commentId, comment.AuthorId, userId);

                    // 4.2.1 被点赞者获得 +2 经验
                    var receiverExpResult = await _experienceService.GrantExperienceAsync(
                        userId: comment.AuthorId,
                        amount: 2,
                        expType: "RECEIVE_LIKE",
                        businessType: "Comment",
                        businessId: commentId,
                        remark: "评论被点赞");

                    if (receiverExpResult)
                    {
                        Serilog.Log.Information("评论被点赞经验值发放成功：CommentId={CommentId}, 作者={AuthorId}, Amount=2",
                            commentId, comment.AuthorId);
                    }
                    else
                    {
                        Serilog.Log.Warning("评论被点赞经验值发放失败：CommentId={CommentId}, 作者={AuthorId}",
                            commentId, comment.AuthorId);
                    }

                    // 4.2.2 点赞者获得 +1 经验
                    var giverExpResult = await _experienceService.GrantExperienceAsync(
                        userId: userId,
                        amount: 1,
                        expType: "GIVE_LIKE",
                        businessType: "Comment",
                        businessId: commentId,
                        remark: "点赞评论");

                    if (giverExpResult)
                    {
                        Serilog.Log.Information("点赞评论经验值发放成功：CommentId={CommentId}, 点赞者={LikerId}, Amount=1",
                            commentId, userId);
                    }
                    else
                    {
                        Serilog.Log.Warning("点赞评论经验值发放失败：CommentId={CommentId}, 点赞者={LikerId}",
                            commentId, userId);
                    }

                    // 4.3 发送点赞通知（不给自己发通知）
                    if (comment.AuthorId != userId)
                    {
                        // 检查是否应该去重
                        var shouldDedup = await _dedupService.ShouldDedupAsync(
                            comment.AuthorId,
                            NotificationType.CommentLiked,
                            commentId);

                        if (!shouldDedup)
                        {
                            try
                            {
                                await _notificationService.CreateNotificationAsync(new CreateNotificationDto
                                {
                                    Type = NotificationType.CommentLiked,
                                    Title = "评论被点赞",
                                    Content = comment.Content,
                                    Priority = (int)NotificationPriority.Low,
                                    BusinessType = BusinessType.Comment,
                                    BusinessId = commentId,
                                    TriggerId = userId,
                                    TriggerName = null, // TODO: 从用户上下文获取用户名
                                    TriggerAvatar = null, // TODO: 从用户表查询头像
                                    ReceiverUserIds = new List<long> { comment.AuthorId }
                                });

                                // 记录去重键（5分钟内不重复通知）
                                await _dedupService.RecordDedupKeyAsync(
                                    comment.AuthorId,
                                    NotificationType.CommentLiked,
                                    commentId,
                                    windowSeconds: 300);

                                Log.Information("评论点赞通知发送成功：CommentId={CommentId}, 接收者={ReceiverId}",
                                    commentId, comment.AuthorId);
                            }
                            catch (Exception notifyEx)
                            {
                                Log.Error(notifyEx, "发送评论点赞通知失败：CommentId={CommentId}, 接收者={ReceiverId}",
                                    commentId, comment.AuthorId);
                            }
                        }
                        else
                        {
                            Log.Debug("评论点赞通知被去重：CommentId={CommentId}, 接收者={ReceiverId}",
                                commentId, comment.AuthorId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "发放评论点赞奖励失败：CommentId={CommentId}, AuthorId={AuthorId}, LikerId={LikerId}, Message={Message}",
                        commentId, comment.AuthorId, userId, ex.Message);
                }
            });
        }

        // 5. 🚀 事件驱动优化：异步触发神评/沙发检查（不阻塞用户操作）
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
            x => x.UserId == userId && commentIds.Contains(x.CommentId) && !x.IsDeleted);

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
        var commentMap = comments.ToDictionary(c => c.VoId);
        var rootComments = new List<CommentVo>();

        foreach (var comment in comments)
        {
            if (comment.VoParentId == null)
            {
                // 顶级评论
                rootComments.Add(comment);
            }
            else
            {
                // 子评论：找到根评论，挂到根评论的Children下
                var rootId = comment.VoRootId ?? comment.VoParentId!.Value;

                if (commentMap.TryGetValue(rootId, out var root))
                {
                    root.VoChildren ??= new List<CommentVo>();
                    root.VoChildren.Add(comment);

                    // 填充 ChildrenTotal
                    root.VoChildrenTotal = (root.VoChildrenTotal ?? 0) + 1;
                }
            }
        }

        // 3. 根据排序方式排序父评论和子评论
        if (sortBy == "newest")
        {
            // 最新排序：按创建时间降序
            rootComments = rootComments
                .OrderByDescending(c => c.VoIsTop)
                .ThenByDescending(c => c.VoCreateTime)
                .ToList();

            // 子评论也按时间降序
            foreach (var root in rootComments)
            {
                if (root.VoChildren?.Any() == true)
                {
                    root.VoChildren = root.VoChildren
                        .OrderByDescending(c => c.VoCreateTime)
                        .ToList();
                }
            }
        }
        else if (sortBy == "hottest")
        {
            // 最热排序：按点赞数降序
            rootComments = rootComments
                .OrderByDescending(c => c.VoIsTop)
                .ThenByDescending(c => c.VoLikeCount)
                .ThenByDescending(c => c.VoCreateTime)
                .ToList();

            // 子评论也按点赞数降序
            foreach (var root in rootComments)
            {
                if (root.VoChildren?.Any() == true)
                {
                    root.VoChildren = root.VoChildren
                        .OrderByDescending(c => c.VoLikeCount)
                        .ThenByDescending(c => c.VoCreateTime)
                        .ToList();
                }
            }
        }
        else
        {
            // 默认排序：按创建时间升序（oldest first）
            rootComments = rootComments
                .OrderByDescending(c => c.VoIsTop)
                .ThenBy(c => c.VoCreateTime)
                .ToList();

            // 子评论也按时间升序
            foreach (var root in rootComments)
            {
                if (root.VoChildren?.Any() == true)
                {
                    root.VoChildren = root.VoChildren
                        .OrderBy(c => c.VoCreateTime)
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
            ids.Add(comment.VoId);
            if (comment.VoChildren?.Any() == true)
            {
                ids.AddRange(GetAllCommentIds(comment.VoChildren));
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
            comment.VoIsLiked = likeStatus.GetValueOrDefault(comment.VoId, false);
            if (comment.VoChildren?.Any() == true)
            {
                FillLikeStatus(comment.VoChildren, likeStatus);
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
            var commentIds = commentVos.Select(c => c.VoId).ToList();
            var likeStatus = await GetUserLikeStatusAsync(userId.Value, commentIds);

            foreach (var comment in commentVos)
            {
                comment.VoIsLiked = likeStatus.GetValueOrDefault(comment.VoId, false);
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
            // 仅在父评论数量超过配置的最小值时生效（避免评论太少时过早产生"神评"）
            var parentCommentCount = await _commentRepository.QueryCountAsync(
                c => c.PostId == postId && c.ParentId == null && !c.IsDeleted && c.IsEnabled);

            if (parentCommentCount <= _highlightOptions.MinParentCommentCount)
            {
                // 评论数量不足时，确保不保留旧的"当前神评"
                var updatedRows = await _highlightRepository.UpdateColumnsAsync(
                    it => new CommentHighlight { IsCurrent = false },
                    h => h.PostId == postId && h.HighlightType == 1 && h.IsCurrent);

                if (updatedRows > 0)
                {
                    var cacheKey = $"god_comments:post:{postId}";
                    await _caching.RemoveAsync(cacheKey);
                }

                return;
            }

            // 只查询这一个帖子的父评论（利用索引，超快）
            var (topComments, _) = await _commentRepository.QueryPageAsync(
                whereExpression: c => c.PostId == postId && c.ParentId == null && !c.IsDeleted && c.IsEnabled,
                pageIndex: 1,
                pageSize: 5,
                orderByExpression: c => c.LikeCount,
                orderByType: OrderByType.Desc,
                thenByExpression: c => c.CreateTime,
                thenByType: OrderByType.Desc);

            if (!topComments.Any())
            {
                return;
            }

            // 🔍 查询所有当前神评记录(可能有多条并列)
            var existingHighlights = await _highlightRepository.QueryAsync(
                h => h.PostId == postId && h.HighlightType == 1 && h.IsCurrent);

            var currentTopComment = topComments.First();

            // 检查是否需要更新
            bool shouldUpdate = !existingHighlights.Any() ||
                               !existingHighlights.Any(h => h.CommentId == currentTopComment.Id) ||
                               existingHighlights.Any(h => h.LikeCount != currentTopComment.LikeCount);

            if (!shouldUpdate)
            {
                return;
            }

            // 🚀 先标记该帖子下所有当前神评为非当前(批量更新,避免遗漏)
            if (existingHighlights.Any())
            {
                await _highlightRepository.UpdateColumnsAsync(
                    it => new CommentHighlight { IsCurrent = false },
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

                // 🎁 发放神评基础奖励（异步，不阻塞）
                foreach (var highlight in newHighlights)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var rewardResult = await _coinRewardService.GrantGodCommentRewardAsync(
                                highlight.CommentId,
                                highlight.AuthorId,
                                highlight.LikeCount);

                            if (rewardResult.IsSuccess)
                            {
                                Log.Information("神评基础奖励发放成功：CommentId={CommentId}, AuthorId={AuthorId}, 奖励={Amount}",
                                    highlight.CommentId, highlight.AuthorId, rewardResult.Amount);
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "发放神评基础奖励失败：CommentId={CommentId}, AuthorId={AuthorId}",
                                highlight.CommentId, highlight.AuthorId);
                        }
                    });
                }
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
            Log.Information("[CommentService] 触发沙发检查：ParentId={ParentId}, PostId={PostId}", parentCommentId, postId);

            // 仅在子评论数量超过配置的最小值时生效（避免回复太少时过早产生"沙发"）
            var childCommentCount = await _commentRepository.QueryCountAsync(
                c => c.ParentId == parentCommentId && !c.IsDeleted && c.IsEnabled);

            if (childCommentCount <= _highlightOptions.MinChildCommentCount)
            {
                // 子评论数量不足时，确保不保留旧的"当前沙发"
                var updatedRows = await _highlightRepository.UpdateColumnsAsync(
                    it => new CommentHighlight { IsCurrent = false },
                    h => h.ParentCommentId == parentCommentId && h.HighlightType == 2 && h.IsCurrent);

                if (updatedRows > 0)
                {
                    var cacheKey = $"sofas:parent:{parentCommentId}";
                    await _caching.RemoveAsync(cacheKey);
                }

                return;
            }

            // 只查询这一个父评论的子评论
            var (topChildren, _) = await _commentRepository.QueryPageAsync(
                whereExpression: c => c.ParentId == parentCommentId && !c.IsDeleted && c.IsEnabled,
                pageIndex: 1,
                pageSize: 5,
                orderByExpression: c => c.LikeCount,
                orderByType: OrderByType.Desc,
                thenByExpression: c => c.CreateTime,
                thenByType: OrderByType.Desc);

            if (!topChildren.Any())
            {
                Log.Information("[CommentService] 未找到子评论：ParentId={ParentId}", parentCommentId);
                return;
            }

            Log.Information("[CommentService] 找到 {Count} 个子评论，最高点赞数={TopLikes}",
                topChildren.Count, topChildren.First().LikeCount);

            // 🔍 查询所有当前沙发记录(可能有多条并列)
            var existingHighlights = await _highlightRepository.QueryAsync(
                h => h.ParentCommentId == parentCommentId && h.HighlightType == 2 && h.IsCurrent);

            var currentTopChild = topChildren.First();

            // 检查是否需要更新
            bool shouldUpdate = !existingHighlights.Any() ||
                               !existingHighlights.Any(h => h.CommentId == currentTopChild.Id) ||
                               existingHighlights.Any(h => h.LikeCount != currentTopChild.LikeCount);

            if (!shouldUpdate)
            {
                Log.Information("[CommentService] 沙发无需更新：ParentId={ParentId}, 当前TopChild={TopChild}",
                    parentCommentId, currentTopChild.Id);
                return;
            }

            // 🚀 先标记该父评论下所有当前沙发为非当前(批量更新,避免遗漏)
            if (existingHighlights.Any())
            {
                Log.Information("[CommentService] 标记旧沙发为非当前：ParentId={ParentId}, 数量={Count}",
                    parentCommentId, existingHighlights.Count);

                await _highlightRepository.UpdateColumnsAsync(
                    it => new CommentHighlight { IsCurrent = false },
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
                Log.Information("[CommentService] 准备插入沙发记录：ParentId={ParentId}, 数量={Count}, CommentIds={CommentIds}",
                    parentCommentId, newHighlights.Count, string.Join(",", newHighlights.Select(h => h.CommentId)));

                try
                {
                    await _highlightRepository.AddRangeAsync(newHighlights);

                    // 🚀 清除缓存（触发下次查询时重新加载）
                    var cacheKey = $"sofas:parent:{parentCommentId}";
                    await _caching.RemoveAsync(cacheKey);

                    Log.Information("[CommentService] 实时更新沙发成功：ParentId={ParentId}, Count={Count}", parentCommentId, newHighlights.Count);

                    // 🎁 发放沙发基础奖励（异步，不阻塞）
                    foreach (var highlight in newHighlights)
                    {
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                var rewardResult = await _coinRewardService.GrantSofaRewardAsync(
                                    highlight.CommentId,
                                    highlight.AuthorId,
                                    highlight.LikeCount);

                                if (rewardResult.IsSuccess)
                                {
                                    Log.Information("沙发基础奖励发放成功：CommentId={CommentId}, AuthorId={AuthorId}, 奖励={Amount}",
                                        highlight.CommentId, highlight.AuthorId, rewardResult.Amount);
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex, "发放沙发基础奖励失败：CommentId={CommentId}, AuthorId={AuthorId}",
                                    highlight.CommentId, highlight.AuthorId);
                            }
                        });
                    }
                }
                catch (Exception insertEx)
                {
                    Log.Error(insertEx, "[CommentService] 插入沙发记录失败：ParentId={ParentId}, 尝试插入的记录数={Count}",
                        parentCommentId, newHighlights.Count);
                    throw;
                }
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
            var parentCommentIds = rootComments.Select(c => c.VoId).ToList();

            // 🐛 安全检查：先查询所有沙发记录
            var sofas = await _highlightRepository.QueryAsync(
                h => h.ParentCommentId != null &&
                     parentCommentIds.Contains(h.ParentCommentId.Value) &&
                     h.HighlightType == 2 &&
                     h.IsCurrent);

            Log.Information("[CommentService] 填充沙发标识：PostId={PostId}, 父评论数={RootCount}, 查询到沙发数={SofaCount}",
                postId, parentCommentIds.Count, sofas.Count);

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
            if (comment.VoParentId == null && godCommentMap.TryGetValue(comment.VoId, out var godRank))
            {
                comment.VoIsGodComment = true;
                comment.VoHighlightRank = godRank;
                Log.Debug("[CommentService] 填充神评标识：CommentId={CommentId}, Rank={Rank}", comment.VoId, godRank);
            }

            // 填充沙发标识（仅子评论）
            if (comment.VoParentId != null && sofaMap.TryGetValue(comment.VoId, out var sofaRank))
            {
                comment.VoIsSofa = true;
                comment.VoHighlightRank = sofaRank;
                Log.Debug("[CommentService] 填充沙发标识：CommentId={CommentId}, ParentId={ParentId}, Rank={Rank}",
                    comment.VoId, comment.VoParentId, sofaRank);
            }

            // 递归处理子评论
            if (comment.VoChildren?.Any() == true)
            {
                FillHighlightStatusRecursive(comment.VoChildren, godCommentMap, sofaMap);
            }
        }
    }
}
