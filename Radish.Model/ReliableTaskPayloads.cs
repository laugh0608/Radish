using Radish.Model.DtoModels;

namespace Radish.Model;

public static class ReliableTaskTypes
{
    public const string PostPublished = "ForumPostPublishedRewards";
    public const string PostLiked = "ForumPostLikedEffects";
    public const string CommentPublished = "ForumCommentPublishedEffects";
    public const string CommentLiked = "ForumCommentLikedEffects";
    public const string ExperienceLevelChanged = "ExperienceLevelChanged";
    public const string HighlightBaseReward = "CommentHighlightRewardQualified";
    public const string HighlightBonusReward = "CommentHighlightBonusQualified";
    public const string NotificationRequested = "NotificationRequested";
    public const string ChatAttachmentBinding = "ChatAttachmentBinding";
}

public sealed record PostPublishedTaskPayload(long PostId, long AuthorId);

public sealed record LikeEffectsTaskPayload(
    long NotificationId,
    long TargetId,
    long? PostId,
    long AuthorId,
    long LikerId,
    string TargetTitleOrContent,
    string? PostPublicId,
    string RewardDateKey,
    string NotificationWindowKey);

public sealed record CommentPublishedTaskPayload(
    long CommentId,
    long PostId,
    long AuthorId,
    string AuthorName,
    string Content,
    string RewardDateKey,
    long? ParentId,
    long? ReplyTargetCommentId,
    long? ReplyTargetAuthorId,
    long ReplyNotificationId,
    long PostNotificationId);

public sealed record ExperienceLevelChangedTaskPayload(
    long ExpTransactionId,
    long UserId,
    int OldLevel,
    int NewLevel,
    long CoinReward,
    long NotificationId);

public sealed record HighlightBaseRewardTaskPayload(
    long HighlightId,
    long CommentId,
    long AuthorId,
    int HighlightType,
    int LikeCount);

public sealed record HighlightBonusRewardTaskPayload(
    long HighlightId,
    long AuthorId,
    int HighlightType,
    int LikeIncrement,
    int LikeCountAfter);

public sealed record NotificationRequestedTaskPayload(CreateNotificationDto Notification);

public sealed record ChatAttachmentBindingTaskPayload(
    long TenantId,
    long MessageId,
    long AttachmentId,
    long UploaderId,
    string UploaderName);
