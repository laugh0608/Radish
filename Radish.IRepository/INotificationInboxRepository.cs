using Radish.Model;

namespace Radish.IRepository;

public sealed record NotificationInboxRecipient(long UserId, bool RealtimePreviewAllowed);

public sealed record NotificationInboxSummarySnapshot(
    long Revision,
    long UnreadGroupCount,
    long UnreadOccurrenceCount,
    IReadOnlyDictionary<string, long> UnreadGroupCountByCategory,
    DateTime LastChangedAtUtc);

public sealed record NotificationInboxRecipientChange(
    long UserId,
    long GroupId,
    bool RealtimePreviewAllowed,
    NotificationInboxSummarySnapshot Summary);

public sealed record NotificationInboxPersistResult(
    long NotificationId,
    bool EventCreated,
    IReadOnlyList<NotificationInboxRecipientChange> RecipientChanges);

public sealed record NotificationInboxGroupSnapshot(
    NotificationInboxGroup Group,
    Notification LatestNotification);

public sealed record NotificationInboxQueryResult(
    IReadOnlyList<NotificationInboxGroupSnapshot> Groups,
    bool HasMore,
    long TotalCount,
    NotificationInboxSummarySnapshot Summary);

public sealed record NotificationInboxMutationResult(
    int AffectedRows,
    NotificationInboxSummarySnapshot Summary);

public sealed record NotificationInboxCapacityWarning(
    long TenantId,
    long UserId,
    long RelationCount);

public sealed record NotificationInboxCleanupResult(
    int DeletedRelationCount,
    int DeletedGroupCount,
    int DeletedNotificationCount,
    IReadOnlyList<NotificationInboxCapacityWarning> CapacityWarnings);

public interface INotificationInboxRepository
{
    Task<IReadOnlyDictionary<string, NotificationSetting>> GetPreferencesAsync(long tenantId, long userId);

    Task<NotificationInboxPersistResult> PersistAsync(
        Notification notification,
        IReadOnlyList<NotificationInboxRecipient> recipients,
        DateTime nowUtc);

    Task<NotificationInboxSummarySnapshot> GetSummaryAsync(long tenantId, long userId);

    Task<IReadOnlyList<long>> GetGroupIdsByNotificationIdsAsync(
        long tenantId,
        long userId,
        IReadOnlyCollection<long> notificationIds);

    Task<NotificationInboxQueryResult> QueryAsync(
        long tenantId,
        long userId,
        string? category,
        bool onlyUnread,
        DateTime? beforeOccurredAtUtc,
        long? beforeGroupId,
        int skip,
        int take);

    Task<NotificationInboxMutationResult> MarkGroupsAsReadAsync(
        long tenantId,
        long userId,
        IReadOnlyCollection<long> groupIds,
        DateTime nowUtc);

    Task<NotificationInboxMutationResult> MarkAllAsReadAsync(
        long tenantId,
        long userId,
        string? category,
        DateTime nowUtc);

    Task<NotificationInboxMutationResult> DeleteGroupAsync(
        long tenantId,
        long userId,
        long groupId,
        DateTime nowUtc);

    Task<IReadOnlyList<NotificationSetting>> UpsertPreferencesAsync(
        long tenantId,
        long userId,
        IReadOnlyList<NotificationSetting> preferences,
        DateTime nowUtc);

    Task<NotificationInboxCleanupResult> CleanupAsync(
        DateTime nowUtc,
        int batchSize,
        int softRelationLimitPerUser);
}
