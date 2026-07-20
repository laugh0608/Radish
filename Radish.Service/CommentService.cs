using System.Linq.Expressions;
using System.Text.RegularExpressions;
using AutoMapper;
using Microsoft.Extensions.Options;
using Radish.Common.CacheTool;
using Radish.Common.AttributeTool;
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
    private static readonly Regex MarkdownImagePattern = new(@"!\[[^\]]*\]\(([^)]+)\)", RegexOptions.Compiled);
    private static readonly Regex MarkdownLinkPattern = new(@"\[([^\]]+)\]\(([^)]+)\)", RegexOptions.Compiled);
    private static readonly Regex AttachmentUriPattern = new(@"attachment://\S+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex StickerUriPattern = new(@"sticker://\S+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex MultiWhitespacePattern = new(@"\s+", RegexOptions.Compiled);

    private readonly IBaseRepository<Comment> _commentRepository;
    private readonly IBaseRepository<UserCommentLike> _userCommentLikeRepository;
    private readonly ICommentRepository? _commentCustomRepository;
    private readonly IBaseRepository<CommentHighlight> _highlightRepository;
    private readonly IPostService _postService;
    private readonly ICaching _caching;
    private readonly IAttachmentUrlResolver _attachmentUrlResolver;
    private readonly CommentHighlightOptions _highlightOptions;
    private readonly IBaseRepository<CommentEditHistory> _commentEditHistoryRepository;
    private readonly ForumEditHistoryOptions _editHistoryOptions;
    private readonly IBaseRepository<Attachment>? _attachmentRepository;
    private readonly IBaseRepository<User>? _userRepository;
    private readonly ISystemSettingProvider _systemSettingProvider;
    private readonly IReliableOutboxService? _reliableOutboxService;
    private readonly IUserAdornmentService? _userAdornmentService;

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
        IAttachmentUrlResolver attachmentUrlResolver,
        IOptions<CommentHighlightOptions> highlightOptions,
        IBaseRepository<CommentEditHistory> commentEditHistoryRepository,
        IOptions<ForumEditHistoryOptions> editHistoryOptions,
        ISystemSettingProvider systemSettingProvider,
        IBaseRepository<Attachment>? attachmentRepository = null,
        IBaseRepository<User>? userRepository = null,
        ICommentRepository? commentCustomRepository = null,
        IReliableOutboxService? reliableOutboxService = null,
        IUserAdornmentService? userAdornmentService = null)
        : base(mapper, baseRepository)
    {
        _commentRepository = baseRepository;
        _userCommentLikeRepository = userCommentLikeRepository;
        _commentCustomRepository = commentCustomRepository;
        _highlightRepository = highlightRepository;
        _postService = postService;
        _caching = caching;
        _attachmentUrlResolver = attachmentUrlResolver;
        _highlightOptions = highlightOptions.Value;
        _commentEditHistoryRepository = commentEditHistoryRepository;
        _editHistoryOptions = editHistoryOptions.Value;
        _attachmentRepository = attachmentRepository;
        _userRepository = userRepository;
        _systemSettingProvider = systemSettingProvider;
        _reliableOutboxService = reliableOutboxService;
        _userAdornmentService = userAdornmentService;
    }

    private async Task ValidateCommentContentSettingsAsync(string content)
    {
        var minContentLength = await _systemSettingProvider.GetInt32Async(SystemConfigDefaults.CommentBodyMinLengthKey);
        var maxContentLength = await _systemSettingProvider.GetInt32Async(SystemConfigDefaults.CommentBodyMaxLengthKey);
        var trimmedContent = content.Trim();

        if (minContentLength > maxContentLength)
        {
            throw new InvalidOperationException("系统设置配置错误：评论内容最小长度不能大于最大长度");
        }

        if (trimmedContent.Length < minContentLength)
        {
            throw new ArgumentException($"评论内容不能少于 {minContentLength} 个字符");
        }

        if (trimmedContent.Length > maxContentLength)
        {
            throw new ArgumentException($"评论内容不能超过 {maxContentLength} 个字符");
        }
    }

    /// <summary>
    /// 添加评论
    /// </summary>
    [UseTran]
    public async Task<(long commentId, CommentHighlightRecheckResultVo highlightRecheckResult)> AddCommentAsync(Comment comment)
    {
        await ValidateCommentContentSettingsAsync(comment.Content);

        Comment? replyTargetComment = null;

        if (!comment.ParentId.HasValue)
        {
            comment.RootId = null;
            comment.ReplyToCommentId = null;
            comment.ReplyToCommentSnapshot = string.Empty;
            comment.ReplyToUserId = null;
            comment.ReplyToUserName = string.Empty;
        }

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

                // 记录真实回复目标（不等同于两层结构中的 ParentId）
                replyTargetComment = await ResolveReplyTargetCommentAsync(comment, parentComment);

                // 更新父评论的回复数
                await UpdateReplyCountAsync(parentComment.Id, 1);
            }
        }

        // 2. 插入评论
        var commentId = await AddAsync(comment);

        var safeAuthorName = string.IsNullOrWhiteSpace(comment.AuthorName) ? "System" : comment.AuthorName;
        await BindReferencedAttachmentsAsync(comment.Content, BusinessType.Comment, commentId, comment.AuthorId, safeAuthorName, comment.TenantId);

        // 3. 更新帖子的评论数
        await _postService.UpdateCommentCountAsync(comment.PostId, 1);

        var replyNotificationId = SnowFlakeSingle.Instance.NextId();
        var postNotificationId = SnowFlakeSingle.Instance.NextId();
        var reliableOutboxService = _reliableOutboxService
            ?? throw new InvalidOperationException("可靠 Outbox 服务未注册");
        await reliableOutboxService.AddAsync(
            ReliableOutboxSources.Main,
            comment.TenantId,
            ReliableTaskTypes.CommentPublished,
            $"task:comment-published:{commentId}",
            "Comment",
            commentId.ToString(),
            new CommentPublishedTaskPayload(
                commentId,
                comment.PostId,
                comment.AuthorId,
                comment.AuthorName,
                comment.Content,
                DateTime.Now.ToString("yyyyMMdd"),
                comment.ParentId,
                replyTargetComment?.Id,
                replyTargetComment?.AuthorId,
                replyNotificationId,
                postNotificationId),
            DateTime.UtcNow);

        // 奖励与通知由 Outbox 在事务提交后可靠执行，目标写业务键负责重复执行。

        // 5. 🚀 新增评论后触发神评/沙发检查（确保后续查询能拿到最新状态）
        var highlightRecheckResult = await TriggerHighlightRecheckAsync(comment.PostId, comment.ParentId);

        return (commentId, highlightRecheckResult);
    }

    public async Task<CommentVo?> GetCommentDetailAsync(long commentId, long? userId = null)
    {
        var comment = await _commentRepository.QueryByIdAsync(commentId);
        if (comment == null || comment.IsDeleted || !comment.IsEnabled)
        {
            return null;
        }

        var commentVo = Mapper.Map<CommentVo>(comment);
        commentVo.VoChildren = [];
        commentVo.VoChildrenTotal = comment.ParentId.HasValue ? 0 : comment.ReplyCount;

        if (userId.HasValue)
        {
            var likeStatus = await GetUserLikeStatusAsync(userId.Value, [commentId]);
            commentVo.VoIsLiked = likeStatus.GetValueOrDefault(commentId, false);
        }

        await FillSingleHighlightStatusAsync(commentVo);
        await FillAuthorProfilesAsync([commentVo]);
        return commentVo;
    }

    private async Task<Comment> ResolveReplyTargetCommentAsync(Comment comment, Comment parentComment)
    {
        var replyTargetComment = parentComment;
        if (!comment.ReplyToCommentId.HasValue)
        {
            comment.ReplyToCommentId = parentComment.Id;
            comment.ReplyToCommentSnapshot = BuildReplySnapshot(parentComment.Content);
            comment.ReplyToUserId = parentComment.AuthorId;
            comment.ReplyToUserName = parentComment.AuthorName;
            return replyTargetComment;
        }

        if (comment.ReplyToCommentId.Value != parentComment.Id)
        {
            var requestedTarget = await _commentRepository.QueryByIdAsync(comment.ReplyToCommentId.Value);
            if (requestedTarget != null &&
                requestedTarget.PostId == comment.PostId &&
                requestedTarget.IsEnabled &&
                !requestedTarget.IsDeleted &&
                (requestedTarget.Id == parentComment.Id ||
                 requestedTarget.ParentId == parentComment.Id ||
                 requestedTarget.RootId == parentComment.Id))
            {
                replyTargetComment = requestedTarget;
            }
        }

        comment.ReplyToCommentId = replyTargetComment.Id;
        comment.ReplyToCommentSnapshot = BuildReplySnapshot(replyTargetComment.Content);
        comment.ReplyToUserId = replyTargetComment.AuthorId;
        comment.ReplyToUserName = replyTargetComment.AuthorName;
        return replyTargetComment;
    }

    private static string BuildReplySnapshot(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var normalized = MarkdownImagePattern.Replace(content, match =>
        {
            var source = match.Groups.Count > 1 ? match.Groups[1].Value : string.Empty;
            if (source.StartsWith("sticker://", StringComparison.OrdinalIgnoreCase))
            {
                return "[表情]";
            }

            return "[图片]";
        });

        normalized = MarkdownLinkPattern.Replace(normalized, match =>
        {
            var label = match.Groups.Count > 1 ? match.Groups[1].Value.Trim() : string.Empty;
            return string.IsNullOrWhiteSpace(label) ? "[附件]" : label;
        });

        normalized = AttachmentUriPattern.Replace(normalized, "[附件]");
        normalized = StickerUriPattern.Replace(normalized, "[表情]");
        normalized = MultiWhitespacePattern.Replace(normalized, " ").Trim();

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        const int maxLength = 30;
        return normalized.Length <= maxLength
            ? normalized
            : $"{normalized[..(maxLength - 3)]}...";
    }

    /// <summary>
    /// 触发神评/沙发实时重算
    /// </summary>
    [UseTran]
    public async Task<CommentHighlightRecheckResultVo> TriggerHighlightRecheckAsync(long postId, long? parentCommentId = null)
    {
        var highlightType = parentCommentId.HasValue ? 2 : 1;
        if (!_highlightOptions.RealtimeUpdate)
        {
            return CommentHighlightRecheckResultVo.NoChange(postId, parentCommentId, highlightType);
        }

        try
        {
            if (parentCommentId == null)
            {
                return await CheckAndUpdateGodCommentAsync(postId);
            }

            return await CheckAndUpdateSofaAsync(parentCommentId.Value, postId);
        }
        catch (Exception ex)
        {
            Log.Error(ex,
                "[CommentService] 实时触发神评/沙发检查失败：PostId={PostId}, ParentCommentId={ParentCommentId}",
                postId, parentCommentId);
            return CommentHighlightRecheckResultVo.NoChange(postId, parentCommentId, highlightType);
        }
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
    public async Task<CommentLikeResultDto> ToggleLikeAsync(long userId, string userName, long commentId)
    {
        var likeResult = await (_commentCustomRepository ?? throw new InvalidOperationException("评论专属仓储未注册"))
            .ToggleCommentLikeAsync(userId, userName, commentId);

        // 触发神评/沙发检查并把结果返回给 API 层广播；重复请求没有真实变化时不重算。
        var highlightRecheckResult = likeResult.Delta != 0
            ? await TriggerHighlightRecheckAsync(likeResult.PostId, likeResult.ParentId)
            : null;

        return new CommentLikeResultDto
        {
            IsLiked = likeResult.IsLiked,
            LikeCount = likeResult.LikeCount,
            HighlightRecheckResult = highlightRecheckResult
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
    /// 分页获取根评论（带点赞状态和排序）
    /// </summary>
    public async Task<(List<CommentVo> comments, int total)> GetRootCommentsPageAsync(
        long postId,
        int pageIndex,
        int pageSize,
        long? userId = null,
        string sortBy = "default")
    {
        if (pageIndex < 1)
        {
            pageIndex = 1;
        }

        if (pageSize < 1)
        {
            pageSize = 20;
        }

        var normalizedSortBy = sortBy?.Trim().ToLowerInvariant() ?? "default";
        var rootCondition = (Expression<Func<Comment, bool>>)(comment =>
            comment.PostId == postId &&
            comment.ParentId == null &&
            comment.IsEnabled &&
            !comment.IsDeleted);

        (List<Comment> rootComments, int total) = normalizedSortBy switch
        {
            "newest" => await _commentRepository.QueryPageAsync(
                rootCondition,
                pageIndex,
                pageSize,
                comment => comment.IsTop,
                OrderByType.Desc,
                comment => comment.CreateTime,
                OrderByType.Desc),
            "hottest" => await _commentRepository.QueryPageAsync(
                rootCondition,
                pageIndex,
                pageSize,
                comment => comment.IsTop,
                OrderByType.Desc,
                comment => new
                {
                    comment.LikeCount,
                    comment.CreateTime
                },
                OrderByType.Desc),
            _ => await _commentRepository.QueryPageAsync(
                rootCondition,
                pageIndex,
                pageSize,
                comment => comment.IsTop,
                OrderByType.Desc,
                comment => comment.CreateTime,
                OrderByType.Asc)
        };

        var commentVos = base.Mapper.Map<List<CommentVo>>(rootComments);
        for (var index = 0; index < commentVos.Count; index++)
        {
            commentVos[index].VoChildren = [];
            commentVos[index].VoChildrenTotal = rootComments[index].ReplyCount;
        }

        if (userId.HasValue && commentVos.Any())
        {
            var commentIds = commentVos.Select(comment => comment.VoId).ToList();
            var likeStatus = await GetUserLikeStatusAsync(userId.Value, commentIds);

            foreach (var comment in commentVos)
            {
                comment.VoIsLiked = likeStatus.GetValueOrDefault(comment.VoId, false);
            }
        }

        if (commentVos.Any())
        {
            await FillHighlightStatusAsync(postId, commentVos);
        }

        await FillAuthorProfilesAsync(commentVos);
        return (commentVos, total);
    }

    private async Task FillAuthorProfilesAsync(List<CommentVo> comments)
    {
        if (comments.Count <= 0)
        {
            return;
        }

        var authorIds = new HashSet<long>();
        CollectAuthorIds(comments, authorIds);
        if (authorIds.Count <= 0)
        {
            return;
        }

        var userIds = authorIds.ToList();

        Dictionary<long, string?> avatarMap = new();
        if (_attachmentRepository != null)
        {
            var avatarAttachments = await _attachmentRepository.QueryAsync(attachment =>
                attachment.BusinessType == "Avatar" &&
                attachment.BusinessId.HasValue &&
                userIds.Contains(attachment.BusinessId.Value) &&
                attachment.IsEnabled &&
                !attachment.IsDeleted);

            avatarMap = avatarAttachments
                .Where(attachment => attachment.BusinessId.HasValue)
                .OrderByDescending(attachment => attachment.CreateTime)
                .GroupBy(attachment => attachment.BusinessId!.Value)
                .ToDictionary(
                    group => group.Key,
                    group => (string?)_attachmentUrlResolver.ResolveAttachmentUrl(group.First().Id));
        }

        Dictionary<long, string> displayNameMap = new();
        Dictionary<long, string> publicIdMap = new();
        IReadOnlyDictionary<long, UserAdornmentVo> adornmentMap = new Dictionary<long, UserAdornmentVo>();
        if (_userRepository != null)
        {
            var users = await _userRepository.QueryAsync(user =>
                userIds.Contains(user.Id) &&
                !user.IsDeleted);
            await EnsureCommentUserPublicIdsAsync(users);

            displayNameMap = ForumDisplayNameHelper.BuildMap(users);
            publicIdMap = users
                .Where(user => !string.IsNullOrWhiteSpace(user.PublicId))
                .GroupBy(user => user.Id)
                .ToDictionary(group => group.Key, group => group.First().PublicId!.Trim());
        }

        if (_userAdornmentService != null)
        {
            adornmentMap = await _userAdornmentService.GetUserAdornmentsAsync(userIds);
        }

        ApplyAuthorProfiles(comments, avatarMap, displayNameMap, publicIdMap, adornmentMap);
    }

    private async Task EnsureCommentUserPublicIdsAsync(List<User> users)
    {
        if (_userRepository == null)
        {
            return;
        }

        foreach (var user in users)
        {
            if (!string.IsNullOrWhiteSpace(user.PublicId))
            {
                user.PublicId = user.PublicId.Trim();
                continue;
            }

            var publicId = User.EnsurePublicId(user.PublicId);
            var affectedRows = await _userRepository.UpdateColumnsAsync(
                item => new User { PublicId = publicId },
                item => item.Id == user.Id &&
                        !item.IsDeleted &&
                        (item.PublicId == null || item.PublicId == string.Empty));

            if (affectedRows > 0)
            {
                user.PublicId = publicId;
                continue;
            }

            var refreshedUser = await _userRepository.QueryByIdAsync(user.Id);
            if (!string.IsNullOrWhiteSpace(refreshedUser?.PublicId))
            {
                user.PublicId = refreshedUser.PublicId.Trim();
            }
        }
    }

    private static void CollectAuthorIds(IEnumerable<CommentVo> comments, ISet<long> authorIds)
    {
        foreach (var comment in comments)
        {
            if (comment.VoAuthorId > 0)
            {
                authorIds.Add(comment.VoAuthorId);
            }

            if (comment.VoReplyToUserId is > 0)
            {
                authorIds.Add(comment.VoReplyToUserId.Value);
            }

            if (comment.VoChildren?.Any() == true)
            {
                CollectAuthorIds(comment.VoChildren, authorIds);
            }
        }
    }

    private static void ApplyAuthorProfiles(
        IEnumerable<CommentVo> comments,
        IReadOnlyDictionary<long, string?> avatarMap,
        IReadOnlyDictionary<long, string> displayNameMap,
        IReadOnlyDictionary<long, string> publicIdMap,
        IReadOnlyDictionary<long, UserAdornmentVo> adornmentMap)
    {
        foreach (var comment in comments)
        {
            if (publicIdMap.TryGetValue(comment.VoAuthorId, out var authorPublicId))
            {
                comment.VoAuthorPublicId = authorPublicId;
            }

            if (displayNameMap.TryGetValue(comment.VoAuthorId, out var authorDisplayName))
            {
                comment.VoAuthorName = authorDisplayName;
            }

            if (comment.VoReplyToUserId is > 0 &&
                displayNameMap.TryGetValue(comment.VoReplyToUserId.Value, out var replyToDisplayName))
            {
                comment.VoReplyToUserName = replyToDisplayName;
            }

            if (avatarMap.TryGetValue(comment.VoAuthorId, out var avatarUrl) &&
                !string.IsNullOrWhiteSpace(avatarUrl))
            {
                comment.VoAuthorAvatarUrl = avatarUrl;
            }

            comment.VoAuthorAdornment = adornmentMap.GetValueOrDefault(comment.VoAuthorId);

            if (comment.VoChildren?.Any() == true)
            {
                ApplyAuthorProfiles(comment.VoChildren, avatarMap, displayNameMap, publicIdMap, adornmentMap);
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
            whereExpression: c => c.ParentId == parentId && !c.IsDeleted && c.IsEnabled,
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

        await FillAuthorProfilesAsync(commentVos);
        return (commentVos, total);
    }

    /// <summary>
    /// 获取评论精确定位信息
    /// </summary>
    public async Task<CommentNavigationVo?> GetCommentNavigationAsync(long postId, long commentId, int rootPageSize, int childPageSize)
    {
        var safeRootPageSize = rootPageSize <= 0 ? 20 : Math.Min(rootPageSize, 100);
        var safeChildPageSize = childPageSize <= 0 ? 5 : Math.Min(childPageSize, 100);

        var targetComment = await _commentRepository.QueryByIdAsync(commentId);
        if (targetComment == null ||
            targetComment.PostId != postId ||
            targetComment.IsDeleted ||
            !targetComment.IsEnabled)
        {
            return null;
        }

        var isRootComment = !targetComment.ParentId.HasValue;
        var rootCommentId = isRootComment
            ? targetComment.Id
            : targetComment.RootId ?? targetComment.ParentId!.Value;

        var rootComment = isRootComment
            ? targetComment
            : await _commentRepository.QueryByIdAsync(rootCommentId);

        if (rootComment == null || rootComment.IsDeleted || !rootComment.IsEnabled)
        {
            return null;
        }

        var rootPrecedingCount = await _commentRepository.QueryCountAsync(comment =>
            comment.PostId == postId &&
            comment.ParentId == null &&
            comment.IsEnabled &&
            !comment.IsDeleted &&
            (
                (comment.IsTop && !rootComment.IsTop) ||
                (comment.IsTop == rootComment.IsTop && comment.CreateTime < rootComment.CreateTime)
            ));

        int? childPageIndex = null;
        if (!isRootComment && targetComment.ParentId.HasValue)
        {
            var parentCommentId = targetComment.ParentId.Value;
            var childPrecedingCount = await _commentRepository.QueryCountAsync(comment =>
                comment.ParentId == parentCommentId &&
                comment.IsEnabled &&
                !comment.IsDeleted &&
                (
                    comment.LikeCount > targetComment.LikeCount ||
                    (comment.LikeCount == targetComment.LikeCount && comment.CreateTime > targetComment.CreateTime)
                ));

            childPageIndex = (childPrecedingCount / safeChildPageSize) + 1;
        }

        return new CommentNavigationVo
        {
            VoCommentId = targetComment.Id,
            VoPostId = targetComment.PostId,
            VoRootCommentId = rootCommentId,
            VoParentCommentId = targetComment.ParentId,
            VoIsRootComment = isRootComment,
            VoRootPageIndex = (rootPrecedingCount / safeRootPageSize) + 1,
            VoChildPageIndex = childPageIndex
        };
    }

    /// <summary>
    /// 更新评论内容
    /// </summary>
    public async Task<(bool success, string message)> UpdateCommentAsync(long commentId, string newContent, long userId, string userName, bool isAdmin = false)
    {
        // 1. 查询评论
        var comment = await _commentRepository.QueryByIdAsync(commentId);
        if (comment == null || comment.IsDeleted)
        {
            return (false, "评论不存在或已被删除");
        }

        // 2. 检查作者权限
        if (comment.AuthorId != userId && !isAdmin)
        {
            return (false, "只有作者本人可以编辑评论");
        }

        if (string.IsNullOrWhiteSpace(newContent))
        {
            return (false, "评论内容不能为空");
        }

        var trimmedContent = newContent.Trim();
        if (string.Equals(comment.Content?.Trim(), trimmedContent, StringComparison.Ordinal))
        {
            return (true, "内容没有变化，无需保存");
        }

        var commentOptions = _editHistoryOptions.Comment;
        var historyEnabled = _editHistoryOptions.Enable && commentOptions.EnableHistory;
        var historyEditCount = await _commentEditHistoryRepository.QueryCountAsync(h => h.CommentId == commentId);
        var existingEditCount = Math.Max(comment.EditCount, historyEditCount);

        // 3. 检查时间窗口
        var editWindowMinutes = Math.Max(0, commentOptions.EditWindowMinutes);
        var timeSinceCreation = DateTime.Now - comment.CreateTime;
        if ((!isAdmin || !_editHistoryOptions.AdminOverride.BypassCommentEditWindow)
            && timeSinceCreation.TotalMinutes > editWindowMinutes)
        {
            return (false, $"评论发布超过{editWindowMinutes}分钟，无法编辑");
        }

        // 3.1 检查编辑次数上限
        if ((!isAdmin || !_editHistoryOptions.AdminOverride.BypassEditCountLimit)
            && existingEditCount >= Math.Max(0, commentOptions.MaxEditCount))
        {
            return (false, "评论编辑次数已达上限，无法继续编辑");
        }

        // 4. 验证内容
        try
        {
            await ValidateCommentContentSettingsAsync(trimmedContent);
        }
        catch (ArgumentException ex)
        {
            return (false, ex.Message);
        }

        var safeUserName = string.IsNullOrWhiteSpace(userName) ? "System" : userName;
        var nextEditSequence = existingEditCount + 1;

        if (historyEnabled)
        {
            if (nextEditSequence <= Math.Max(0, commentOptions.HistorySaveEditCount))
            {
                await _commentEditHistoryRepository.AddAsync(new CommentEditHistory
                {
                    CommentId = comment.Id,
                    PostId = comment.PostId,
                    EditSequence = nextEditSequence,
                    OldContent = comment.Content ?? string.Empty,
                    NewContent = trimmedContent,
                    EditorId = userId,
                    EditorName = safeUserName,
                    EditedAt = DateTime.Now,
                    TenantId = comment.TenantId,
                    CreateTime = DateTime.Now,
                    CreateBy = safeUserName,
                    CreateId = userId
                });
            }
        }

        // 5. 更新评论
        comment.Content = trimmedContent;
        comment.EditCount = nextEditSequence;
        comment.ModifyTime = DateTime.Now;
        comment.ModifyBy = safeUserName;
        comment.ModifyId = userId;
        await _commentRepository.UpdateAsync(comment);
        await BindReferencedAttachmentsAsync(trimmedContent, BusinessType.Comment, commentId, userId, safeUserName, comment.TenantId);

        if (historyEnabled)
        {
            await TrimCommentHistoryAsync(commentId, Math.Max(1, commentOptions.MaxHistoryRecords));
        }

        return (true, "编辑成功");
    }

    public async Task<(List<CommentEditHistoryVo> histories, int total)> GetCommentEditHistoryPageAsync(long commentId, int pageIndex, int pageSize)
    {
        var safePageIndex = pageIndex < 1 ? 1 : pageIndex;
        var safePageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 100);

        var (histories, total) = await _commentEditHistoryRepository.QueryPageAsync(
            h => h.CommentId == commentId,
            safePageIndex,
            safePageSize,
            h => h.EditSequence,
            SqlSugar.OrderByType.Desc,
            h => h.CreateTime,
            SqlSugar.OrderByType.Desc);

        return (Mapper.Map<List<CommentEditHistoryVo>>(histories), total);
    }

    private async Task TrimCommentHistoryAsync(long commentId, int maxHistoryRecords)
    {
        var histories = await _commentEditHistoryRepository.QueryWithOrderAsync(
            h => h.CommentId == commentId,
            h => h.EditSequence,
            SqlSugar.OrderByType.Desc);

        if (histories.Count <= maxHistoryRecords)
        {
            return;
        }

        var removeIds = histories
            .Skip(maxHistoryRecords)
            .Select(h => h.Id)
            .ToList();

#pragma warning disable CS0618
        await _commentEditHistoryRepository.DeleteByIdsAsync(removeIds);
#pragma warning restore CS0618
    }

    private async Task BindReferencedAttachmentsAsync(
        string? content,
        string businessType,
        long businessId,
        long operatorId,
        string operatorName,
        long tenantId)
    {
        if (_attachmentRepository == null || businessId <= 0 || operatorId <= 0)
        {
            return;
        }

        var referencedAttachmentIds = AttachmentReferenceHelper.ExtractAttachmentIds(content);
        if (referencedAttachmentIds.Count == 0)
        {
            return;
        }

        var normalizedTenantId = tenantId > 0 ? tenantId : 0;
        var attachments = await _attachmentRepository.QueryAsync(a =>
            !a.IsDeleted &&
            !a.BusinessId.HasValue &&
            a.TenantId == normalizedTenantId &&
            a.UploaderId == operatorId);

        foreach (var attachment in attachments.Where(a => AttachmentReferenceHelper.IsAttachmentReferenced(a, referencedAttachmentIds)))
        {
            attachment.BusinessType = businessType;
            attachment.BusinessId = businessId;
            attachment.ModifyTime = DateTime.Now;
            attachment.ModifyBy = operatorName;
            attachment.ModifyId = operatorId;
            await _attachmentRepository.UpdateAsync(attachment);
        }
    }

    /// <summary>
    /// 检查并更新帖子的神评
    /// </summary>
    /// <remarks>
    /// 当父评论被点赞/取消点赞时调用，实时更新神评状态
    /// </remarks>
    private async Task<CommentHighlightRecheckResultVo> CheckAndUpdateGodCommentAsync(long postId)
    {
        var result = CommentHighlightRecheckResultVo.NoChange(postId, null, 1);
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
                    result.VoChanged = true;
                }

                return result;
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
                return result;
            }

            // 🔍 查询所有当前神评记录(可能有多条并列)
            var existingHighlights = await _highlightRepository.QueryAsync(
                h => h.PostId == postId && h.HighlightType == 1 && h.IsCurrent);

            var topLikeCount = topComments.First().LikeCount;
            if (topLikeCount <= 0)
            {
                result.VoChanged = await ClearCurrentHighlightsAsync(existingHighlights, $"god_comments:post:{postId}");
                return result;
            }

            var desiredComments = topComments
                .Where(comment => comment.LikeCount == topLikeCount)
                .ToList();

            if (await ShouldKeepExistingHighlightsAsync(existingHighlights, desiredComments))
            {
                result.VoCurrentCommentIds = existingHighlights.Select(highlight => highlight.CommentId).ToList();
                return result;
            }

            result = await SynchronizeCurrentHighlightsAsync(
                postId,
                null,
                highlightType: 1,
                desiredComments,
                existingHighlights,
                cacheKey: $"god_comments:post:{postId}",
                createBy: "CommentService.RealTime");

            if (result.VoChanged)
            {
                Log.Information("[CommentService] 实时更新神评：PostId={PostId}, Count={Count}", postId, result.VoCurrentCommentIds.Count);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[CommentService] 检查神评失败：PostId={PostId}", postId);
        }

        return result;
    }

    /// <summary>
    /// 检查并更新父评论的沙发
    /// </summary>
    /// <remarks>
    /// 当子评论被点赞/取消点赞时调用，实时更新沙发状态
    /// </remarks>
    private async Task<CommentHighlightRecheckResultVo> CheckAndUpdateSofaAsync(long parentCommentId, long postId)
    {
        var result = CommentHighlightRecheckResultVo.NoChange(postId, parentCommentId, 2);
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
                    result.VoChanged = true;
                }

                return result;
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
                return result;
            }

            Log.Information("[CommentService] 找到 {Count} 个子评论，最高点赞数={TopLikes}",
                topChildren.Count, topChildren.First().LikeCount);

            // 🔍 查询所有当前沙发记录(可能有多条并列)
            var existingHighlights = await _highlightRepository.QueryAsync(
                h => h.ParentCommentId == parentCommentId && h.HighlightType == 2 && h.IsCurrent);

            var topLikeCount = topChildren.First().LikeCount;
            if (topLikeCount <= 0)
            {
                result.VoChanged = await ClearCurrentHighlightsAsync(existingHighlights, $"sofas:parent:{parentCommentId}");
                return result;
            }

            var desiredChildren = topChildren
                .Where(child => child.LikeCount == topLikeCount)
                .ToList();

            if (await ShouldKeepExistingHighlightsAsync(existingHighlights, desiredChildren))
            {
                result.VoCurrentCommentIds = existingHighlights.Select(highlight => highlight.CommentId).ToList();
                return result;
            }

            result = await SynchronizeCurrentHighlightsAsync(
                postId,
                parentCommentId,
                highlightType: 2,
                desiredChildren,
                existingHighlights,
                cacheKey: $"sofas:parent:{parentCommentId}",
                createBy: "CommentService.RealTime");

            if (result.VoChanged)
            {
                Log.Information("[CommentService] 实时更新沙发成功：ParentId={ParentId}, Count={Count}",
                    parentCommentId, result.VoCurrentCommentIds.Count);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[CommentService] 检查沙发失败：ParentCommentId={ParentId}", parentCommentId);
        }

        return result;
    }

    private async Task<bool> ClearCurrentHighlightsAsync(List<CommentHighlight> existingHighlights, string cacheKey)
    {
        if (!existingHighlights.Any())
        {
            return false;
        }

        var currentHighlightIds = existingHighlights.Select(highlight => highlight.Id).ToList();
        await _highlightRepository.UpdateColumnsAsync(
            it => new CommentHighlight { IsCurrent = false },
            highlight => currentHighlightIds.Contains(highlight.Id));
        await _caching.RemoveAsync(cacheKey);
        return true;
    }

    private async Task<bool> ShouldKeepExistingHighlightsAsync(List<CommentHighlight> existingHighlights, List<Comment> desiredComments)
    {
        if (!existingHighlights.Any() || !desiredComments.Any())
        {
            return false;
        }

        var existingIds = existingHighlights.Select(highlight => highlight.CommentId).ToHashSet();
        var desiredIds = desiredComments.Select(comment => comment.Id).ToHashSet();

        if (desiredIds.Overlaps(existingIds))
        {
            return false;
        }

        var stabilityWindowMinutes = await _systemSettingProvider.GetInt32Async(
            SystemConfigDefaults.CommentHighlightStabilityWindowMinutesKey);
        if (stabilityWindowMinutes <= 0)
        {
            return false;
        }

        var latestCurrentTime = existingHighlights.Max(highlight => highlight.CreateTime);
        if (DateTime.Now - latestCurrentTime >= TimeSpan.FromMinutes(stabilityWindowMinutes))
        {
            return false;
        }

        var existingBestLikeCount = existingHighlights.Max(highlight => highlight.LikeCount);
        var desiredBestLikeCount = desiredComments.Max(comment => comment.LikeCount);
        var requiredDelta = await _systemSettingProvider.GetInt32Async(
            SystemConfigDefaults.CommentHighlightReplacementMinLikeDeltaKey);

        return desiredBestLikeCount - existingBestLikeCount < requiredDelta;
    }

    private async Task<CommentHighlightRecheckResultVo> SynchronizeCurrentHighlightsAsync(
        long postId,
        long? parentCommentId,
        int highlightType,
        List<Comment> desiredComments,
        List<CommentHighlight> existingHighlights,
        string cacheKey,
        string createBy)
    {
        var result = CommentHighlightRecheckResultVo.NoChange(postId, parentCommentId, highlightType);
        result.VoCurrentCommentIds = desiredComments.Select(comment => comment.Id).ToList();

        if (!desiredComments.Any())
        {
            return result;
        }

        var changed = false;
        var desiredIds = desiredComments.Select(comment => comment.Id).ToHashSet();
        var existingByCommentId = existingHighlights
            .GroupBy(highlight => highlight.CommentId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(highlight => highlight.CreateTime).First());

        var staleIds = existingHighlights
            .Where(highlight => !desiredIds.Contains(highlight.CommentId))
            .Select(highlight => highlight.Id)
            .ToList();

        if (staleIds.Any())
        {
            await _highlightRepository.UpdateColumnsAsync(
                it => new CommentHighlight { IsCurrent = false },
                h => staleIds.Contains(h.Id));
            changed = true;
        }

        var newHighlights = new List<CommentHighlight>();
        var rank = 1;

        foreach (var comment in desiredComments)
        {
            if (existingByCommentId.TryGetValue(comment.Id, out var existing))
            {
                var likeIncrement = comment.LikeCount - existing.LikeCount;
                if (likeIncrement > 0)
                {
                    await QueueHighlightLikeBonusAsync(existing.Id, existing.AuthorId, likeIncrement, highlightType, comment.LikeCount, comment.TenantId);
                }

                if (existing.LikeCount != comment.LikeCount ||
                    existing.Rank != rank ||
                    existing.ContentSnapshot != comment.Content ||
                    existing.AuthorId != comment.AuthorId ||
                    existing.AuthorName != comment.AuthorName)
                {
                    await _highlightRepository.UpdateColumnsAsync(
                        it => new CommentHighlight
                        {
                            LikeCount = comment.LikeCount,
                            Rank = rank,
                            ContentSnapshot = comment.Content,
                            AuthorId = comment.AuthorId,
                            AuthorName = comment.AuthorName
                        },
                        h => h.Id == existing.Id);
                    changed = true;
                }

                rank++;
                continue;
            }

            newHighlights.Add(new CommentHighlight
            {
                PostId = postId,
                CommentId = comment.Id,
                ParentCommentId = parentCommentId,
                HighlightType = highlightType,
                StatDate = DateTime.Today,
                LikeCount = comment.LikeCount,
                Rank = rank,
                ContentSnapshot = comment.Content,
                AuthorId = comment.AuthorId,
                AuthorName = comment.AuthorName,
                IsCurrent = true,
                TenantId = comment.TenantId,
                CreateTime = DateTime.Now,
                CreateBy = createBy
            });

            rank++;
        }

        if (newHighlights.Any())
        {
            await _highlightRepository.AddRangeAsync(newHighlights);
            changed = true;

            foreach (var highlight in newHighlights)
            {
                await QueueBaseHighlightRewardAsync(highlight);
            }
        }

        if (changed)
        {
            await _caching.RemoveAsync(cacheKey);
        }

        result.VoChanged = changed;
        return result;
    }

    private async Task QueueBaseHighlightRewardAsync(CommentHighlight highlight)
    {
        var reliableOutboxService = _reliableOutboxService
            ?? throw new InvalidOperationException("可靠 Outbox 服务未注册");
        await reliableOutboxService.AddAsync(
            ReliableOutboxSources.Main,
            highlight.TenantId,
            ReliableTaskTypes.HighlightBaseReward,
            $"task:highlight-base:{highlight.Id}",
            "CommentHighlight",
            highlight.Id.ToString(),
            new HighlightBaseRewardTaskPayload(
                highlight.Id,
                highlight.CommentId,
                highlight.AuthorId,
                highlight.HighlightType,
                highlight.LikeCount),
            DateTime.UtcNow);
    }

    private async Task QueueHighlightLikeBonusAsync(
        long highlightId,
        long authorId,
        int likeIncrement,
        int highlightType,
        int likeCountAfter,
        long tenantId)
    {
        var reliableOutboxService = _reliableOutboxService
            ?? throw new InvalidOperationException("可靠 Outbox 服务未注册");
        await reliableOutboxService.AddAsync(
            ReliableOutboxSources.Main,
            tenantId,
            ReliableTaskTypes.HighlightBonusReward,
            $"task:highlight-bonus:{highlightId}:to-like:{likeCountAfter}",
            "CommentHighlight",
            highlightId.ToString(),
            new HighlightBonusRewardTaskPayload(
                highlightId,
                authorId,
                highlightType,
                likeIncrement,
                likeCountAfter),
            DateTime.UtcNow);
    }

    private async Task FillSingleHighlightStatusAsync(CommentVo comment)
    {
        var highlight = await _highlightRepository.QueryFirstAsync(
            h => h.CommentId == comment.VoId && h.LikeCount > 0 && h.IsCurrent);

        if (highlight == null)
        {
            return;
        }

        if (highlight.HighlightType == 1 && comment.VoParentId == null)
        {
            comment.VoIsGodComment = true;
            comment.VoHighlightRank = highlight.Rank;
        }

        if (highlight.HighlightType == 2 && comment.VoParentId != null)
        {
            comment.VoIsSofa = true;
            comment.VoHighlightRank = highlight.Rank;
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
                h => h.PostId == postId && h.HighlightType == 1 && h.LikeCount > 0 && h.IsCurrent);

            var godCommentMap = godComments.ToDictionary(h => h.CommentId, h => h.Rank);

            // 2. 收集所有父评论的ID，批量查询沙发
            var parentCommentIds = rootComments.Select(c => c.VoId).ToList();

            // 🐛 安全检查：先查询所有沙发记录
            var sofas = await _highlightRepository.QueryAsync(
                h => h.ParentCommentId != null &&
                     parentCommentIds.Contains(h.ParentCommentId.Value) &&
                     h.HighlightType == 2 &&
                     h.LikeCount > 0 &&
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
