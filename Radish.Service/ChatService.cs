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
    private readonly IBaseRepository<User> _userRepository;
    private readonly IChatPresenceService _chatPresenceService;

    public ChatService(
        IMapper mapper,
        IBaseRepository<Channel> baseRepository,
        IBaseRepository<ChannelMessage> messageRepository,
        IBaseRepository<ChannelMember> memberRepository,
        IBaseRepository<User> userRepository,
        IChatPresenceService chatPresenceService)
        : base(mapper, baseRepository)
    {
        _channelRepository = baseRepository;
        _messageRepository = messageRepository;
        _memberRepository = memberRepository;
        _userRepository = userRepository;
        _chatPresenceService = chatPresenceService;
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

        return messages
            .Select(message => MapMessageVo(message, replyMap))
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

        await EnsureMemberAndUpdateReadStateAsync(channel.Id, userId, tenantId, normalizedUserName, messageId);

        var replyMap = new Dictionary<long, ChannelMessage>();
        if (replyToMessage != null)
        {
            replyMap[replyToMessage.Id] = replyToMessage;
        }

        return MapMessageVo(message, replyMap);
    }

    public async Task<long?> RecallMessageAsync(long tenantId, long userId, string userName, long messageId, bool canRecallOthers)
    {
        _ = tenantId;

        if (messageId <= 0)
        {
            return null;
        }

        var message = await _messageRepository.QueryByIdAsync(messageId);
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
        return users
            .OrderBy(u => u.UserName)
            .Select(u => new ChannelMemberVo
            {
                VoUserId = u.Id,
                VoUserName = u.UserName,
                // User 实体当前不包含头像字段，先返回空值，后续可接入用户档案服务补齐。
                VoUserAvatarUrl = null,
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
        return message == null ? null : MapMessageVo(message, null);
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

    private ChannelMessageVo MapMessageVo(ChannelMessage message, Dictionary<long, ChannelMessage>? replyMap)
    {
        var messageVo = Mapper.Map<ChannelMessageVo>(message);
        if (message.ReplyToId.HasValue && replyMap != null && replyMap.TryGetValue(message.ReplyToId.Value, out var replyMessage))
        {
            messageVo.VoReplyTo = Mapper.Map<ChannelMessageVo>(replyMessage);
        }

        messageVo.VoIsRecalled = message.IsDeleted;
        if (messageVo.VoReplyTo != null)
        {
            messageVo.VoReplyTo.VoIsRecalled = messageVo.VoReplyTo.VoIsRecalled || messageVo.VoReplyTo.VoContent == null;
        }

        return messageVo;
    }

}
