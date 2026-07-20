using Hangfire;
using Radish.IRepository;

namespace Radish.Api.Services;

/// <summary>按通知定义保留期清理已读或已删除收件箱数据。</summary>
public sealed class NotificationInboxCleanupJob
{
    private readonly INotificationInboxRepository _repository;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<NotificationInboxCleanupJob> _logger;

    public NotificationInboxCleanupJob(
        INotificationInboxRepository repository,
        TimeProvider timeProvider,
        ILogger<NotificationInboxCleanupJob> logger)
    {
        _repository = repository;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 2, DelaysInSeconds = [60, 300])]
    public async Task<NotificationInboxCleanupResult> ExecuteAsync(
        int batchSize = 200,
        int softRelationLimitPerUser = 5000)
    {
        var result = await _repository.CleanupAsync(
            _timeProvider.GetUtcNow().UtcDateTime,
            batchSize,
            softRelationLimitPerUser);

        if (result.DeletedGroupCount > 0 || result.DeletedNotificationCount > 0)
        {
            _logger.LogInformation(
                "[NotificationInboxCleanup] 清理完成：Relations={RelationCount}, Groups={GroupCount}, Notifications={NotificationCount}",
                result.DeletedRelationCount,
                result.DeletedGroupCount,
                result.DeletedNotificationCount);
        }

        foreach (var warning in result.CapacityWarnings)
        {
            _logger.LogWarning(
                "[NotificationInboxCleanup] 用户事件关系超过软上限且无可静默清理项：TenantId={TenantId}, UserId={UserId}, RelationCount={RelationCount}, SoftLimit={SoftLimit}",
                warning.TenantId,
                warning.UserId,
                warning.RelationCount,
                softRelationLimitPerUser);
        }

        return result;
    }
}
