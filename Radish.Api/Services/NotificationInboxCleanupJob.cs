using Hangfire;
using System.Diagnostics;
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

    [AutomaticRetry(Attempts = 2, DelaysInSeconds = [60, 300], LogEvents = false)]
    public async Task<NotificationInboxCleanupResult> ExecuteAsync(
        int batchSize = 200,
        int softRelationLimitPerUser = 5000)
    {
        var started = Stopwatch.GetTimestamp();
        var result = await _repository.CleanupAsync(
            _timeProvider.GetUtcNow().UtcDateTime,
            batchSize,
            softRelationLimitPerUser);

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["SourceCategory"] = "job", ["jobKind"] = "notification-inbox"
        });
        if (result.DeletedRelationCount > 0 || result.DeletedGroupCount > 0 || result.DeletedNotificationCount > 0)
        {
            using var completed = _logger.BeginScope(new Dictionary<string, object> { ["EventCode"] = "job.cleanup.completed" });
            _logger.LogInformation(
                "Inbox cleanup completed; relations={relationCount}; groups={groupCount}; notifications={notificationCount}; duration={durationMs} ms",
                result.DeletedRelationCount, result.DeletedGroupCount, result.DeletedNotificationCount,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }

        if (result.CapacityWarnings.Count > 0)
        {
            using var warning = _logger.BeginScope(new Dictionary<string, object> { ["EventCode"] = "job.cleanup.capacity_warning" });
            _logger.LogWarning("Inbox capacity needs attention; count={count}", result.CapacityWarnings.Count);
        }

        return result;
    }
}
