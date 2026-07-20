using System.Text.Json;

namespace Radish.Model;

/// <summary>通知目标的结构化持久化载荷。</summary>
public sealed class NotificationTargetData
{
    public long? PostId { get; set; }
    public string? PostPublicId { get; set; }
    public long? CommentId { get; set; }
    public long? ChannelId { get; set; }
    public long? MessageId { get; set; }
    public long? UserId { get; set; }
    public string? UserPublicId { get; set; }
    public long? OrderId { get; set; }
    public long? BenefitId { get; set; }
    public string? DocumentSlug { get; set; }
    public long? DocumentId { get; set; }
    public long? DraftId { get; set; }
    public long? GovernanceCaseId { get; set; }

    public string ToJson() => JsonSerializer.Serialize(this);

    public static NotificationTargetData FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new NotificationTargetData();
        }

        return JsonSerializer.Deserialize<NotificationTargetData>(json)
               ?? new NotificationTargetData();
    }

    public string ToCanonicalIdentity(string targetKind)
    {
        return targetKind switch
        {
            NotificationTargetKind.ForumPost => $"post:{PostId}:{PostPublicId}:comment:{CommentId}",
            NotificationTargetKind.ChatConversation => $"channel:{ChannelId}:message:{MessageId}",
            NotificationTargetKind.UserProfile => $"user:{UserId}:{UserPublicId}",
            NotificationTargetKind.ShopOrder => $"order:{OrderId}",
            NotificationTargetKind.Inventory => $"benefit:{BenefitId}",
            NotificationTargetKind.Experience => $"user:{UserId}",
            NotificationTargetKind.DocsDocument => $"doc:{DocumentSlug}",
            NotificationTargetKind.DocsAuthorDraft => $"doc:{DocumentId}:draft:{DraftId}",
            NotificationTargetKind.GovernanceCase => $"case:{GovernanceCaseId}",
            _ => NotificationTargetKind.None
        };
    }
}
