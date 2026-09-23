using Hangfire;
using System.Diagnostics;
using System.Text.Json;
using Radish.IRepository;
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
        var started = Stopwatch.GetTimestamp();
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
            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["EventCode"] = "outbox.dispatched", ["SourceCategory"] = "job"
            });
            _logger.LogInformation("Outbox dispatch completed; count={count}; duration={durationMs} ms",
                dispatchedCount, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }

        return dispatchedCount;
    }
}

public sealed class ReliableOutboxExecutionJob
{
    private readonly IReliableOutboxService _outboxService;
    private readonly IReliableTaskProcessor _processor;
    private readonly IContentModerationCaseRepository _contentModerationCaseRepository;

    public ReliableOutboxExecutionJob(
        IReliableOutboxService outboxService,
        IReliableTaskProcessor processor,
        IContentModerationCaseRepository contentModerationCaseRepository)
    {
        _outboxService = outboxService;
        _processor = processor;
        _contentModerationCaseRepository = contentModerationCaseRepository;
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
            await RecordContentModerationFailureAsync(message, ex);
            // 重试 / 死信由实际完成状态写入的 Repository 记录；这里不重复输出异常载荷。
        }
    }

    private async Task RecordContentModerationFailureAsync(
        ReliableOutboxSnapshot message,
        Exception exception)
    {
        if (message.TaskType is not
            (ReliableTaskTypes.ContentModerationChatRecall or ReliableTaskTypes.ContentModerationChatRestore))
        {
            return;
        }

        if (message.TaskType == ReliableTaskTypes.ContentModerationChatRecall)
        {
            var payload = JsonSerializer.Deserialize<ContentModerationChatRecallTaskPayload>(message.PayloadJson)
                ?? throw new JsonException("内容治理 Chat 回收任务载荷为空");
            await _contentModerationCaseRepository.CompleteChatTargetActionAsync(
                new ContentModerationChatActionCompletionCommand(
                    payload.TenantId,
                    payload.CaseId,
                    payload.TargetActionId,
                    payload.OperationKey,
                    false,
                    exception.GetType().Name,
                    payload.OperatorUserId,
                    payload.OperatorName,
                    DateTime.UtcNow));
            return;
        }

        var restorePayload = JsonSerializer.Deserialize<ContentModerationChatRestoreTaskPayload>(
            message.PayloadJson) ?? throw new JsonException("内容治理 Chat 恢复任务载荷为空");
        await _contentModerationCaseRepository.CompleteChatReliefAsync(
            new ContentModerationChatReliefCompletionCommand(
                restorePayload.TenantId,
                restorePayload.AppealId,
                restorePayload.TargetActionId,
                restorePayload.OperationKey,
                false,
                exception.GetType().Name,
                restorePayload.OperatorUserId,
                restorePayload.OperatorName,
                DateTime.UtcNow));
    }
}
