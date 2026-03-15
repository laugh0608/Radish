using System.Text.Json;
using System.Text.RegularExpressions;
using AutoMapper;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Service.Base;
using SqlSugar;

namespace Radish.Service;

/// <summary>聊天室服务实现</summary>
public class ChatService : BaseService<Channel, ChannelVo>, IChatService
{
    private readonly IBaseRepository<Channel> _channelRepository;
    private readonly IBaseRepository<ChannelMessage> _messageRepository;
    private readonly IBaseRepository<ChannelMember> _memberRepository;
    private readonly IBaseRepository<Attachment> _attachmentRepository;
    private readonly IBaseRepository<User> _userRepository;
    private readonly IChatPresenceService _chatPresenceService;
    private readonly INotificationService _notificationService;

    public ChatService(
        IMapper mapper,
        IBaseRepository<Channel> baseRepository,
        IBaseRepository<ChannelMessage> messageRepository,
        IBaseRepository<ChannelMember> memberRepository,
        IBaseRepository<Attachment> attachmentRepository,
        IBaseRepository<User> userRepository,
        IChatPresenceService chatPresenceService,
        INotificationService notificationService)
        : base(mapper, baseRepository)
    {
        _channelRepository = baseRepository;
        _messageRepository = messageRepository;
        _memberRepository = memberRepository;
        _attachmentRepository = attachmentRepository;
        _userRepository = userRepository;
        _chatPresenceService = chatPresenceService;
        _notificationService = notificationService;
    }

    public async Task<List<ChannelVo>> GetChannelListAsync(long tenantId, long userId)
    {
        var channels = await _channelRepository.QueryWithOrderAsync(
            c => c.IsEnabled && !c.IsDeleted,
            c => c.Sort,
            OrderByType.Asc);

        if (channels.Count == 0)
        {
            return new List<ChannelVo>();
        }

        var channelIds = channels.Select(c => c.Id).ToList();
        var members = await _memberRepository.QueryAsync(m => m.UserId == userId && channelIds.Contains(m.ChannelId) && !m.IsDeleted);
        var memberMap = members.ToDictionary(m => m.ChannelId, m => m);

        var result = new List<ChannelVo>(channels.Count);
        foreach (var channel in channels)
        {
            var channelVo = Mapper.Map<ChannelVo>(channel);
            channelVo.VoLastMessage = await GetLastMessagePreviewAsync(channel.Id);

            memberMap.TryGetValue(channel.Id, out var member);
            var unreadState = await GetUnreadStateInternalAsync(channel.Id, userId, member?.LastReadMessageId);
            channelVo.VoUnreadCount = unreadState.VoUnreadCount;
            channelVo.VoHasMention = unreadState.VoHasMention;

            result.Add(channelVo);
        }

        return result;
    }

    public async Task<ChannelVo?> GetChannelDetailAsync(long tenantId, long userId, long channelId)
    {
        var channel = await _channelRepository.QueryFirstAsync(c => c.Id == channelId && c.IsEnabled && !c.IsDeleted);
        if (channel == null)
        {
            return null;
        }

        var channelVo = Mapper.Map<ChannelVo>(channel);
        channelVo.VoLastMessage = await GetLastMessagePreviewAsync(channel.Id);

        var member = await _memberRepository.QueryFirstAsync(m => m.ChannelId == channel.Id && m.UserId == userId && !m.IsDeleted);
        var unreadState = await GetUnreadStateInternalAsync(channel.Id, userId, member?.LastReadMessageId);
        channelVo.VoUnreadCount = unreadState.VoUnreadCount;
        channelVo.VoHasMention = unreadState.VoHasMention;

        return channelVo;
    }

    public async Task<List<ChannelMessageVo>> GetHistoryAsync(long tenantId, long userId, long channelId, long? beforeMessageId, int pageSize = 50)
    {
        _ = tenantId;

        if (channelId <= 0)
        {
            return new List<ChannelMessageVo>();
        }

        var channel = await _channelRepository.QueryFirstAsync(c => c.Id == channelId && c.IsEnabled && !c.IsDeleted);
        if (channel == null)
        {
            return new List<ChannelMessageVo>();
        }

        var safePageSize = Math.Clamp(pageSize, 1, 50);
        var whereExpression = beforeMessageId.HasValue && beforeMessageId.Value > 0
            ? (System.Linq.Expressions.Expression<Func<ChannelMessage, bool>>)(m => m.ChannelId == channelId && m.Id < beforeMessageId.Value)
            : m => m.ChannelId == channelId;

        var (messages, _) = await _messageRepository.QueryPageAsync(
            whereExpression,
            1,
            safePageSize,
            m => m.Id,
            OrderByType.Desc);

        if (messages.Count == 0)
        {
            return new List<ChannelMessageVo>();
        }

        messages = messages
            .OrderBy(m => m.Id)
            .ToList();

        var replyIds = messages
            .Where(m => m.ReplyToId.HasValue)
            .Select(m => m.ReplyToId!.Value)
            .Distinct()
            .ToList();
        var replyMap = replyIds.Count > 0
            ? (await _messageRepository.QueryByIdsAsync(replyIds)).ToDictionary(m => m.Id, m => m)
            : new Dictionary<long, ChannelMessage>();
        var avatarMap = await GetUserAvatarMapAsync(messages.Select(m => m.UserId)
            .Concat(replyMap.Values.Select(m => m.UserId)));

        return messages
            .Select(message => MapMessageVo(message, replyMap, avatarMap))
            .ToList();
    }

    public async Task<ChannelMessageVo> SendMessageAsync(long tenantId, long userId, string userName, string? userAvatarUrl, SendChannelMessageDto request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ChannelId <= 0)
        {
            throw new ArgumentException("频道 Id 无效", nameof(request.ChannelId));
        }

        var channel = await _channelRepository.QueryFirstAsync(c => c.Id == request.ChannelId && c.IsEnabled && !c.IsDeleted);
        if (channel == null)
        {
            throw new InvalidOperationException("频道不存在或不可用");
        }

        var normalizedUserName = string.IsNullOrWhiteSpace(userName) ? "Unknown" : userName.Trim();
        var normalizedContent = request.Content?.Trim();

        if (request.Type == MessageType.Text && string.IsNullOrWhiteSpace(normalizedContent))
        {
            throw new ArgumentException("文本消息内容不能为空", nameof(request.Content));
        }

        if (request.Type == MessageType.Image && string.IsNullOrWhiteSpace(request.ImageUrl))
        {
            throw new ArgumentException("图片消息必须提供图片地址", nameof(request.ImageUrl));
        }

        if (string.IsNullOrWhiteSpace(userAvatarUrl))
        {
            userAvatarUrl = await GetUserAvatarUrlAsync(userId);
        }

        ChannelMessage? replyToMessage = null;
        if (request.ReplyToId.HasValue && request.ReplyToId.Value > 0)
        {
            replyToMessage = await _messageRepository.QueryFirstAsync(m => m.Id == request.ReplyToId.Value && m.ChannelId == request.ChannelId);
            if (replyToMessage == null)
            {
                throw new InvalidOperationException("引用消息不存在");
            }
        }

        var message = new ChannelMessage
        {
            ChannelId = request.ChannelId,
            UserId = userId,
            UserName = normalizedUserName,
            UserAvatarUrl = userAvatarUrl,
            Type = request.Type,
            Content = normalizedContent,
            ReplyToId = request.ReplyToId,
            AttachmentId = request.AttachmentId,
            ImageUrl = request.ImageUrl?.Trim(),
            ImageThumbnailUrl = request.ImageThumbnailUrl?.Trim(),
            TenantId = tenantId,
            CreateTime = DateTime.Now,
            IsDeleted = false
        };

        var messageId = await _messageRepository.AddAsync(message);
        message.Id = messageId;

        channel.LastMessageId = messageId;
        channel.LastMessageTime = message.CreateTime;
        channel.ModifyTime = DateTime.Now;
        channel.ModifyBy = normalizedUserName;
        channel.ModifyId = userId;
        await _channelRepository.UpdateAsync(channel);

        if (request.AttachmentId.HasValue && request.AttachmentId.Value > 0)
        {
            await TryBindMessageAttachmentAsync(request.AttachmentId.Value, messageId, userId, normalizedUserName);
        }

        await EnsureMemberAndUpdateReadStateAsync(channel.Id, userId, tenantId, normalizedUserName, messageId);
        await SendMentionNotificationsAsync(
            tenantId,
            messageId,
            request.ChannelId,
            channel.Name,
            userId,
            normalizedUserName,
            userAvatarUrl,
            normalizedContent);

        var replyMap = new Dictionary<long, ChannelMessage>();
        if (replyToMessage != null)
        {
            replyMap[replyToMessage.Id] = replyToMessage;
        }

        return MapMessageVo(message, replyMap);
    }

    private async Task TryBindMessageAttachmentAsync(long attachmentId, long messageId, long userId, string userName)
    {
        try
        {
            var attachment = await _attachmentRepository.QueryFirstAsync(a => a.Id == attachmentId && !a.IsDeleted);
            if (attachment == null || attachment.UploaderId != userId)
            {
                return;
            }

            attachment.BusinessType = "Chat";
            attachment.BusinessId = messageId;
            attachment.ModifyTime = DateTime.Now;
            attachment.ModifyBy = userName;
            attachment.ModifyId = userId;
            await _attachmentRepository.UpdateAsync(attachment);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[ChatService] 绑定消息附件失败：AttachmentId={AttachmentId}, MessageId={MessageId}", attachmentId, messageId);
        }
    }

    public async Task<long?> RecallMessageAsync(long tenantId, long userId, string userName, long messageId, bool canRecallOthers)
    {
        _ = tenantId;

        if (messageId <= 0)
        {
            return null;
        }

        var message = await _messageRepository.QueryFirstAsync(m => m.Id == messageId);
        if (message == null)
        {
            return null;
        }

        if (message.IsDeleted)
        {
            return message.ChannelId;
        }

        var isOwner = message.UserId == userId;
        if (!isOwner && !canRecallOthers)
        {
            return null;
        }

        if (!canRecallOthers && message.CreateTime < DateTime.Now.AddMinutes(-30))
        {
            return null;
        }

        var normalizedOperator = string.IsNullOrWhiteSpace(userName) ? "System" : userName.Trim();
        var affected = await _messageRepository.UpdateColumnsAsync(
            m => new ChannelMessage
            {
                IsDeleted = true,
                DeletedAt = DateTime.Now,
                DeletedBy = normalizedOperator
            },
            m => m.Id == messageId && !m.IsDeleted);

        return affected > 0 ? message.ChannelId : null;
    }

    public async Task JoinChannelAsync(long tenantId, long userId, long channelId, string operatorName)
    {
        var channel = await _channelRepository.QueryFirstAsync(c => c.Id == channelId && c.IsEnabled && !c.IsDeleted);
        if (channel == null)
        {
            throw new InvalidOperationException("频道不存在或不可用");
        }

        await EnsureMemberAndUpdateReadStateAsync(channelId, userId, tenantId, operatorName, null);
    }

    public Task LeaveChannelAsync(long tenantId, long userId, long channelId)
    {
        _ = tenantId;
        _ = userId;
        _ = channelId;
        return Task.CompletedTask;
    }

    public async Task<ChannelUnreadStateVo> MarkChannelAsReadAsync(long tenantId, long userId, long channelId, string operatorName)
    {
        _ = tenantId;

        var channel = await _channelRepository.QueryFirstAsync(c => c.Id == channelId && c.IsEnabled && !c.IsDeleted);
        if (channel == null)
        {
            return new ChannelUnreadStateVo
            {
                VoChannelId = channelId,
                VoUnreadCount = 0,
                VoHasMention = false
            };
        }

        var (latestMessages, _) = await _messageRepository.QueryPageAsync(
            m => m.ChannelId == channelId && !m.IsDeleted,
            1,
            1,
            m => m.Id,
            OrderByType.Desc);

        var latestMessageId = latestMessages.FirstOrDefault()?.Id;
        await EnsureMemberAndUpdateReadStateAsync(channelId, userId, tenantId, operatorName, latestMessageId);

        return await GetChannelUnreadStateAsync(tenantId, userId, channelId);
    }

    public async Task<ChannelUnreadStateVo> GetChannelUnreadStateAsync(long tenantId, long userId, long channelId)
    {
        _ = tenantId;

        var member = await _memberRepository.QueryFirstAsync(m => m.ChannelId == channelId && m.UserId == userId && !m.IsDeleted);
        return await GetUnreadStateInternalAsync(channelId, userId, member?.LastReadMessageId);
    }

    public async Task<List<long>> GetChannelAudienceUserIdsAsync(long tenantId, long channelId)
    {
        _ = tenantId;

        var userIds = await _memberRepository.QueryDistinctAsync(
            m => m.UserId,
            m => m.ChannelId == channelId && !m.IsDeleted);

        return userIds
            .Where(userId => userId > 0)
            .Distinct()
            .ToList();
    }

    public async Task<List<ChannelMemberVo>> GetOnlineMembersAsync(long tenantId, long channelId)
    {
        var channel = await _channelRepository.QueryFirstAsync(c => c.Id == channelId && c.IsEnabled && !c.IsDeleted);
        if (channel == null)
        {
            return new List<ChannelMemberVo>();
        }

        var onlineUserIds = _chatPresenceService.GetOnlineUserIds(tenantId, channelId);
        if (onlineUserIds.Count == 0)
        {
            return new List<ChannelMemberVo>();
        }

        var users = await _userRepository.QueryAsync(u => onlineUserIds.Contains(u.Id) && !u.IsDeleted);
        var avatarMap = await GetUserAvatarMapAsync(users.Select(u => u.Id));
        return users
            .OrderBy(u => u.UserName)
            .Select(u => new ChannelMemberVo
            {
                VoUserId = u.Id,
                VoUserName = u.UserName,
                VoUserAvatarUrl = avatarMap.TryGetValue(u.Id, out var avatarUrl) ? avatarUrl : null,
                VoIsOnline = true
            })
            .ToList();
    }

    private async Task<ChannelMessageVo?> GetLastMessagePreviewAsync(long channelId)
    {
        var (messages, _) = await _messageRepository.QueryPageAsync(
            m => m.ChannelId == channelId,
            1,
            1,
            m => m.Id,
            OrderByType.Desc);

        var message = messages.FirstOrDefault();
        if (message == null)
        {
            return null;
        }

        var avatarMap = await GetUserAvatarMapAsync(new[] { message.UserId });
        return MapMessageVo(message, null, avatarMap);
    }

    private async Task<ChannelUnreadStateVo> GetUnreadStateInternalAsync(long channelId, long userId, long? lastReadMessageId)
    {
        var hasReadCursor = lastReadMessageId.HasValue && lastReadMessageId.Value > 0;

        var unreadCount = hasReadCursor
            ? await _messageRepository.QueryCountAsync(m => m.ChannelId == channelId && !m.IsDeleted && m.Id > lastReadMessageId!.Value && m.UserId != userId)
            : await _messageRepository.QueryCountAsync(m => m.ChannelId == channelId && !m.IsDeleted && m.UserId != userId);

        var hasMention = false;
        if (unreadCount > 0)
        {
            var mentionToken = $"({userId})";
            hasMention = hasReadCursor
                ? await _messageRepository.QueryExistsAsync(m => m.ChannelId == channelId && !m.IsDeleted && m.Id > lastReadMessageId!.Value && m.UserId != userId && m.Content != null && m.Content.Contains(mentionToken))
                : await _messageRepository.QueryExistsAsync(m => m.ChannelId == channelId && !m.IsDeleted && m.UserId != userId && m.Content != null && m.Content.Contains(mentionToken));
        }

        return new ChannelUnreadStateVo
        {
            VoChannelId = channelId,
            VoUnreadCount = unreadCount,
            VoHasMention = hasMention
        };
    }

    private async Task EnsureMemberAndUpdateReadStateAsync(long channelId, long userId, long tenantId, string operatorName, long? lastReadMessageId)
    {
        var normalizedOperator = string.IsNullOrWhiteSpace(operatorName) ? "System" : operatorName.Trim();

        var member = await _memberRepository.QueryFirstAsync(m => m.ChannelId == channelId && m.UserId == userId);
        if (member == null)
        {
            await _memberRepository.AddAsync(new ChannelMember
            {
                ChannelId = channelId,
                UserId = userId,
                Role = MemberRole.Member,
                LastReadMessageId = lastReadMessageId,
                JoinedAt = DateTime.Now,
                TenantId = tenantId,
                CreateTime = DateTime.Now,
                CreateBy = normalizedOperator,
                CreateId = userId,
                IsDeleted = false
            });

            return;
        }

        if (lastReadMessageId.HasValue)
        {
            member.LastReadMessageId = lastReadMessageId;
        }

        if (member.IsDeleted)
        {
            member.IsDeleted = false;
            member.DeletedAt = null;
            member.DeletedBy = null;
        }

        member.ModifyTime = DateTime.Now;
        member.ModifyBy = normalizedOperator;
        member.ModifyId = userId;

        await _memberRepository.UpdateAsync(member);
    }

    private async Task<Dictionary<long, string>> GetUserAvatarMapAsync(IEnumerable<long> userIds)
    {
        var targetUserIds = userIds
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (targetUserIds.Count == 0)
        {
            return new Dictionary<long, string>();
        }

        var attachments = await _attachmentRepository.QueryAsync(a =>
            a.BusinessType == "Avatar"
            && a.BusinessId.HasValue
            && targetUserIds.Contains(a.BusinessId.Value)
            && a.IsEnabled
            && !a.IsDeleted);

        return attachments
            .Where(a => a.BusinessId.HasValue && !string.IsNullOrWhiteSpace(a.Url))
            .GroupBy(a => a.BusinessId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(a => a.Id)
                    .Select(a => a.Url.Trim())
                    .First());
    }

    private async Task<string?> GetUserAvatarUrlAsync(long userId)
    {
        if (userId <= 0)
        {
            return null;
        }

        var avatarMap = await GetUserAvatarMapAsync(new[] { userId });
        return avatarMap.TryGetValue(userId, out var avatarUrl) ? avatarUrl : null;
    }

    private async Task SendMentionNotificationsAsync(
        long tenantId,
        long messageId,
        long channelId,
        string channelName,
        long senderUserId,
        string senderUserName,
        string? senderAvatarUrl,
        string? content)
    {
        var mentionedUserIds = ParseMentionedUserIds(content)
            .Where(userId => userId > 0 && userId != senderUserId)
            .Distinct()
            .ToList();

        if (mentionedUserIds.Count == 0)
        {
            return;
        }

        var receivers = await _userRepository.QueryAsync(u =>
            mentionedUserIds.Contains(u.Id)
            && u.TenantId == tenantId
            && u.IsEnable
            && !u.IsDeleted);
        var receiverUserIds = receivers
            .Select(u => u.Id)
            .Distinct()
            .ToList();

        if (receiverUserIds.Count == 0)
        {
            return;
        }

        var preview = BuildMentionPreview(content);
        var notificationContent = string.IsNullOrWhiteSpace(preview)
            ? $"{senderUserName} 在频道「{channelName}」中提到了你"
            : $"{senderUserName} 在频道「{channelName}」中提到了你：{preview}";

        await _notificationService.CreateNotificationAsync(new CreateNotificationDto
        {
            Type = NotificationType.Mentioned,
            Title = "聊天室提及",
            Content = notificationContent,
            Priority = (int)NotificationPriority.High,
            BusinessId = messageId,
            TriggerId = senderUserId,
            TriggerName = senderUserName,
            TriggerAvatar = senderAvatarUrl,
            ReceiverUserIds = receiverUserIds,
            TenantId = tenantId,
            ExtData = JsonSerializer.Serialize(new
            {
                channelId,
                messageId
            })
        });
    }

    private static List<long> ParseMentionedUserIds(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return new List<long>();
        }

        return Regex.Matches(content, @"@\[[^\]]+\]\((\d+)\)")
            .Select(match => long.TryParse(match.Groups[1].Value, out var userId) ? userId : 0)
            .Where(userId => userId > 0)
            .Distinct()
            .ToList();
    }

    private static string BuildMentionPreview(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var normalized = Regex.Replace(content, @"@\[(?<name>[^\]]+)\]\((\d+)\)", "@${name}");
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();

        return normalized.Length > 80 ? $"{normalized[..80]}..." : normalized;
    }

    private ChannelMessageVo MapMessageVo(
        ChannelMessage message,
        Dictionary<long, ChannelMessage>? replyMap,
        Dictionary<long, string>? avatarMap = null)
    {
        var messageVo = Mapper.Map<ChannelMessageVo>(message);
        if (string.IsNullOrWhiteSpace(messageVo.VoUserAvatarUrl)
            && avatarMap != null
            && avatarMap.TryGetValue(message.UserId, out var avatarUrl))
        {
            messageVo.VoUserAvatarUrl = avatarUrl;
        }

        if (message.ReplyToId.HasValue && replyMap != null && replyMap.TryGetValue(message.ReplyToId.Value, out var replyMessage))
        {
            messageVo.VoReplyTo = Mapper.Map<ChannelMessageVo>(replyMessage);
            if (string.IsNullOrWhiteSpace(messageVo.VoReplyTo.VoUserAvatarUrl)
                && avatarMap != null
                && avatarMap.TryGetValue(replyMessage.UserId, out var replyAvatarUrl))
            {
                messageVo.VoReplyTo.VoUserAvatarUrl = replyAvatarUrl;
            }
        }

        messageVo.VoIsRecalled = message.IsDeleted;
        if (messageVo.VoReplyTo != null)
        {
            messageVo.VoReplyTo.VoIsRecalled = messageVo.VoReplyTo.VoIsRecalled || messageVo.VoReplyTo.VoContent == null;
        }

        return messageVo;
    }

}
