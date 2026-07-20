using System.Globalization;
using System.Text.Json;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Shared.CustomEnum;

namespace Radish.Service;

/// <summary>通知目标解析只返回当前接收者仍可访问的站内资源。</summary>
public sealed class NotificationTargetResolver : INotificationTargetResolver
{
    private const string InvalidReason = "Notification.Target.Invalid";
    private const string DeletedReason = "Notification.Target.Deleted";
    private const string ForbiddenReason = "Notification.Target.Forbidden";

    private readonly IBaseRepository<Post> _postRepository;
    private readonly IBaseRepository<Comment> _commentRepository;
    private readonly IBaseRepository<User> _userRepository;
    private readonly IBaseRepository<Order> _orderRepository;
    private readonly IBaseRepository<UserBenefit> _benefitRepository;
    private readonly IBaseRepository<WikiDocument> _documentRepository;
    private readonly IChannelMessageRepository _messageRepository;
    private readonly IChatChannelAccessService _chatAccessService;

    public NotificationTargetResolver(
        IBaseRepository<Post> postRepository,
        IBaseRepository<Comment> commentRepository,
        IBaseRepository<User> userRepository,
        IBaseRepository<Order> orderRepository,
        IBaseRepository<UserBenefit> benefitRepository,
        IBaseRepository<WikiDocument> documentRepository,
        IChannelMessageRepository messageRepository,
        IChatChannelAccessService chatAccessService)
    {
        _postRepository = postRepository;
        _commentRepository = commentRepository;
        _userRepository = userRepository;
        _orderRepository = orderRepository;
        _benefitRepository = benefitRepository;
        _documentRepository = documentRepository;
        _messageRepository = messageRepository;
        _chatAccessService = chatAccessService;
    }

    public async Task<IReadOnlyDictionary<long, NotificationTargetVo>> ResolveAsync(
        long tenantId,
        long userId,
        IReadOnlyCollection<Notification> notifications)
    {
        var parsedTargets = new Dictionary<long, NotificationTargetData>();
        var resolved = new Dictionary<long, NotificationTargetVo>();
        foreach (var notification in notifications)
        {
            try
            {
                parsedTargets[notification.Id] = NotificationTargetData.FromJson(notification.TargetDataJson);
            }
            catch (JsonException)
            {
                resolved[notification.Id] = Unavailable(InvalidReason);
            }
        }

        var forumTargets = notifications
            .Where(item => item.TargetKind == NotificationTargetKind.ForumPost && parsedTargets.ContainsKey(item.Id))
            .Select(item => parsedTargets[item.Id])
            .ToList();
        var postIds = forumTargets.Where(item => item.PostId.HasValue).Select(item => item.PostId!.Value).Distinct().ToList();
        var postPublicIds = forumTargets
            .Where(item => !item.PostId.HasValue)
            .Select(item => item.PostPublicId?.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var posts = postIds.Count == 0 ? [] : await _postRepository.QueryByIdsAsync(postIds);
        if (postPublicIds.Count > 0)
        {
            posts.AddRange(await _postRepository.QueryAsync(item =>
                item.PublicId != null && postPublicIds.Contains(item.PublicId)));
        }
        posts = posts.Where(item => item.IsEnabled && !item.IsDeleted).ToList();

        var commentIds = forumTargets.Where(item => item.CommentId.HasValue).Select(item => item.CommentId!.Value).Distinct().ToList();
        var comments = commentIds.Count == 0 ? [] : await _commentRepository.QueryByIdsAsync(commentIds);
        comments = comments.Where(item => item.IsEnabled && !item.IsDeleted).ToList();
        var userIds = parsedTargets.Values
            .Where(item => item.UserId.HasValue)
            .Select(item => item.UserId!.Value)
            .Distinct()
            .ToList();
        var userPublicIds = parsedTargets.Values
            .Where(item => !item.UserId.HasValue)
            .Select(item => item.UserPublicId?.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var users = userIds.Count == 0 ? [] : await _userRepository.QueryByIdsAsync(userIds);
        if (userPublicIds.Count > 0)
        {
            users.AddRange(await _userRepository.QueryAsync(item =>
                item.PublicId != null && userPublicIds.Contains(item.PublicId)));
        }
        users = users.Where(item => item.IsEnable && !item.IsDeleted).ToList();

        var orderIds = parsedTargets.Values.Where(item => item.OrderId.HasValue).Select(item => item.OrderId!.Value).Distinct().ToList();
        var orders = orderIds.Count == 0 ? [] : await _orderRepository.QueryByIdsAsync(orderIds);
        orders = orders.Where(item => !item.IsDeleted).ToList();
        var benefitIds = parsedTargets.Values.Where(item => item.BenefitId.HasValue).Select(item => item.BenefitId!.Value).Distinct().ToList();
        var benefits = benefitIds.Count == 0 ? [] : await _benefitRepository.QueryByIdsAsync(benefitIds);
        benefits = benefits.Where(item => !item.IsDeleted).ToList();
        var messageIds = parsedTargets.Values.Where(item => item.MessageId.HasValue).Select(item => item.MessageId!.Value).Distinct().ToList();
        var messages = messageIds.Count == 0 ? [] : await _messageRepository.QueryByIdsAsync(messageIds);
        messages = messages.Where(item => !item.IsDeleted).ToList();
        var documentSlugs = parsedTargets.Values
            .Select(item => item.DocumentSlug?.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var documents = documentSlugs.Count == 0
            ? []
            : await _documentRepository.QueryAsync(item => documentSlugs.Contains(item.Slug));
        documents = documents.Where(item => !item.IsDeleted).ToList();

        var postById = posts.DistinctBy(item => item.Id).ToDictionary(item => item.Id);
        var postByPublicId = posts
            .Where(item => !string.IsNullOrWhiteSpace(item.PublicId))
            .DistinctBy(item => item.PublicId, StringComparer.Ordinal)
            .ToDictionary(item => item.PublicId!, StringComparer.Ordinal);
        var commentById = comments.ToDictionary(item => item.Id);
        var userById = users.DistinctBy(item => item.Id).ToDictionary(item => item.Id);
        var userByPublicId = users
            .Where(item => !string.IsNullOrWhiteSpace(item.PublicId))
            .DistinctBy(item => item.PublicId, StringComparer.Ordinal)
            .ToDictionary(item => item.PublicId!, StringComparer.Ordinal);
        var orderById = orders.ToDictionary(item => item.Id);
        var benefitById = benefits.ToDictionary(item => item.Id);
        var messageById = messages.ToDictionary(item => item.Id);
        var documentBySlug = documents.ToDictionary(item => item.Slug, StringComparer.Ordinal);
        var chatAccessByChannel = new Dictionary<long, ChatChannelAccessResult>();

        foreach (var notification in notifications)
        {
            if (resolved.ContainsKey(notification.Id))
            {
                continue;
            }

            var target = parsedTargets[notification.Id];
            resolved[notification.Id] = notification.TargetKind switch
            {
                NotificationTargetKind.None => new NotificationTargetVo(),
                NotificationTargetKind.ForumPost => ResolveForumTarget(
                    target,
                    tenantId,
                    postById,
                    postByPublicId,
                    commentById),
                NotificationTargetKind.UserProfile => ResolveUserTarget(
                    target,
                    tenantId,
                    userById,
                    userByPublicId),
                NotificationTargetKind.ShopOrder => ResolveOrderTarget(target, tenantId, userId, orderById),
                NotificationTargetKind.Inventory => ResolveBenefitTarget(target, tenantId, userId, benefitById),
                NotificationTargetKind.Experience => ResolveExperienceTarget(target, tenantId, userId, userById),
                NotificationTargetKind.DocsDocument => ResolveDocumentTarget(target, tenantId, documentBySlug),
                NotificationTargetKind.GovernanceCase => Unavailable(ForbiddenReason),
                NotificationTargetKind.ChatConversation => await ResolveChatTargetAsync(
                    target,
                    tenantId,
                    userId,
                    messageById,
                    chatAccessByChannel),
                _ => Unavailable(InvalidReason)
            };
        }

        return resolved;
    }

    private static NotificationTargetVo ResolveForumTarget(
        NotificationTargetData target,
        long tenantId,
        IReadOnlyDictionary<long, Post> postById,
        IReadOnlyDictionary<string, Post> postByPublicId,
        IReadOnlyDictionary<long, Comment> commentById)
    {
        Post? post = null;
        if (target.PostId.HasValue)
        {
            postById.TryGetValue(target.PostId.Value, out post);
        }
        else if (!string.IsNullOrWhiteSpace(target.PostPublicId))
        {
            postByPublicId.TryGetValue(target.PostPublicId.Trim(), out post);
        }

        if (post == null)
        {
            return Unavailable(target.PostId.HasValue || !string.IsNullOrWhiteSpace(target.PostPublicId)
                ? DeletedReason
                : InvalidReason);
        }
        if (!IsTenantVisible(post.TenantId, tenantId))
        {
            return Unavailable(ForbiddenReason);
        }

        if (target.CommentId.HasValue &&
            (!commentById.TryGetValue(target.CommentId.Value, out var comment) || comment.PostId != post.Id))
        {
            return Unavailable(DeletedReason);
        }

        return Map(target, NotificationTargetKind.ForumPost);
    }

    private async Task<NotificationTargetVo> ResolveChatTargetAsync(
        NotificationTargetData target,
        long tenantId,
        long userId,
        IReadOnlyDictionary<long, ChannelMessage> messageById,
        IDictionary<long, ChatChannelAccessResult> accessByChannel)
    {
        if (!target.ChannelId.HasValue)
        {
            return Unavailable(InvalidReason);
        }

        if (!accessByChannel.TryGetValue(target.ChannelId.Value, out var access))
        {
            access = await _chatAccessService.GetAccessAsync(tenantId, userId, target.ChannelId.Value);
            accessByChannel[target.ChannelId.Value] = access;
        }
        if (!access.Exists)
        {
            return Unavailable(DeletedReason);
        }
        if (!access.CanView)
        {
            return Unavailable(ForbiddenReason);
        }
        if (target.MessageId.HasValue &&
            (!messageById.TryGetValue(target.MessageId.Value, out var message) ||
             message.ChannelId != target.ChannelId.Value ||
             !IsTenantVisible(message.TenantId, tenantId)))
        {
            return Unavailable(DeletedReason);
        }

        return Map(target, NotificationTargetKind.ChatConversation);
    }

    private static NotificationTargetVo ResolveUserTarget(
        NotificationTargetData target,
        long tenantId,
        IReadOnlyDictionary<long, User> userById,
        IReadOnlyDictionary<string, User> userByPublicId)
    {
        User? user = null;
        if (target.UserId.HasValue)
        {
            userById.TryGetValue(target.UserId.Value, out user);
        }
        else if (!string.IsNullOrWhiteSpace(target.UserPublicId))
        {
            userByPublicId.TryGetValue(target.UserPublicId.Trim(), out user);
        }

        if (user == null || !user.IsEnable)
        {
            return Unavailable(target.UserId.HasValue || !string.IsNullOrWhiteSpace(target.UserPublicId)
                ? DeletedReason
                : InvalidReason);
        }
        return IsTenantVisible(user.TenantId, tenantId)
            ? Map(target, NotificationTargetKind.UserProfile)
            : Unavailable(ForbiddenReason);
    }

    private static NotificationTargetVo ResolveOrderTarget(
        NotificationTargetData target,
        long tenantId,
        long userId,
        IReadOnlyDictionary<long, Order> orderById)
    {
        if (!target.OrderId.HasValue)
        {
            return Unavailable(InvalidReason);
        }
        if (!orderById.TryGetValue(target.OrderId.Value, out var order))
        {
            return Unavailable(DeletedReason);
        }
        return order.UserId == userId && IsTenantVisible(order.TenantId, tenantId)
            ? Map(target, NotificationTargetKind.ShopOrder)
            : Unavailable(ForbiddenReason);
    }

    private static NotificationTargetVo ResolveBenefitTarget(
        NotificationTargetData target,
        long tenantId,
        long userId,
        IReadOnlyDictionary<long, UserBenefit> benefitById)
    {
        if (!target.BenefitId.HasValue)
        {
            return Unavailable(InvalidReason);
        }
        if (!benefitById.TryGetValue(target.BenefitId.Value, out var benefit))
        {
            return Unavailable(DeletedReason);
        }
        return benefit.UserId == userId && IsTenantVisible(benefit.TenantId, tenantId)
            ? Map(target, NotificationTargetKind.Inventory)
            : Unavailable(ForbiddenReason);
    }

    private static NotificationTargetVo ResolveExperienceTarget(
        NotificationTargetData target,
        long tenantId,
        long userId,
        IReadOnlyDictionary<long, User> userById)
    {
        if (!target.UserId.HasValue)
        {
            return Unavailable(InvalidReason);
        }
        if (!userById.TryGetValue(target.UserId.Value, out var user) || !user.IsEnable)
        {
            return Unavailable(DeletedReason);
        }
        return target.UserId.Value == userId && IsTenantVisible(user.TenantId, tenantId)
            ? Map(target, NotificationTargetKind.Experience)
            : Unavailable(ForbiddenReason);
    }

    private static NotificationTargetVo ResolveDocumentTarget(
        NotificationTargetData target,
        long tenantId,
        IReadOnlyDictionary<string, WikiDocument> documentBySlug)
    {
        var slug = target.DocumentSlug?.Trim();
        if (string.IsNullOrWhiteSpace(slug))
        {
            return Unavailable(InvalidReason);
        }
        if (!documentBySlug.TryGetValue(slug, out var document) ||
            document.Status != (int)WikiDocumentStatusEnum.Published)
        {
            return Unavailable(DeletedReason);
        }
        if (!IsTenantVisible(document.TenantId, tenantId) ||
            document.Visibility == (int)WikiDocumentVisibilityEnum.Restricted)
        {
            return Unavailable(ForbiddenReason);
        }
        return Map(target, NotificationTargetKind.DocsDocument);
    }

    private static NotificationTargetVo Map(NotificationTargetData target, string kind)
    {
        return new NotificationTargetVo
        {
            VoKind = kind,
            VoPostId = ToId(target.PostId),
            VoPostPublicId = target.PostPublicId,
            VoCommentId = ToId(target.CommentId),
            VoChannelId = ToId(target.ChannelId),
            VoMessageId = ToId(target.MessageId),
            VoUserId = ToId(target.UserId),
            VoUserPublicId = target.UserPublicId,
            VoOrderId = ToId(target.OrderId),
            VoBenefitId = ToId(target.BenefitId),
            VoDocumentSlug = target.DocumentSlug,
            VoGovernanceCaseId = ToId(target.GovernanceCaseId)
        };
    }

    private static NotificationTargetVo Unavailable(string reason)
    {
        return new NotificationTargetVo
        {
            VoKind = NotificationTargetKind.None,
            VoUnavailableReason = reason
        };
    }

    private static string? ToId(long? value) => value?.ToString(CultureInfo.InvariantCulture);

    private static bool IsTenantVisible(long resourceTenantId, long requestTenantId)
    {
        return resourceTenantId == 0 || resourceTenantId == requestTenantId;
    }
}
