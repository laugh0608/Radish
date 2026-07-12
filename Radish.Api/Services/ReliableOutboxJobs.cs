using Hangfire;
using Radish.IService;
using Radish.Model;

namespace Radish.Api.Services;

public sealed class ReliableOutboxDispatcherJob
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);
    private readonly IReliableOutboxService _outboxService;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly ILogger<ReliableOutboxDispatcherJob> _logger;

    public ReliableOutboxDispatcherJob(
        IReliableOutboxService outboxService,
        IBackgroundJobClient backgroundJobClient,
        ILogger<ReliableOutboxDispatcherJob> logger)
    {
        _outboxService = outboxService;
        _backgroundJobClient = backgroundJobClient;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 0)]
    public async Task<int> DispatchAsync(int batchSize = 50)
    {
        var workerId = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";
        var dispatchedCount = 0;

        foreach (var sourceDatabase in new[] { ReliableOutboxSources.Main, ReliableOutboxSources.Chat })
        {
            var messages = await _outboxService.ClaimDueAsync(
                sourceDatabase,
                batchSize,
                workerId,
                DateTime.UtcNow,
                LeaseDuration);

            foreach (var message in messages)
            {
                _backgroundJobClient.Enqueue<ReliableOutboxExecutionJob>(
                    job => job.ExecuteAsync(message.SourceDatabase, message.Id, CancellationToken.None));
                dispatchedCount++;
            }
        }

        if (dispatchedCount > 0)
        {
            _logger.LogInformation("[ReliableOutbox] 已分派 {Count} 个任务", dispatchedCount);
        }

        return dispatchedCount;
    }
}

public sealed class ReliableOutboxExecutionJob
{
    private readonly IReliableOutboxService _outboxService;
    private readonly IReliableTaskProcessor _processor;
    private readonly ILogger<ReliableOutboxExecutionJob> _logger;

    public ReliableOutboxExecutionJob(
        IReliableOutboxService outboxService,
        IReliableTaskProcessor processor,
        ILogger<ReliableOutboxExecutionJob> logger)
    {
        _outboxService = outboxService;
        _processor = processor;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 0)]
    public async Task ExecuteAsync(string sourceDatabase, long outboxId, CancellationToken cancellationToken)
    {
        var message = await _outboxService.QueryByIdAsync(sourceDatabase, outboxId);
        if (message == null || message.Status != ReliableOutboxStatuses.Processing)
        {
            return;
        }

        try
        {
            await _processor.ProcessAsync(message, cancellationToken);
            await _outboxService.MarkSucceededAsync(sourceDatabase, outboxId, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            await _outboxService.MarkFailedAsync(sourceDatabase, outboxId, ex, DateTime.UtcNow);
            _logger.LogError(
                ex,
                "[ReliableOutbox] 任务执行失败：Source={Source}, OutboxId={OutboxId}, TaskType={TaskType}",
                sourceDatabase,
                outboxId,
                message.TaskType);
        }
    }
}
