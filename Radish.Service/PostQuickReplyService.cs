using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AutoMapper;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Radish.Common.AttributeTool;
using Radish.Common.CacheTool;
using Radish.Common.Exceptions;
using Radish.Common.OptionTool;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Service.Base;
using Radish.Shared.CustomEnum;
using SqlSugar;

namespace Radish.Service;

/// <summary>帖子轻回应服务实现</summary>
public class PostQuickReplyService : BaseService<PostQuickReply, PostQuickReplyVo>, IPostQuickReplyService
{
    private readonly IBaseRepository<PostQuickReply> _postQuickReplyRepository;
    private readonly IBaseRepository<Post> _postRepository;
    private readonly ICaching _caching;
    private readonly IAttachmentService? _attachmentService;
    private readonly ISystemSettingProvider _systemSettingProvider;
    private readonly ForumQuickReplyOptions _options;
    private readonly IReliableOutboxService? _reliableOutboxService;
    private readonly ILogger<PostQuickReplyService>? _logger;
    private readonly IBaseRepository<User>? _userRepository;

    public PostQuickReplyService(
        IMapper mapper,
        IBaseRepository<PostQuickReply> baseRepository,
        IBaseRepository<Post> postRepository,
        ICaching caching,
        ISystemSettingProvider systemSettingProvider,
        IOptions<ForumQuickReplyOptions> options,
        IAttachmentService? attachmentService = null,
        INotificationService? notificationService = null,
        ILogger<PostQuickReplyService>? logger = null,
        IBaseRepository<User>? userRepository = null,
        IReliableOutboxService? reliableOutboxService = null)
        : base(mapper, baseRepository)
    {
        _postQuickReplyRepository = baseRepository;
        _postRepository = postRepository;
        _caching = caching;
        _systemSettingProvider = systemSettingProvider;
        _options = options.Value;
        _attachmentService = attachmentService;
        _reliableOutboxService = reliableOutboxService;
        _logger = logger;
        _userRepository = userRepository;
    }

    public async Task<PostQuickReplyWallVo> GetRecentByPostIdAsync(long postId, int take)
    {
        if (postId <= 0)
        {
            throw new ArgumentException("帖子ID必须大于0");
        }

        EnsureFeatureEnabled();

        var takeSettings = await GetTakeSettingsAsync();
        var safeTake = Math.Clamp(take <= 0 ? takeSettings.DefaultTake : take, 1, takeSettings.MaxTake);
        await EnsurePostExistsAsync(postId);

        var (items, total) = await _postQuickReplyRepository.QueryPageAsync(
            reply => reply.PostId == postId &&
                     reply.Status == (int)PostQuickReplyStatusEnum.Visible &&
                     !reply.IsDeleted,
            1,
            safeTake,
            reply => reply.CreateTime,
            SqlSugar.OrderByType.Desc);

        var replyVos = Mapper.Map<List<PostQuickReplyVo>>(items);
        await FillAuthorProfilesAsync(replyVos);

        return new PostQuickReplyWallVo
        {
            VoItems = replyVos,
            VoTotal = total
        };
    }

    public async Task<(List<UserPostQuickReplyVo> items, int total)> GetMinePageAsync(long userId, int pageIndex, int pageSize)
    {
        EnsureFeatureEnabled();

        if (userId <= 0)
        {
            throw new ArgumentException("用户ID必须大于0");
        }

        var safePageIndex = pageIndex < 1 ? 1 : pageIndex;
        var safePageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 100);

        var (items, total) = await _postQuickReplyRepository.QueryPageAsync(
            reply => reply.AuthorId == userId && !reply.IsDeleted,
            safePageIndex,
            safePageSize,
            reply => reply.CreateTime,
            SqlSugar.OrderByType.Desc,
            reply => reply.Id,
            SqlSugar.OrderByType.Desc);

        if (items.Count == 0)
        {
            return ([], total);
        }

        var postIds = items
            .Select(item => item.PostId)
            .Where(postId => postId > 0)
            .Distinct()
            .ToList();

        var postMap = postIds.Count == 0
            ? new Dictionary<long, Post>()
            : (await _postRepository.QueryAsync(post => postIds.Contains(post.Id)))
                .GroupBy(post => post.Id)
                .ToDictionary(group => group.Key, group => group.First());

        var result = items.Select(item =>
        {
            postMap.TryGetValue(item.PostId, out var post);
            return new UserPostQuickReplyVo
            {
                VoId = item.Id,
                VoPostId = item.PostId,
                VoPostPublicId = NormalizePostPublicId(post?.PublicId),
                VoPostTitle = !string.IsNullOrWhiteSpace(post?.Title)
                    ? post.Title.Trim()
                    : $"帖子 {item.PostId}",
                VoContent = item.Content,
                VoCreateTime = item.CreateTime
            };
        }).ToList();

        return (result, total);
    }

    private static string? NormalizePostPublicId(string? publicId)
    {
        return string.IsNullOrWhiteSpace(publicId) ? null : publicId.Trim();
    }

    [UseTran]
    public async Task<PostQuickReplyVo> CreateAsync(CreatePostQuickReplyDto request, long userId, string userName, long tenantId)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureFeatureEnabled();

        if (userId <= 0)
        {
            throw new BusinessException("请先登录后再发布轻回应", 401, "Auth.Unauthorized", "error.auth.unauthorized");
        }

        if (request.PostId <= 0)
        {
            throw new ArgumentException("帖子ID必须大于0");
        }

        var normalizedContent = NormalizeContent(request.Content);
        if (string.IsNullOrWhiteSpace(normalizedContent))
        {
            throw new ArgumentException("轻回应内容不能为空");
        }

        var maxContentLength = await GetMaxContentLengthAsync();
        if (normalizedContent.Length > maxContentLength)
        {
            throw new ArgumentException($"轻回应内容不能超过{maxContentLength}个字符");
        }

        var post = await EnsurePostExistsAsync(request.PostId);
        if (post.IsLocked)
        {
            throw new BusinessException("当前帖子已锁定，无法发送轻回应", 409, "Forum.PostLocked", "error.forum.post_locked");
        }

        var rateLimitSettings = await GetRateLimitSettingsAsync();
        await EnsureRateLimitAsync(userId, request.PostId, normalizedContent, rateLimitSettings);

        var safeUserName = string.IsNullOrWhiteSpace(userName) ? $"User-{userId}" : userName.Trim();
        var entity = new PostQuickReply
        {
            PostId = request.PostId,
            AuthorId = userId,
            AuthorName = safeUserName,
            Content = normalizedContent,
            NormalizedContent = normalizedContent,
            Status = (int)PostQuickReplyStatusEnum.Visible,
            TenantId = tenantId > 0 ? tenantId : 0,
            CreateTime = DateTime.UtcNow,
            CreateBy = safeUserName,
            CreateId = userId
        };

        var quickReplyId = await _postQuickReplyRepository.AddAsync(entity);
        entity.Id = quickReplyId;

        await _caching.SetStringAsync(
            BuildCooldownCacheKey(userId, request.PostId),
            "1",
            TimeSpan.FromSeconds(rateLimitSettings.PerPostCooldownSeconds));
        await _caching.SetStringAsync(
            BuildDuplicateCacheKey(userId, request.PostId, normalizedContent),
            "1",
            TimeSpan.FromSeconds(rateLimitSettings.DuplicateWindowSeconds));

        var replyVo = Mapper.Map<PostQuickReplyVo>(entity);
        replyVo.VoId = quickReplyId;
        await FillAuthorProfilesAsync([replyVo]);
        await TrySendPostQuickReplyNotificationAsync(post, entity, replyVo.VoAuthorAvatarUrl);

        return replyVo;
    }

    [UseTran]
    public async Task DeleteAsync(long quickReplyId, long operatorId, string operatorName, bool isAdmin)
    {
        EnsureFeatureEnabled();

        if (quickReplyId <= 0)
        {
            throw new ArgumentException("轻回应ID必须大于0");
        }

        if (operatorId <= 0)
        {
            throw new BusinessException("请先登录后再删除轻回应", 401, "Auth.Unauthorized", "error.auth.unauthorized");
        }

        var quickReply = await _postQuickReplyRepository.QueryFirstAsync(reply => reply.Id == quickReplyId && !reply.IsDeleted);
        if (quickReply == null)
        {
            throw new BusinessException("轻回应不存在", 404, "QuickReply.NotFound", "error.quick_reply.not_found");
        }

        if (quickReply.AuthorId != operatorId && !isAdmin)
        {
            throw new BusinessException("无权删除此轻回应", 403, "QuickReply.DeleteForbidden", "error.quick_reply.delete_forbidden");
        }

        var safeOperatorName = string.IsNullOrWhiteSpace(operatorName) ? $"User-{operatorId}" : operatorName.Trim();
        await _postQuickReplyRepository.UpdateColumnsAsync(
            reply => new PostQuickReply
            {
                IsDeleted = true,
                DeletedAt = DateTime.UtcNow,
                DeletedBy = safeOperatorName,
                ModifyTime = DateTime.UtcNow,
                ModifyBy = safeOperatorName,
                ModifyId = operatorId
            },
            reply => reply.Id == quickReplyId);
    }

    private async Task<Post> EnsurePostExistsAsync(long postId)
    {
        var post = await _postRepository.QueryByIdAsync(postId);

        if (post == null || post.IsDeleted)
        {
            throw new BusinessException("帖子不存在或不可访问", 404, "Forum.PostNotFound", "error.forum.post_not_found");
        }

        return post;
    }

    private async Task EnsureRateLimitAsync(
        long userId,
        long postId,
        string normalizedContent,
        QuickReplyRateLimitSettings rateLimitSettings)
    {
        var cooldownCacheKey = BuildCooldownCacheKey(userId, postId);
        if (await _caching.ExistsAsync(cooldownCacheKey))
        {
            throw new BusinessException(
                $"发送过于频繁，请{rateLimitSettings.PerPostCooldownSeconds}秒后再试",
                429,
                "QuickReply.RateLimitExceeded",
                "error.quick_reply.rate_limit_exceeded");
        }

        var duplicateCacheKey = BuildDuplicateCacheKey(userId, postId, normalizedContent);
        if (await _caching.ExistsAsync(duplicateCacheKey))
        {
            throw new BusinessException("相同内容短时间内请勿重复发送", 409, "QuickReply.DuplicateContent", "error.quick_reply.duplicate_content");
        }
    }

    private void EnsureFeatureEnabled()
    {
        if (!_options.Enable)
        {
            throw new BusinessException("轻回应功能当前未启用", 503, "QuickReply.Disabled", "error.quick_reply.disabled");
        }
    }

    private async Task<int> GetMaxContentLengthAsync()
    {
        return await _systemSettingProvider.GetInt32Async(SystemConfigDefaults.QuickReplyMaxContentLengthKey);
    }

    private async Task<QuickReplyTakeSettings> GetTakeSettingsAsync()
    {
        var defaultTake = await _systemSettingProvider.GetInt32Async(SystemConfigDefaults.QuickReplyDefaultTakeKey);
        var maxTake = await _systemSettingProvider.GetInt32Async(SystemConfigDefaults.QuickReplyMaxTakeKey);
        if (defaultTake > maxTake)
        {
            throw new InvalidOperationException("轻回应返回条数系统设置无效：默认返回条数不能大于最大返回条数");
        }

        return new QuickReplyTakeSettings(defaultTake, maxTake);
    }

    private async Task<QuickReplyRateLimitSettings> GetRateLimitSettingsAsync()
    {
        var perPostCooldownSeconds = await _systemSettingProvider.GetInt32Async(
            SystemConfigDefaults.QuickReplyPerPostCooldownSecondsKey);
        var duplicateWindowSeconds = await _systemSettingProvider.GetInt32Async(
            SystemConfigDefaults.QuickReplyDuplicateWindowSecondsKey);

        return new QuickReplyRateLimitSettings(perPostCooldownSeconds, duplicateWindowSeconds);
    }

    private async Task FillAuthorProfilesAsync(List<PostQuickReplyVo> replies)
    {
        if (replies.Count == 0)
        {
            return;
        }

        var userIds = replies
            .Select(reply => reply.VoAuthorId)
            .Where(userId => userId > 0)
            .Distinct()
            .ToList();

        if (userIds.Count == 0)
        {
            return;
        }

        Dictionary<long, string> avatarMap = new();
        if (_attachmentService != null)
        {
            avatarMap = (await _attachmentService.GetLatestAvatarAssetMapAsync(userIds))
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Value.Url))
                .ToDictionary(entry => entry.Key, entry => entry.Value.Url);
        }

        Dictionary<long, string> displayNameMap = new();
        if (_userRepository != null)
        {
            displayNameMap = ForumDisplayNameHelper.BuildMap(await _userRepository.QueryAsync(user =>
                userIds.Contains(user.Id) &&
                !user.IsDeleted));
        }

        foreach (var reply in replies)
        {
            if (displayNameMap.TryGetValue(reply.VoAuthorId, out var displayName))
            {
                reply.VoAuthorName = displayName;
            }

            if (avatarMap.TryGetValue(reply.VoAuthorId, out var avatarUrl))
            {
                reply.VoAuthorAvatarUrl = avatarUrl;
            }
        }
    }

    private async Task TrySendPostQuickReplyNotificationAsync(Post post, PostQuickReply quickReply, string? triggerAvatar)
    {
        if (post.AuthorId <= 0 || post.AuthorId == quickReply.AuthorId)
        {
            return;
        }

        try
        {
            var notificationId = SnowFlakeSingle.Instance.NextId();
            var notification = new CreateNotificationDto
            {
                NotificationId = notificationId,
                BusinessKey = $"notification:post-quick-reply:{quickReply.Id}:receiver:{post.AuthorId}",
                Type = NotificationType.PostQuickReplied,
                Title = "帖子收到轻回应",
                Content = quickReply.Content,
                Priority = (int)NotificationPriority.Normal,
                BusinessType = BusinessType.Post,
                BusinessId = post.Id,
                TriggerId = quickReply.AuthorId,
                TriggerName = quickReply.AuthorName,
                TriggerAvatar = triggerAvatar,
                ReceiverUserIds = new List<long> { post.AuthorId },
                ExtData = NotificationNavigationHelper.BuildForumNavigationExtData(post.Id, postPublicId: post.PublicId),
                TenantId = quickReply.TenantId
            };
            var reliableOutboxService = _reliableOutboxService
                ?? throw new InvalidOperationException("可靠 Outbox 服务未注册");
            await reliableOutboxService.AddAsync(
                ReliableOutboxSources.Main,
                quickReply.TenantId,
                ReliableTaskTypes.NotificationRequested,
                $"task:notification:post-quick-reply:{quickReply.Id}",
                "PostQuickReply",
                quickReply.Id.ToString(),
                new NotificationRequestedTaskPayload(notification),
                DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex,
                "[PostQuickReplyService] 发送轻回应通知失败，PostId={PostId}, QuickReplyId={QuickReplyId}, ReceiverUserId={ReceiverUserId}",
                post.Id,
                quickReply.Id,
                post.AuthorId);
            throw;
        }
    }

    private static string NormalizeContent(string? content)
    {
        var trimmed = content?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        return Regex.Replace(trimmed, "\\s+", " ");
    }

    private static string BuildCooldownCacheKey(long userId, long postId)
    {
        return $"post_quick_reply:cooldown:{userId}:{postId}";
    }

    private static string BuildDuplicateCacheKey(long userId, long postId, string normalizedContent)
    {
        return $"post_quick_reply:dedup:{userId}:{postId}:{ComputeSha256(normalizedContent)}";
    }

    private static string ComputeSha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }

    private sealed record QuickReplyTakeSettings(int DefaultTake, int MaxTake);

    private sealed record QuickReplyRateLimitSettings(int PerPostCooldownSeconds, int DuplicateWindowSeconds);
}
