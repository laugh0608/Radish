using System.Text.Json;
using Radish.Common;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Shared.CustomEnum;

namespace Radish.Service;

public sealed class ReliableTaskProcessor : IReliableTaskProcessor
{
    private readonly ICoinRewardService _coinRewardService;
    private readonly ICoinService _coinService;
    private readonly IExperienceService _experienceService;
    private readonly INotificationService _notificationService;
    private readonly IChatAttachmentBindingService _chatAttachmentBindingService;
    private readonly IBaseRepository<Post> _postRepository;
    private readonly IBaseRepository<Comment> _commentRepository;

    public ReliableTaskProcessor(
        ICoinRewardService coinRewardService,
        ICoinService coinService,
        IExperienceService experienceService,
        INotificationService notificationService,
        IChatAttachmentBindingService chatAttachmentBindingService,
        IBaseRepository<Post> postRepository,
        IBaseRepository<Comment> commentRepository)
    {
        _coinRewardService = coinRewardService;
        _coinService = coinService;
        _experienceService = experienceService;
        _notificationService = notificationService;
        _chatAttachmentBindingService = chatAttachmentBindingService;
        _postRepository = postRepository;
        _commentRepository = commentRepository;
    }

    public async Task ProcessAsync(ReliableOutboxSnapshot message, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (message.SchemaVersion != 1)
        {
            throw new PermanentReliableTaskException(
                $"不支持的可靠任务载荷版本：TaskType={message.TaskType}, Version={message.SchemaVersion}");
        }

        switch (message.TaskType)
        {
            case ReliableTaskTypes.PostPublished:
                await ProcessPostPublishedAsync(Deserialize<PostPublishedTaskPayload>(message));
                break;
            case ReliableTaskTypes.PostLiked:
                await ProcessLikeAsync(Deserialize<LikeEffectsTaskPayload>(message), isPost: true);
                break;
            case ReliableTaskTypes.CommentPublished:
                await ProcessCommentPublishedAsync(Deserialize<CommentPublishedTaskPayload>(message));
                break;
            case ReliableTaskTypes.CommentLiked:
                await ProcessLikeAsync(Deserialize<LikeEffectsTaskPayload>(message), isPost: false);
                break;
            case ReliableTaskTypes.ExperienceLevelChanged:
                await ProcessLevelChangedAsync(Deserialize<ExperienceLevelChangedTaskPayload>(message));
                break;
            case ReliableTaskTypes.HighlightBaseReward:
                await ProcessHighlightBaseRewardAsync(Deserialize<HighlightBaseRewardTaskPayload>(message));
                break;
            case ReliableTaskTypes.HighlightBonusReward:
                await ProcessHighlightBonusRewardAsync(Deserialize<HighlightBonusRewardTaskPayload>(message));
                break;
            case ReliableTaskTypes.NotificationRequested:
                await _notificationService.CreateNotificationAsync(
                    Deserialize<NotificationRequestedTaskPayload>(message).Notification);
                break;
            case ReliableTaskTypes.ChatAttachmentBinding:
                await _chatAttachmentBindingService.BindAsync(
                    Deserialize<ChatAttachmentBindingTaskPayload>(message));
                break;
            default:
                throw new PermanentReliableTaskException($"未知可靠任务类型：{message.TaskType}");
        }
    }

    private async Task ProcessPostPublishedAsync(PostPublishedTaskPayload payload)
    {
        await _experienceService.GrantExperienceOnceAsync(
            payload.AuthorId,
            20,
            "POST_CREATE",
            $"exp:post-create:author:{payload.AuthorId}:post:{payload.PostId}",
            "Post",
            payload.PostId,
            "发布帖子");

        var posts = await _postRepository.QueryAsync(post => post.AuthorId == payload.AuthorId && !post.IsDeleted);
        var firstPostId = posts.OrderBy(post => post.CreateTime).ThenBy(post => post.Id).FirstOrDefault()?.Id;
        if (firstPostId == payload.PostId)
        {
            await _experienceService.GrantExperienceOnceAsync(
                payload.AuthorId,
                30,
                "FIRST_POST",
                $"exp:first-post:user:{payload.AuthorId}",
                "Post",
                payload.PostId,
                "首次发帖奖励");
        }
    }

    private async Task ProcessLikeAsync(LikeEffectsTaskPayload payload, bool isPost)
    {
        if (!DateTime.TryParseExact(
                payload.RewardDateKey,
                "yyyyMMdd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var rewardDate))
        {
            throw new PermanentReliableTaskException($"奖励日期格式无效：{payload.RewardDateKey}");
        }

        if (isPost)
        {
            await _coinRewardService.GrantLikeRewardAsync(payload.TargetId, payload.AuthorId, payload.LikerId, rewardDate);
        }
        else
        {
            await _coinRewardService.GrantCommentLikeRewardAsync(payload.TargetId, payload.AuthorId, payload.LikerId, rewardDate);
        }

        var businessType = isPost ? "Post" : "Comment";
        var targetKey = isPost ? "post" : "comment";
        await _experienceService.GrantExperienceOnceAsync(
            payload.AuthorId,
            2,
            "RECEIVE_LIKE",
            $"exp:receive-like:{targetKey}:user:{payload.AuthorId}:target:{payload.TargetId}:day:{payload.RewardDateKey}",
            businessType,
            payload.TargetId,
            isPost ? "帖子被点赞" : "评论被点赞");
        await _experienceService.GrantExperienceOnceAsync(
            payload.LikerId,
            1,
            "GIVE_LIKE",
            $"exp:give-like:{targetKey}:user:{payload.LikerId}:target:{payload.TargetId}:day:{payload.RewardDateKey}",
            businessType,
            payload.TargetId,
            isPost ? "点赞帖子" : "点赞评论");

        if (payload.AuthorId == payload.LikerId)
        {
            return;
        }

        var postPublicId = payload.PostPublicId;
        if (!isPost && string.IsNullOrWhiteSpace(postPublicId) && payload.PostId.HasValue)
        {
            postPublicId = (await _postRepository.QueryByIdAsync(payload.PostId.Value))?.PublicId;
        }

        await _notificationService.CreateNotificationAsync(new CreateNotificationDto
        {
            NotificationId = payload.NotificationId,
            BusinessKey = payload.NotificationWindowKey,
            Type = isPost ? NotificationType.PostLiked : NotificationType.CommentLiked,
            Title = isPost ? "帖子被点赞" : "评论被点赞",
            Content = isPost ? $"你的帖子《{payload.TargetTitleOrContent}》收到了一个赞" : payload.TargetTitleOrContent,
            Priority = (int)NotificationPriority.Low,
            BusinessType = businessType,
            BusinessId = payload.TargetId,
            TriggerId = payload.LikerId,
            ReceiverUserIds = [payload.AuthorId],
            ExtData = NotificationNavigationHelper.BuildForumNavigationExtData(
                payload.PostId ?? payload.TargetId,
                isPost ? null : payload.TargetId,
                postPublicId)
        });
    }

    private async Task ProcessCommentPublishedAsync(CommentPublishedTaskPayload payload)
    {
        if (!DateTime.TryParseExact(
                payload.RewardDateKey,
                "yyyyMMdd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var rewardDate))
        {
            throw new PermanentReliableTaskException($"奖励日期格式无效：{payload.RewardDateKey}");
        }

        await _coinRewardService.GrantCommentRewardAsync(payload.CommentId, payload.AuthorId, payload.PostId);
        await _experienceService.GrantExperienceOnceAsync(
            payload.AuthorId,
            5,
            "COMMENT_CREATE",
            $"exp:comment-create:author:{payload.AuthorId}:comment:{payload.CommentId}",
            "Comment",
            payload.CommentId,
            "发布评论");

        var comments = await _commentRepository.QueryAsync(comment => comment.AuthorId == payload.AuthorId && !comment.IsDeleted);
        var firstCommentId = comments.OrderBy(comment => comment.CreateTime).ThenBy(comment => comment.Id).FirstOrDefault()?.Id;
        if (firstCommentId == payload.CommentId)
        {
            await _experienceService.GrantExperienceOnceAsync(
                payload.AuthorId,
                10,
                "FIRST_COMMENT",
                $"exp:first-comment:user:{payload.AuthorId}",
                "Comment",
                payload.CommentId,
                "首次评论奖励");
        }

        var post = await _postRepository.QueryByIdAsync(payload.PostId);
        if (payload.ReplyTargetCommentId.HasValue && payload.ReplyTargetAuthorId.HasValue)
        {
            await _coinRewardService.GrantCommentReplyRewardAsync(
                payload.ReplyTargetCommentId.Value,
                payload.ReplyTargetAuthorId.Value,
                payload.CommentId,
                rewardDate);
            if (payload.ReplyTargetAuthorId.Value != payload.AuthorId)
            {
                await _notificationService.CreateNotificationAsync(BuildCommentNotification(
                    payload.ReplyNotificationId,
                    $"notification:comment-reply:{payload.CommentId}:receiver:{payload.ReplyTargetAuthorId.Value}",
                    "评论回复",
                    BusinessType.Comment,
                    payload.CommentId,
                    payload.ReplyTargetAuthorId.Value,
                    payload,
                    post?.PublicId));
            }
        }
        else if (post != null && post.AuthorId != payload.AuthorId)
        {
            await _notificationService.CreateNotificationAsync(BuildCommentNotification(
                payload.PostNotificationId,
                $"notification:post-comment:{payload.CommentId}:receiver:{post.AuthorId}",
                "帖子被评论",
                BusinessType.Post,
                payload.PostId,
                post.AuthorId,
                payload,
                post.PublicId));
        }
    }

    private async Task ProcessLevelChangedAsync(ExperienceLevelChangedTaskPayload payload)
    {
        if (payload.CoinReward > 0)
        {
            await _coinService.GrantCoinOnceAsync(
                payload.UserId,
                payload.CoinReward,
                "LEVEL_UP_REWARD",
                $"coin:level-up:exp-transaction:{payload.ExpTransactionId}",
                "User",
                payload.UserId,
                $"升级到 Lv.{payload.NewLevel}");
        }

        await _notificationService.CreateNotificationAsync(new CreateNotificationDto
        {
            NotificationId = payload.NotificationId,
            BusinessKey = $"notification:level-up:exp-transaction:{payload.ExpTransactionId}",
            Type = NotificationType.LevelUp,
            Title = "等级提升",
            Content = $"恭喜你从 Lv.{payload.OldLevel} 升级到 Lv.{payload.NewLevel}！获得 {payload.CoinReward} 个萝卜币奖励！",
            Priority = (int)NotificationPriority.High,
            BusinessType = BusinessType.User,
            BusinessId = payload.UserId,
            TriggerName = "系统",
            ReceiverUserIds = [payload.UserId]
        });
    }

    private async Task ProcessHighlightBaseRewardAsync(HighlightBaseRewardTaskPayload payload)
    {
        if (payload.HighlightType == 1)
        {
            await _coinRewardService.GrantGodCommentRewardAsync(payload.CommentId, payload.AuthorId, payload.LikeCount);
        }
        else
        {
            await _coinRewardService.GrantSofaRewardAsync(payload.CommentId, payload.AuthorId, payload.LikeCount);
        }

        await _experienceService.GrantExperienceOnceAsync(
            payload.AuthorId,
            payload.HighlightType == 1 ? 50 : 30,
            payload.HighlightType == 1 ? "GOD_COMMENT" : "SOFA_COMMENT",
            payload.HighlightType == 1
                ? $"exp:highlight-base:god-comment:author:{payload.AuthorId}:comment:{payload.CommentId}"
                : $"exp:highlight-base:sofa:author:{payload.AuthorId}:comment:{payload.CommentId}",
            "Comment",
            payload.CommentId,
            payload.HighlightType == 1 ? "评论成为神评" : "评论成为沙发");
    }

    private async Task ProcessHighlightBonusRewardAsync(HighlightBonusRewardTaskPayload payload)
    {
        await _coinRewardService.GrantLikeBonusRewardAsync(
            payload.HighlightId,
            payload.AuthorId,
            payload.LikeIncrement,
            payload.HighlightType == 1 ? "GodComment" : "Sofa",
            payload.LikeCountAfter);
    }

    private static CreateNotificationDto BuildCommentNotification(
        long notificationId,
        string businessKey,
        string title,
        string businessType,
        long businessId,
        long receiverId,
        CommentPublishedTaskPayload payload,
        string? postPublicId)
    {
        return new CreateNotificationDto
        {
            NotificationId = notificationId,
            BusinessKey = businessKey,
            Type = NotificationType.CommentReplied,
            Title = title,
            Content = payload.Content,
            Priority = (int)NotificationPriority.Normal,
            BusinessType = businessType,
            BusinessId = businessId,
            TriggerId = payload.AuthorId,
            TriggerName = payload.AuthorName,
            ReceiverUserIds = [receiverId],
            ExtData = NotificationNavigationHelper.BuildForumNavigationExtData(
                payload.PostId,
                payload.CommentId,
                postPublicId)
        };
    }

    private static TPayload Deserialize<TPayload>(ReliableOutboxSnapshot message)
    {
        return JsonSerializer.Deserialize<TPayload>(message.PayloadJson)
            ?? throw new PermanentReliableTaskException($"可靠任务载荷为空：{message.TaskType}");
    }
}
