using Microsoft.AspNetCore.Http;
using Radish.Common.Exceptions;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;

namespace Radish.Service;

/// <summary>当前用户的私有帖子收藏应用服务。</summary>
public sealed class UserPostBookmarkService : IUserPostBookmarkService
{
    private const string PostPublicIdPrefix = "pst_";

    private readonly IUserPostBookmarkRepository _bookmarkRepository;
    private readonly IBaseRepository<Post> _postRepository;
    private readonly IBaseRepository<User> _userRepository;
    private readonly IBaseRepository<Category> _categoryRepository;
    private readonly IBaseRepository<PostTag> _postTagRepository;
    private readonly IBaseRepository<Tag> _tagRepository;
    private readonly TimeProvider _timeProvider;

    public UserPostBookmarkService(
        IUserPostBookmarkRepository bookmarkRepository,
        IBaseRepository<Post> postRepository,
        IBaseRepository<User> userRepository,
        IBaseRepository<Category> categoryRepository,
        IBaseRepository<PostTag> postTagRepository,
        IBaseRepository<Tag> tagRepository,
        TimeProvider timeProvider)
    {
        _bookmarkRepository = bookmarkRepository;
        _postRepository = postRepository;
        _userRepository = userRepository;
        _categoryRepository = categoryRepository;
        _postTagRepository = postTagRepository;
        _tagRepository = tagRepository;
        _timeProvider = timeProvider;
    }

    public async Task<PostBookmarkStateVo> SetStateAsync(
        long tenantId,
        long currentUserId,
        string currentUserName,
        string postPublicId,
        bool isBookmarked)
    {
        EnsureAuthenticated(currentUserId);
        var normalizedTenantId = Math.Max(0, tenantId);
        var normalizedPublicId = postPublicId?.Trim().ToLowerInvariant();
        if (!HasPostPublicIdFormat(normalizedPublicId))
        {
            throw PostNotFound();
        }

        var post = await _bookmarkRepository.QueryPostByPublicIdAsync(
            normalizedTenantId,
            normalizedPublicId!);
        EnsurePostAvailable(post);
        return await SetStateCoreAsync(
            normalizedTenantId,
            currentUserId,
            currentUserName,
            post!,
            isBookmarked);
    }

    public async Task<PostBookmarkStateVo> SetStateByLegacyPostIdAsync(
        long tenantId,
        long currentUserId,
        string currentUserName,
        long postId,
        bool isBookmarked)
    {
        EnsureAuthenticated(currentUserId);
        var normalizedTenantId = Math.Max(0, tenantId);
        var post = await _bookmarkRepository.QueryPostByIdAsync(normalizedTenantId, postId);
        EnsurePostAvailable(post);
        await EnsurePostPublicIdAsync(post!, normalizedTenantId);
        return await SetStateCoreAsync(
            normalizedTenantId,
            currentUserId,
            currentUserName,
            post!,
            isBookmarked);
    }

    public async Task<VoPagedResult<UserPostBookmarkVo>> GetMineAsync(
        long tenantId,
        long currentUserId,
        int pageIndex,
        int pageSize)
    {
        EnsureAuthenticated(currentUserId);
        var normalizedTenantId = Math.Max(0, tenantId);
        var safePageIndex = Math.Max(1, pageIndex);
        var safePageSize = Math.Clamp(pageSize, 1, 50);
        var (bookmarks, total) = await _bookmarkRepository.QueryMineAsync(
            normalizedTenantId,
            currentUserId,
            safePageIndex,
            safePageSize);
        if (bookmarks.Count == 0)
        {
            return new VoPagedResult<UserPostBookmarkVo>
            {
                VoTotal = total,
                VoPageIndex = safePageIndex,
                VoPageSize = safePageSize
            };
        }

        var postIds = bookmarks.Select(item => item.PostId).Distinct().ToList();
        var posts = await _postRepository.QueryAsync(post =>
            post.TenantId == normalizedTenantId &&
            postIds.Contains(post.Id) &&
            post.IsPublished &&
            post.IsEnabled &&
            !post.IsDeleted);
        var availablePosts = posts
            .Where(post => HasPostPublicIdFormat(post.PublicId))
            .ToDictionary(post => post.Id);
        var authorIds = availablePosts.Values
            .Select(post => post.AuthorId)
            .Where(id => id > 0)
            .Distinct()
            .ToList();
        var categoryIds = availablePosts.Values
            .Select(post => post.CategoryId)
            .Where(id => id > 0)
            .Distinct()
            .ToList();
        var authors = authorIds.Count == 0
            ? []
            : await _userRepository.QueryAsync(user =>
                user.TenantId == normalizedTenantId &&
                authorIds.Contains(user.Id));
        var categories = categoryIds.Count == 0
            ? []
            : await _categoryRepository.QueryAsync(category =>
                categoryIds.Contains(category.Id) &&
                category.IsEnabled &&
                !category.IsDeleted);
        var postTags = availablePosts.Count == 0
            ? []
            : await _postTagRepository.QueryAsync(item =>
                postIds.Contains(item.PostId));
        var tagIds = postTags.Select(item => item.TagId).Distinct().ToList();
        var tags = tagIds.Count == 0
            ? []
            : await _tagRepository.QueryAsync(tag =>
                tagIds.Contains(tag.Id) &&
                tag.IsEnabled &&
                !tag.IsDeleted);

        var authorMap = authors.ToDictionary(user => user.Id);
        var categoryMap = categories.ToDictionary(category => category.Id);
        var tagMap = tags.ToDictionary(tag => tag.Id);
        var tagsByPost = postTags
            .GroupBy(item => item.PostId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(item => tagMap.GetValueOrDefault(item.TagId))
                    .Where(tag => tag != null)
                    .OrderBy(tag => tag!.SortOrder)
                    .ThenBy(tag => tag!.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(tag => new UserPostBookmarkTagVo
                    {
                        VoName = tag!.Name,
                        VoSlug = TagSlugHelper.BuildCanonicalSlug(tag.Name, tag.Slug)
                    })
                    .ToList());

        return new VoPagedResult<UserPostBookmarkVo>
        {
            VoItems = bookmarks
                .Select(bookmark => BuildListItem(
                    bookmark,
                    availablePosts.GetValueOrDefault(bookmark.PostId),
                    authorMap,
                    categoryMap,
                    tagsByPost))
                .ToList(),
            VoTotal = total,
            VoPageIndex = safePageIndex,
            VoPageSize = safePageSize
        };
    }

    public async Task<PostBookmarkRemoveVo> RemoveAsync(
        long tenantId,
        long currentUserId,
        string currentUserName,
        string bookmarkPublicId)
    {
        EnsureAuthenticated(currentUserId);
        var normalizedPublicId = bookmarkPublicId?.Trim().ToLowerInvariant() ?? string.Empty;
        await _bookmarkRepository.RemoveAsync(
            Math.Max(0, tenantId),
            currentUserId,
            normalizedPublicId,
            NormalizeOperatorName(currentUserName, currentUserId),
            _timeProvider.GetUtcNow().UtcDateTime);
        return new PostBookmarkRemoveVo
        {
            VoBookmarkPublicId = normalizedPublicId,
            VoRemoved = true
        };
    }

    private async Task<PostBookmarkStateVo> SetStateCoreAsync(
        long tenantId,
        long currentUserId,
        string currentUserName,
        Post post,
        bool isBookmarked)
    {
        var command = new PostBookmarkStateCommand(
            tenantId,
            currentUserId,
            post.Id,
            NormalizeOperatorName(currentUserName, currentUserId),
            isBookmarked,
            _timeProvider.GetUtcNow().UtcDateTime);
        try
        {
            var result = await _bookmarkRepository.SetStateAsync(command);
            return BuildStateVo(result);
        }
        catch (PostBookmarkPostNotFoundException)
        {
            throw PostNotFound();
        }
        catch (PostBookmarkPostUnavailableException)
        {
            throw PostUnavailable();
        }
        catch (PostBookmarkUserUnavailableException)
        {
            throw UserUnavailable();
        }
        catch (PostBookmarkStateConflictException)
        {
            throw StateConflict();
        }
    }

    private async Task EnsurePostPublicIdAsync(Post post, long tenantId)
    {
        if (HasPostPublicIdFormat(post.PublicId))
        {
            post.PublicId = post.PublicId!.Trim().ToLowerInvariant();
            return;
        }
        if (!string.IsNullOrWhiteSpace(post.PublicId))
        {
            throw StateConflict();
        }

        var generated = $"{PostPublicIdPrefix}{Guid.CreateVersion7():N}";
        var affectedRows = await _postRepository.UpdateColumnsAsync(
            item => new Post { PublicId = generated },
            item =>
                item.TenantId == tenantId &&
                item.Id == post.Id &&
                (item.PublicId == null || item.PublicId == string.Empty));
        if (affectedRows > 0)
        {
            post.PublicId = generated;
            return;
        }

        var refreshed = await _bookmarkRepository.QueryPostByIdAsync(tenantId, post.Id);
        if (!HasPostPublicIdFormat(refreshed?.PublicId))
        {
            throw StateConflict();
        }
        post.PublicId = refreshed!.PublicId!.Trim().ToLowerInvariant();
    }

    private static UserPostBookmarkVo BuildListItem(
        UserPostBookmark bookmark,
        Post? post,
        IReadOnlyDictionary<long, User> authorMap,
        IReadOnlyDictionary<long, Category> categoryMap,
        IReadOnlyDictionary<long, List<UserPostBookmarkTagVo>> tagsByPost)
    {
        var item = new UserPostBookmarkVo
        {
            VoBookmarkPublicId = bookmark.PublicId,
            VoBookmarkedAt = bookmark.BookmarkedAt,
            VoTargetStatus = PostBookmarkTargetStatuses.Unavailable
        };
        if (post == null)
        {
            return item;
        }

        authorMap.TryGetValue(post.AuthorId, out var author);
        categoryMap.TryGetValue(post.CategoryId, out var category);
        item.VoTargetStatus = PostBookmarkTargetStatuses.Available;
        item.VoPostPublicId = post.PublicId!.Trim().ToLowerInvariant();
        item.VoTitle = post.Title;
        item.VoSummary = post.Summary;
        item.VoAuthorPublicId =
            author is { IsEnable: true, IsDeleted: false } &&
            User.HasPublicIdFormat(author.PublicId)
                ? author.PublicId!.Trim().ToLowerInvariant()
                : null;
        item.VoAuthorName = post.AuthorName;
        item.VoPublishTime = post.PublishTime ?? post.CreateTime;
        item.VoCategoryName = category?.Name;
        item.VoTags = tagsByPost.GetValueOrDefault(post.Id) ?? [];
        item.VoCoverAttachmentId = post.CoverAttachmentId;
        item.VoViewCount = post.ViewCount;
        item.VoLikeCount = post.LikeCount;
        item.VoCommentCount = post.CommentCount;
        item.VoCollectCount = post.CollectCount;
        return item;
    }

    private static PostBookmarkStateVo BuildStateVo(PostBookmarkWriteResult result)
    {
        if (!HasPostPublicIdFormat(result.Post.PublicId))
        {
            throw StateConflict();
        }

        return new PostBookmarkStateVo
        {
            VoBookmarkPublicId = result.Bookmark is not null &&
                                 UserPostBookmark.HasPublicIdFormat(result.Bookmark.PublicId)
                ? result.Bookmark.PublicId.Trim().ToLowerInvariant()
                : null,
            VoPostPublicId = result.Post.PublicId!.Trim().ToLowerInvariant(),
            VoIsBookmarked = result.IsBookmarked,
            VoCollectCount = result.CollectCount,
            VoBookmarkedAt = result.IsBookmarked ? result.Bookmark?.BookmarkedAt : null
        };
    }

    private static void EnsurePostAvailable(Post? post)
    {
        if (post == null)
        {
            throw PostNotFound();
        }
        if (post.IsDeleted || !post.IsEnabled || !post.IsPublished)
        {
            throw PostUnavailable();
        }
    }

    private static void EnsureAuthenticated(long currentUserId)
    {
        if (currentUserId <= 0)
        {
            throw new BusinessException(
                "请先登录后再管理收藏",
                StatusCodes.Status401Unauthorized,
                "PostBookmark.AuthenticationRequired",
                "error.post_bookmark.authentication_required");
        }
    }

    private static bool HasPostPublicIdFormat(string? value)
    {
        var normalized = value?.Trim();
        return normalized is { Length: 36 } &&
               normalized.StartsWith(PostPublicIdPrefix, StringComparison.OrdinalIgnoreCase) &&
               normalized[PostPublicIdPrefix.Length..].All(Uri.IsHexDigit);
    }

    private static string NormalizeOperatorName(string operatorName, long userId)
    {
        return string.IsNullOrWhiteSpace(operatorName)
            ? $"User-{userId}"
            : operatorName.Trim();
    }

    private static BusinessException PostNotFound() =>
        new(
            "帖子不存在",
            StatusCodes.Status404NotFound,
            "PostBookmark.PostNotFound",
            "error.post_bookmark.post_not_found");

    private static BusinessException PostUnavailable() =>
        new(
            "帖子当前不可收藏",
            StatusCodes.Status409Conflict,
            "PostBookmark.PostUnavailable",
            "error.post_bookmark.post_unavailable");

    private static BusinessException UserUnavailable() =>
        new(
            "当前用户不可建立收藏",
            StatusCodes.Status403Forbidden,
            "PostBookmark.UserUnavailable",
            "error.post_bookmark.user_unavailable");

    private static BusinessException StateConflict() =>
        new(
            "收藏状态发生冲突，请刷新后重试",
            StatusCodes.Status409Conflict,
            "PostBookmark.StateConflict",
            "error.post_bookmark.state_conflict");
}
