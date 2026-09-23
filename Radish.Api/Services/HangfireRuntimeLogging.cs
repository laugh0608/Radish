using System.Security.Cryptography;
using System.Text;
using Hangfire;
using Hangfire.Common;
using Hangfire.Logging;
using Hangfire.States;
using Radish.Common.LogTool;
using Serilog;
using Serilog.Events;

namespace Radish.Api.Services;

/// <summary>在原有重试策略之后观察最终候选状态，不修改状态、次数、延迟或审计。</summary>
public sealed class HangfireRuntimeStateFilter : JobFilterAttribute, IElectStateFilter
{
    public HangfireRuntimeStateFilter() => Order = int.MaxValue;

    public void OnStateElection(ElectStateContext context)
    {
        var failure = context.CandidateState as FailedState
            ?? context.TraversedStates.OfType<FailedState>().LastOrDefault();
        if (failure == null) return; // 普通调度、成功和停机重入队不是任务失败。

        var retrying = context.CandidateState is ScheduledState or EnqueuedState;
        if (!retrying && context.CandidateState is not (FailedState or DeletedState)) return;

        var identity = SHA256.HashData(Encoding.UTF8.GetBytes($"radish:hangfire:{context.BackgroundJob.Id}"));
        var logger = Log.ForContext("SourceCategory", "job")
            .ForContext("EventCode", retrying ? "job.retrying" : "job.failed")
            .ForContext("operationId", new Guid(identity.AsSpan(0, 16)).ToString("D"))
            .ForContext("failureKind", RuntimeFailureSummary.Classify(failure.Exception))
            .ForContext("outcome", retrying ? "retrying" : "failed");
        // 状态选举发生在存储提交前；该事件描述处理决定，不充当已提交状态的审计凭据。
        if (retrying) logger.Warning("Job retry selected");
        else logger.Error("Job failure selected without automatic retry");
    }
}

/// <summary>不求值框架消息工厂，不将 Job 参数、异常或 provider 原文传给旧 / 候选 sink。</summary>
public sealed class HangfireRuntimeLogProvider : ILogProvider
{
    public ILog GetLogger(string name) => new SafeLogger(name == typeof(AutomaticRetryAttribute).FullName);

    private sealed class SafeLogger(bool automaticRetry) : ILog
    {
        public bool Log(Hangfire.Logging.LogLevel level, Func<string>? messageFunc, Exception? exception = null)
        {
            // 此来源由状态观察器接管；其他框架故障仍保留安全摘要。
            if (automaticRetry) return false;
            var mapped = level switch
            {
                Hangfire.Logging.LogLevel.Fatal => LogEventLevel.Fatal,
                Hangfire.Logging.LogLevel.Error => LogEventLevel.Error,
                Hangfire.Logging.LogLevel.Warn => LogEventLevel.Warning,
                _ => LogEventLevel.Debug
            };
            if (!Serilog.Log.IsEnabled(mapped)) return false;
            if (messageFunc == null) return true;
            var code = mapped >= LogEventLevel.Error ? "hangfire.runtime_failed"
                : mapped == LogEventLevel.Warning ? "hangfire.runtime_warning" : "hangfire.diagnostic";
            var logger = Serilog.Log.ForContext("SourceCategory", "job").ForContext("EventCode", code);
            if (exception != null) logger = logger.ForContext("failureKind", RuntimeFailureSummary.Classify(exception));
            logger.Write(mapped, "Hangfire runtime event");
            return true;
        }
    }
}

public static class HangfireRuntimeLogging
{
    public static void Configure(IGlobalConfiguration configuration)
    {
        // 默认过滤器可能早于 provider 构造；显式关闭其原文日志，保留全部重试参数。
        foreach (var retry in GlobalJobFilters.Filters.Select(filter => filter.Instance).OfType<AutomaticRetryAttribute>())
            retry.LogEvents = false;
        if (!GlobalJobFilters.Filters.Any(filter => filter.Instance is HangfireRuntimeStateFilter))
            configuration.UseFilter(new HangfireRuntimeStateFilter());
        configuration.UseLogProvider(new HangfireRuntimeLogProvider());
    }
}
