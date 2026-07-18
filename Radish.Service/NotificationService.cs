using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Radish.Common;
using Radish.Common.Exceptions;
using Radish.IRepository;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using SqlSugar;

namespace Radish.Service;

/// <summary>以 Message 库分组与 revision 为真相源的通知中心服务。</summary>
public sealed class NotificationService : INotificationService
{
    private readonly INotificationInboxRepository _inboxRepository;
    private readonly IUserRepository _userRepository;
    private readonly INotificationPushService _pushService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        INotificationInboxRepository inboxRepository,
        IUserRepository userRepository,
        INotificationPushService pushService,
        TimeProvider timeProvider,
        ILogger<NotificationService> logger)
    {
        _inboxRepository = inboxRepository;
        _userRepository = userRepository;
        _pushService = pushService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<long> CreateNotificationAsync(CreateNotificationDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var receiverIds = dto.ReceiverUserIds
            .Where(userId => userId > 0)
            .Distinct()
            .OrderBy(userId => userId)
            .ToList();
        if (receiverIds.Count == 0)
        {
            throw new ArgumentException("接收者用户 ID 列表不能为空。", nameof(dto));
        }

        var definition = NotificationDefinitionRegistry.GetRequired(dto.Type);
        var targetKind = string.IsNullOrWhiteSpace(dto.TargetKind)
            ? definition.AllowedTargetKinds.Single()
            : dto.TargetKind.Trim();
        ValidateTarget(definition, targetKind, dto.Target);
        ValidateTemplateArguments(definition, dto.TemplateArguments);

        var nowUtc = GetUtcNow();
        var occurredAtUtc = NormalizeUtc(dto.OccurredAtUtc ?? nowUtc);
        if (dto.NotificationId.HasValue && !dto.OccurredAtUtc.HasValue)
        {
            throw new ArgumentException("可靠通知必须携带固定的 OccurredAtUtc。", nameof(dto));
        }

        var tenantId = dto.TenantId ?? 0;
        if (tenantId < 0)
        {
            throw new ArgumentException("租户 ID 不能小于 0。", nameof(dto));
        }

        var activeRecipientIds = await _userRepository.GetActiveUserIdsAsync(tenantId, receiverIds);
        if (activeRecipientIds.Count != receiverIds.Count)
        {
            throw new ArgumentException("通知接收者不存在、已停用或不属于指定租户。", nameof(dto));
        }

        var notificationId = dto.NotificationId ?? SnowFlakeSingle.Instance.NextId();
        var recipients = new List<NotificationInboxRecipient>(receiverIds.Count);
        foreach (var userId in receiverIds)
        {
            var preferences = await _inboxRepository.GetPreferencesAsync(tenantId, userId);
            preferences.TryGetValue(definition.Category, out var preference);
            var inAppEnabled = !definition.CanDisableInApp || preference?.InAppEnabled != false;
            if (!inAppEnabled)
            {
                continue;
            }

            var realtimePreviewAllowed = !definition.CanDisableRealtimePreview ||
                                         preference?.RealtimePreviewEnabled != false;
            recipients.Add(new NotificationInboxRecipient(userId, realtimePreviewAllowed));
        }

        if (recipients.Count == 0)
        {
            _logger.LogInformation(
                "通知 {NotificationId} 已按 {Category} 偏好抑制全部接收者",
                notificationId,
                definition.Category);
            return notificationId;
        }

        var target = dto.Target ?? new NotificationTargetData();
        var notification = new Notification(new NotificationInitializationOptions(
            definition.Kind,
            RequireSnapshot(dto.Title, "通知标题"))
        {
            Category = definition.Category,
            TemplateKey = definition.TemplateKey,
            TemplateArgumentsJson = JsonSerializer.Serialize(dto.TemplateArguments),
            TargetKind = targetKind,
            TargetDataJson = targetKind == NotificationTargetKind.None ? null : target.ToJson(),
            TargetSchemaVersion = 1,
            OccurredAtUtc = occurredAtUtc,
            Content = dto.Content,
            Priority = (int)definition.DefaultPriority,
            BusinessType = dto.BusinessType,
            BusinessId = dto.BusinessId,
            TriggerId = dto.TriggerId,
            TriggerName = dto.TriggerName,
            TriggerAvatar = dto.TriggerAvatar,
            ExtData = dto.ExtData,
            TenantId = tenantId
        })
        {
            Id = notificationId,
            BusinessKey = string.IsNullOrWhiteSpace(dto.BusinessKey)
                ? $"notification:{notificationId}"
                : dto.BusinessKey.Trim(),
            CreateTime = nowUtc
        };

        var persisted = await _inboxRepository.PersistAsync(notification, recipients, nowUtc);
        foreach (var change in persisted.RecipientChanges)
        {
            await PushChangedAsync(change, "Created");
        }

        return persisted.NotificationId;
    }

    public async Task<NotificationInboxPageVo> GetInboxAsync(
        long tenantId,
        long userId,
        NotificationInboxQueryDto query)
    {
        ArgumentNullException.ThrowIfNull(query);
        ValidateCategory(query.Category, allowEmpty: true);
        var currentSummary = await _inboxRepository.GetSummaryAsync(tenantId, userId);
        var cursor = DecodeCursor(query.Cursor);
        if (cursor != null && cursor.Revision != currentSummary.Revision)
        {
            throw CursorExpired();
        }

        var result = await _inboxRepository.QueryAsync(
            tenantId,
            userId,
            query.Category,
            query.OnlyUnread,
            cursor?.LastOccurredAtUtc,
            cursor?.GroupId,
            0,
            Math.Clamp(query.PageSize, 1, 50));
        if (result.Summary.Revision != currentSummary.Revision)
        {
            throw CursorExpired();
        }

        var items = result.Groups.Select(MapGroup).ToList();
        string? nextCursor = null;
        if (result.HasMore && result.Groups.Count > 0)
        {
            var last = result.Groups[^1].Group;
            nextCursor = EncodeCursor(new InboxCursor(
                result.Summary.Revision,
                last.LastOccurredAtUtc,
                last.Id));
        }

        return new NotificationInboxPageVo
        {
            VoItems = items,
            VoNextCursor = nextCursor,
            VoSummary = MapSummary(result.Summary)
        };
    }

    public async Task<NotificationInboxSummaryVo> GetInboxSummaryAsync(long tenantId, long userId)
    {
        return MapSummary(await _inboxRepository.GetSummaryAsync(tenantId, userId));
    }

    public async Task<NotificationInboxMutationVo> MarkInboxGroupsAsReadAsync(
        long tenantId,
        long userId,
        IReadOnlyCollection<long> groupIds)
    {
        var result = await _inboxRepository.MarkGroupsAsReadAsync(
            tenantId,
            userId,
            groupIds,
            GetUtcNow());
        await PushMutationAsync(userId, result, "Read");
        return MapMutation(result);
    }

    public async Task<NotificationInboxMutationVo> MarkAllInboxAsReadAsync(
        long tenantId,
        long userId,
        string? category = null)
    {
        ValidateCategory(category, allowEmpty: true);
        var result = await _inboxRepository.MarkAllAsReadAsync(
            tenantId,
            userId,
            category,
            GetUtcNow());
        await PushMutationAsync(userId, result, "ReadAll");
        return MapMutation(result);
    }

    public async Task<NotificationInboxMutationVo> DeleteInboxGroupAsync(
        long tenantId,
        long userId,
        long groupId)
    {
        var result = await _inboxRepository.DeleteGroupAsync(
            tenantId,
            userId,
            groupId,
            GetUtcNow());
        await PushMutationAsync(userId, result, "Deleted");
        return MapMutation(result);
    }

    public async Task<IReadOnlyList<NotificationPreferenceVo>> GetPreferencesAsync(long tenantId, long userId)
    {
        var stored = await _inboxRepository.GetPreferencesAsync(tenantId, userId);
        return BuildPreferenceVos(stored);
    }

    public async Task<IReadOnlyList<NotificationPreferenceVo>> UpdatePreferencesAsync(
        long tenantId,
        long userId,
        UpdateNotificationPreferencesDto dto,
        long operatorId,
        string operatorName)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var duplicatedCategories = dto.Preferences
            .GroupBy(preference => preference.Category?.Trim(), StringComparer.Ordinal)
            .Where(group => string.IsNullOrWhiteSpace(group.Key) || group.Count() > 1)
            .Select(group => group.Key)
            .ToList();
        if (duplicatedCategories.Count > 0)
        {
            throw new ArgumentException("通知偏好分类不能为空或重复。", nameof(dto));
        }

        var nowUtc = GetUtcNow();
        var preferences = dto.Preferences.Select(preference =>
        {
            var category = preference.Category.Trim();
            ValidateCategory(category, allowEmpty: false);
            var definitions = GetActiveDefinitions(category);
            var canDisableInApp = definitions.All(definition => definition.CanDisableInApp);
            var canDisablePreview = definitions.All(definition => definition.CanDisableRealtimePreview);
            return new NotificationSetting
            {
                TenantId = tenantId,
                UserId = userId,
                Category = category,
                InAppEnabled = canDisableInApp ? preference.InAppEnabled : true,
                RealtimePreviewEnabled = canDisablePreview ? preference.RealtimePreviewEnabled : true,
                CreateBy = operatorName,
                CreateId = operatorId,
                ModifyBy = operatorName,
                ModifyId = operatorId,
                CreateTime = nowUtc
            };
        }).ToList();
        var stored = await _inboxRepository.UpsertPreferencesAsync(
            tenantId,
            userId,
            preferences,
            nowUtc);
        return BuildPreferenceVos(stored.ToDictionary(item => item.Category, StringComparer.Ordinal));
    }

    public async Task<(List<UserNotificationVo> notifications, int total)> GetUserNotificationsAsync(
        long tenantId,
        long userId,
        NotificationListQueryDto query)
    {
        var category = ResolveLegacyCategory(query.Type);
        var result = await _inboxRepository.QueryAsync(
            tenantId,
            userId,
            category,
            query.OnlyUnread == true,
            null,
            null,
            Math.Max(0, query.PageIndex - 1) * Math.Clamp(query.PageSize, 1, 50),
            Math.Clamp(query.PageSize, 1, 50));
        var notifications = result.Groups.Select(snapshot => new UserNotificationVo
        {
            VoId = snapshot.Group.Id,
            VoUserId = userId,
            VoNotificationId = snapshot.LatestNotification.Id,
            VoIsRead = snapshot.Group.UnreadOccurrenceCount == 0,
            VoReadAt = snapshot.Group.ReadAtUtc,
            VoCreateTime = snapshot.Group.LastOccurredAtUtc,
            VoNotification = MapLegacyNotification(snapshot.LatestNotification)
        }).ToList();
        return (notifications, checked((int)result.TotalCount));
    }

    public async Task<long> GetUnreadCountAsync(long tenantId, long userId)
    {
        return (await _inboxRepository.GetSummaryAsync(tenantId, userId)).UnreadGroupCount;
    }

    public async Task<int> MarkAsReadAsync(long tenantId, long userId, List<long> notificationIds)
    {
        var groupIds = await _inboxRepository.GetGroupIdsByNotificationIdsAsync(
            tenantId,
            userId,
            notificationIds);
        return (await MarkInboxGroupsAsReadAsync(tenantId, userId, groupIds)).VoAffectedRows;
    }

    public async Task<int> MarkAllAsReadAsync(long tenantId, long userId)
    {
        return (await MarkAllInboxAsReadAsync(tenantId, userId)).VoAffectedRows;
    }

    public async Task<bool> DeleteNotificationAsync(long tenantId, long userId, long notificationId)
    {
        var groupIds = await _inboxRepository.GetGroupIdsByNotificationIdsAsync(
            tenantId,
            userId,
            [notificationId]);
        if (groupIds.Count == 0)
        {
            return false;
        }

        var result = await DeleteInboxGroupAsync(tenantId, userId, groupIds[0]);
        return result.VoAffectedRows > 0;
    }

    public async Task<UnreadCountDto> GetUnreadCountDetailAsync(long tenantId, long userId)
    {
        var summary = await _inboxRepository.GetSummaryAsync(tenantId, userId);
        return new UnreadCountDto
        {
            UserId = userId,
            UnreadCount = summary.UnreadGroupCount,
            UnreadCountByType = summary.UnreadGroupCountByCategory.ToDictionary(item => item.Key, item => item.Value)
        };
    }

    private async Task PushChangedAsync(NotificationInboxRecipientChange change, string reason)
    {
        await _pushService.PushInboxChangedAsync(change.UserId, new NotificationInboxChangedVo
        {
            VoRevision = change.Summary.Revision,
            VoUnreadGroupCount = change.Summary.UnreadGroupCount,
            VoUnreadOccurrenceCount = change.Summary.UnreadOccurrenceCount,
            VoReason = reason,
            VoLatestGroupId = change.GroupId,
            VoRealtimePreviewAllowed = change.RealtimePreviewAllowed
        });
    }

    private async Task PushMutationAsync(
        long userId,
        NotificationInboxMutationResult result,
        string reason)
    {
        if (result.AffectedRows <= 0)
        {
            return;
        }

        await _pushService.PushInboxChangedAsync(userId, new NotificationInboxChangedVo
        {
            VoRevision = result.Summary.Revision,
            VoUnreadGroupCount = result.Summary.UnreadGroupCount,
            VoUnreadOccurrenceCount = result.Summary.UnreadOccurrenceCount,
            VoReason = reason,
            VoRealtimePreviewAllowed = false
        });
    }

    private static NotificationInboxGroupVo MapGroup(NotificationInboxGroupSnapshot snapshot)
    {
        var displayCount = snapshot.LatestNotification.Type is NotificationType.PostLiked or NotificationType.CommentLiked
            ? snapshot.Group.DistinctTriggerCount
            : snapshot.Group.OccurrenceCount;
        var (title, content) = Render(snapshot.LatestNotification, displayCount);
        return new NotificationInboxGroupVo
        {
            VoGroupId = snapshot.Group.Id,
            VoLatestNotificationId = snapshot.LatestNotification.Id,
            VoCategory = snapshot.Group.Category,
            VoKind = snapshot.Group.Kind,
            VoTitle = title,
            VoContent = content,
            VoPriority = snapshot.LatestNotification.Priority,
            VoOccurrenceCount = snapshot.Group.OccurrenceCount,
            VoUnreadOccurrenceCount = snapshot.Group.UnreadOccurrenceCount,
            VoDistinctTriggerCount = snapshot.Group.DistinctTriggerCount,
            VoFirstOccurredAtUtc = snapshot.Group.FirstOccurredAtUtc,
            VoLastOccurredAtUtc = snapshot.Group.LastOccurredAtUtc,
            VoTriggerId = snapshot.LatestNotification.TriggerId?.ToString(CultureInfo.InvariantCulture),
            VoTriggerName = snapshot.LatestNotification.TriggerName,
            VoTriggerAvatar = snapshot.LatestNotification.TriggerAvatar,
            VoTarget = MapTarget(snapshot.LatestNotification)
        };
    }

    private static NotificationVo MapLegacyNotification(Notification notification)
    {
        var (title, content) = Render(notification, 1);
        return new NotificationVo
        {
            VoId = notification.Id,
            VoType = notification.Type,
            VoPriority = notification.Priority,
            VoTitle = title,
            VoContent = content,
            VoBusinessType = notification.BusinessType,
            VoBusinessId = notification.BusinessId,
            VoTriggerId = notification.TriggerId,
            VoTriggerName = notification.TriggerName,
            VoTriggerAvatar = notification.TriggerAvatar,
            VoExtData = notification.ExtData,
            VoCreateTime = notification.OccurredAtUtc
        };
    }

    private static NotificationTargetVo MapTarget(Notification notification)
    {
        try
        {
            var target = NotificationTargetData.FromJson(notification.TargetDataJson);
            return new NotificationTargetVo
            {
                VoKind = notification.TargetKind,
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
        catch (JsonException)
        {
            return new NotificationTargetVo
            {
                VoKind = NotificationTargetKind.None,
                VoUnavailableReason = "Notification.Target.Invalid"
            };
        }
    }

    private static NotificationInboxSummaryVo MapSummary(NotificationInboxSummarySnapshot summary)
    {
        return new NotificationInboxSummaryVo
        {
            VoRevision = summary.Revision,
            VoUnreadGroupCount = summary.UnreadGroupCount,
            VoUnreadOccurrenceCount = summary.UnreadOccurrenceCount,
            VoUnreadGroupCountByCategory = summary.UnreadGroupCountByCategory
                .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal),
            VoLastChangedAtUtc = summary.LastChangedAtUtc
        };
    }

    private static NotificationInboxMutationVo MapMutation(NotificationInboxMutationResult result)
    {
        return new NotificationInboxMutationVo
        {
            VoAffectedRows = result.AffectedRows,
            VoSummary = MapSummary(result.Summary)
        };
    }

    private static IReadOnlyList<NotificationPreferenceVo> BuildPreferenceVos(
        IReadOnlyDictionary<string, NotificationSetting> stored)
    {
        return NotificationDefinitionRegistry.ActiveCategories.Select(category =>
        {
            var definitions = GetActiveDefinitions(category);
            stored.TryGetValue(category, out var preference);
            return new NotificationPreferenceVo
            {
                VoCategory = category,
                VoInAppEnabled = preference?.InAppEnabled ?? true,
                VoRealtimePreviewEnabled = preference?.RealtimePreviewEnabled ?? true,
                VoCanDisableInApp = definitions.All(definition => definition.CanDisableInApp),
                VoCanDisableRealtimePreview = definitions.All(definition => definition.CanDisableRealtimePreview),
                VoSupportedKinds = definitions.Select(definition => definition.Kind).Order().ToList()
            };
        }).ToList();
    }

    private static IReadOnlyList<NotificationDefinition> GetActiveDefinitions(string category)
    {
        var definitions = NotificationDefinitionRegistry.All
            .Where(definition => definition.IsProducerActive && definition.Category == category)
            .ToList();
        if (definitions.Count == 0)
        {
            throw new ArgumentException($"通知分类当前不可配置：{category}", nameof(category));
        }

        return definitions;
    }

    private static void ValidateCategory(string? category, bool allowEmpty)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            if (allowEmpty)
            {
                return;
            }

            throw new ArgumentException("通知分类不能为空。", nameof(category));
        }

        _ = GetActiveDefinitions(category.Trim());
    }

    private static void ValidateTarget(
        NotificationDefinition definition,
        string targetKind,
        NotificationTargetData? target)
    {
        if (!definition.AllowedTargetKinds.Contains(targetKind))
        {
            throw new ArgumentException($"通知 {definition.Kind} 不允许目标类型 {targetKind}。", nameof(targetKind));
        }

        var isValid = targetKind switch
        {
            NotificationTargetKind.None => target == null,
            NotificationTargetKind.ForumPost => target is { PostId: > 0 } ||
                                                !string.IsNullOrWhiteSpace(target?.PostPublicId),
            NotificationTargetKind.ChatConversation => target is { ChannelId: > 0 },
            NotificationTargetKind.UserProfile => target is { UserId: > 0 } ||
                                                  !string.IsNullOrWhiteSpace(target?.UserPublicId),
            NotificationTargetKind.ShopOrder => target is { OrderId: > 0 },
            NotificationTargetKind.Inventory => target is { BenefitId: > 0 },
            NotificationTargetKind.Experience => target is { UserId: > 0 },
            NotificationTargetKind.DocsDocument => !string.IsNullOrWhiteSpace(target?.DocumentSlug),
            NotificationTargetKind.GovernanceCase => target is { GovernanceCaseId: > 0 },
            _ => false
        };
        if (!isValid)
        {
            throw new ArgumentException($"通知 {definition.Kind} 的结构化目标不完整。", nameof(target));
        }
    }

    private static void ValidateTemplateArguments(
        NotificationDefinition definition,
        IReadOnlyDictionary<string, string?> arguments)
    {
        var missingArguments = definition.RequiredTemplateArguments
            .Where(name => !arguments.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value))
            .Order()
            .ToList();
        if (missingArguments.Count > 0)
        {
            throw new ArgumentException(
                $"通知 {definition.Kind} 缺少模板参数：{string.Join(", ", missingArguments)}。",
                nameof(arguments));
        }
    }

    private static string? ResolveLegacyCategory(string? types)
    {
        if (string.IsNullOrWhiteSpace(types))
        {
            return null;
        }

        var categories = types.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(type => NotificationDefinitionRegistry.TryGet(type.Trim(), out var definition)
                ? definition!.Category
                : null)
            .Where(category => category != null)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return categories.Count == 1 ? categories[0] : null;
    }

    private static (string Title, string Content) Render(Notification notification, long occurrenceCount)
    {
        if (notification.TemplateKey == "notification.legacy")
        {
            return (notification.Title, notification.Content);
        }

        Dictionary<string, string?> arguments;
        try
        {
            arguments = JsonSerializer.Deserialize<Dictionary<string, string?>>(
                            notification.TemplateArgumentsJson ?? "{}")
                        ?? new Dictionary<string, string?>(StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return (notification.Title, notification.Content);
        }

        string Arg(string name, string fallback = "") =>
            arguments.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value!
                : fallback;

        var english = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en";
        var actor = Arg("actorName", english ? "Someone" : "有人");
        var target = Arg("targetTitle", english ? "your content" : "你的内容");
        return (notification.Type, english) switch
        {
            (NotificationType.PostLiked, true) => ("Post liked", $"{target} received {occurrenceCount} like(s)."),
            (NotificationType.PostLiked, false) => ("帖子被点赞", $"《{target}》收到了 {occurrenceCount} 个赞"),
            (NotificationType.CommentLiked, true) => ("Comment liked", $"Your comment received {occurrenceCount} like(s)."),
            (NotificationType.CommentLiked, false) => ("评论被点赞", $"你的评论收到了 {occurrenceCount} 个赞"),
            (NotificationType.CommentReplied, true) => ("New reply", $"{actor} replied to your comment."),
            (NotificationType.CommentReplied, false) => ("评论回复", $"{actor} 回复了你的评论"),
            (NotificationType.PostCommented, true) => ("New comment", $"{actor} commented on {target}."),
            (NotificationType.PostCommented, false) => ("帖子被评论", $"{actor} 评论了《{target}》"),
            (NotificationType.PostQuickReplied, true) => ("New quick reply", $"{target} received {occurrenceCount} quick response(s)."),
            (NotificationType.PostQuickReplied, false) => ("收到轻回应", $"《{target}》收到了 {occurrenceCount} 个轻回应"),
            (NotificationType.ChatMentioned, true) => ("Mentioned in chat", $"{actor} mentioned you in {Arg("channelName", "a channel")}."),
            (NotificationType.ChatMentioned, false) => ("聊天室提及", $"{actor} 在频道「{Arg("channelName", "聊天") }」中提到了你"),
            (NotificationType.DirectMessageRequested, true) => ("New message request", $"{actor} sent you a message request."),
            (NotificationType.DirectMessageRequested, false) => ("新的私信请求", $"{actor} 向你发送了私信请求"),
            (NotificationType.Followed, true) => ("New follower", $"{actor} followed you."),
            (NotificationType.Followed, false) => ("新增关注", $"{actor} 关注了你"),
            (NotificationType.PurchaseSucceeded, true) => ("Purchase complete", $"You purchased {Arg("productName", "an item")}."),
            (NotificationType.PurchaseSucceeded, false) => ("购买成功", $"你已成功购买 {Arg("productName", "商品")}"),
            (NotificationType.BenefitExpired, true) => ("Benefit expired", $"Your benefit {Arg("benefitName", "") } has expired."),
            (NotificationType.BenefitExpired, false) => ("权益已到期", $"你的权益“{Arg("benefitName", "") }”已到期"),
            (NotificationType.LevelUp, true) => ("Level up", $"You advanced from Lv.{Arg("oldLevel")} to Lv.{Arg("newLevel")}."),
            (NotificationType.LevelUp, false) => ("等级提升", $"你已从 Lv.{Arg("oldLevel")} 升级到 Lv.{Arg("newLevel")}"),
            (NotificationType.LotteryWon, true) => ("Lottery result", $"You won {Arg("prizeName", "a prize")} in {target}."),
            (NotificationType.LotteryWon, false) => ("抽奖开奖结果", $"你在《{target}》中赢得了“{Arg("prizeName", "奖品") }”"),
            _ => (notification.Title, notification.Content)
        };
    }

    private static string EncodeCursor(InboxCursor cursor)
    {
        return Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(cursor))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static InboxCursor? DecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }

        try
        {
            var normalized = cursor.Replace('-', '+').Replace('_', '/');
            normalized = normalized.PadRight(normalized.Length + ((4 - normalized.Length % 4) % 4), '=');
            return JsonSerializer.Deserialize<InboxCursor>(Convert.FromBase64String(normalized))
                   ?? throw new FormatException();
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            throw new BusinessException(
                "通知分页游标无效",
                StatusCodes.Status400BadRequest,
                "Notification.CursorInvalid",
                "error.notification.cursor_invalid");
        }
    }

    private static BusinessException CursorExpired() => new(
        "通知列表已更新，请刷新第一页",
        StatusCodes.Status409Conflict,
        "Notification.CursorExpired",
        "error.notification.cursor_expired");

    private DateTime GetUtcNow() => _timeProvider.GetUtcNow().UtcDateTime;

    private static DateTime NormalizeUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static string RequireSnapshot(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{fieldName}不能为空。", fieldName);
        }

        return value.Trim();
    }

    private static string? ToId(long? value) => value?.ToString(CultureInfo.InvariantCulture);

    private sealed record InboxCursor(long Revision, DateTime LastOccurredAtUtc, long GroupId);
}
