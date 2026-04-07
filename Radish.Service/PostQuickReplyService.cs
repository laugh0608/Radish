using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AutoMapper;
using Microsoft.Extensions.Options;
using Radish.Common.AttributeTool;
using Radish.Common.CacheTool;
using Radish.Common.OptionTool;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Service.Base;
using Radish.Shared.CustomEnum;

namespace Radish.Service;

/// <summary>帖子轻回应服务实现</summary>
public class PostQuickReplyService : BaseService<PostQuickReply, PostQuickReplyVo>, IPostQuickReplyService
{
    private readonly IBaseRepository<PostQuickReply> _postQuickReplyRepository;
    private readonly IBaseRepository<Post> _postRepository;
    private readonly ICaching _caching;
    private readonly IAttachmentService? _attachmentService;
    private readonly ForumQuickReplyOptions _options;

    public PostQuickReplyService(
        IMapper mapper,
        IBaseRepository<PostQuickReply> baseRepository,
        IBaseRepository<Post> postRepository,
        ICaching caching,
        IOptions<ForumQuickReplyOptions> options,
        IAttachmentService? attachmentService = null)
        : base(mapper, baseRepository)
    {
        _postQuickReplyRepository = baseRepository;
        _postRepository = postRepository;
        _caching = caching;
        _options = options.Value;
        _attachmentService = attachmentService;
    }

    public async Task<PostQuickReplyWallVo> GetRecentByPostIdAsync(long postId, int take)
    {
        if (postId <= 0)
        {
            throw new ArgumentException("帖子ID必须大于0");
        }

        EnsureFeatureEnabled();

        var defaultTake = Math.Max(1, _options.DefaultTake);
        var maxTake = Math.Max(defaultTake, _options.MaxTake);
        var safeTake = Math.Clamp(take <= 0 ? defaultTake : take, 1, maxTake);
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
        await FillAuthorAvatarUrlsAsync(replyVos);

        return new PostQuickReplyWallVo
        {
            VoItems = replyVos,
            VoTotal = total
        };
    }

    [UseTran]
    public async Task<PostQuickReplyVo> CreateAsync(CreatePostQuickReplyDto request, long userId, string userName, long tenantId)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureFeatureEnabled();

        if (userId <= 0)
        {
            throw new InvalidOperationException("请先登录后再发布轻回应");
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

        if (normalizedContent.Length > Math.Max(1, _options.MaxContentLength))
        {
            throw new ArgumentException($"轻回应内容不能超过{Math.Max(1, _options.MaxContentLength)}个字符");
        }

        var post = await EnsurePostExistsAsync(request.PostId);
        if (post.IsLocked)
        {
            throw new InvalidOperationException("当前帖子已锁定，无法发送轻回应");
        }

        await EnsureRateLimitAsync(userId, request.PostId, normalizedContent);

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

        await _caching.SetStringAsync(
            BuildCooldownCacheKey(userId, request.PostId),
            "1",
            TimeSpan.FromSeconds(Math.Max(1, _options.PerPostCooldownSeconds)));
        await _caching.SetStringAsync(
            BuildDuplicateCacheKey(userId, request.PostId, normalizedContent),
            "1",
            TimeSpan.FromSeconds(Math.Max(1, _options.DuplicateWindowSeconds)));

        var replyVo = Mapper.Map<PostQuickReplyVo>(entity);
        replyVo.VoId = quickReplyId;
        await FillAuthorAvatarUrlsAsync([replyVo]);

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
            throw new InvalidOperationException("请先登录后再删除轻回应");
        }

        var quickReply = await _postQuickReplyRepository.QueryFirstAsync(reply => reply.Id == quickReplyId && !reply.IsDeleted);
        if (quickReply == null)
        {
            throw new InvalidOperationException("轻回应不存在");
        }

        if (quickReply.AuthorId != operatorId && !isAdmin)
        {
            throw new InvalidOperationException("无权删除此轻回应");
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
        var post = await _postRepository.QueryFirstAsync(
            item => item.Id == postId &&
                    item.IsEnabled &&
                    item.IsPublished &&
                    !item.IsDeleted);

        if (post == null)
        {
            throw new InvalidOperationException("帖子不存在或不可访问");
        }

        return post;
    }

    private async Task EnsureRateLimitAsync(long userId, long postId, string normalizedContent)
    {
        var cooldownCacheKey = BuildCooldownCacheKey(userId, postId);
        if (await _caching.ExistsAsync(cooldownCacheKey))
        {
            throw new InvalidOperationException($"发送过于频繁，请{Math.Max(1, _options.PerPostCooldownSeconds)}秒后再试");
        }

        var duplicateCacheKey = BuildDuplicateCacheKey(userId, postId, normalizedContent);
        if (await _caching.ExistsAsync(duplicateCacheKey))
        {
            throw new InvalidOperationException("相同内容短时间内请勿重复发送");
        }
    }

    private void EnsureFeatureEnabled()
    {
        if (!_options.Enable)
        {
            throw new InvalidOperationException("轻回应功能当前未启用");
        }
    }

    private async Task FillAuthorAvatarUrlsAsync(List<PostQuickReplyVo> replies)
    {
        if (_attachmentService == null || replies.Count == 0)
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

        var avatarMap = (await _attachmentService.GetLatestAvatarAssetMapAsync(userIds))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Value.Url))
            .ToDictionary(entry => entry.Key, entry => entry.Value.Url);

        foreach (var reply in replies)
        {
            if (avatarMap.TryGetValue(reply.VoAuthorId, out var avatarUrl))
            {
                reply.VoAuthorAvatarUrl = avatarUrl;
            }
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
}
