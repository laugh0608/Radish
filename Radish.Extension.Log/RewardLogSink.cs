using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

namespace Radish.Extension.Log;

/// <summary>按实际生成来源重建奖励、币与经验的运行摘要。</summary>
internal sealed class RewardLogSink(ILogEventSink next) : ILogEventSink
{
    private const string Reward = "Radish.Service.RewardRuntimeLog";
    private const string Coin = "Radish.Service.CoinService";
    private const string Idempotency = "Radish.Service.OperationIdempotencyService";
    private const string CoinReward = "Radish.Service.CoinRewardService";
    private const string Experience = "Radish.Service.ExperienceService";
    private const string Calculator = "Radish.Extension.ExperienceExtension.ExperienceCalculator";
    private const string Unknown = "runtime.unclassified";

    private static readonly Dictionary<string, MessageTemplate> Templates = new(StringComparer.Ordinal)
    {
        ["reward.retrying"] = new MessageTemplateParser().Parse("Reward concurrency retry scheduled."),
        ["reward.failed"] = new MessageTemplateParser().Parse("Reward operation consumed a failure."),
        ["reward.conflict"] = new MessageTemplateParser().Parse("Reward key conflict has no existing transaction."),
        ["reward.cache_fallback"] = new MessageTemplateParser().Parse("Reward level configuration cache operation failed."),
        ["reward.batch_completed"] = new MessageTemplateParser().Parse("Reward batch returned results."),
        ["reward.batch_failed"] = new MessageTemplateParser().Parse("Reward batch consumed failures."),
        ["coin.transfer_completion_recovered"] = new MessageTemplateParser().Parse("Transfer completion recording returned after retry."),
        ["idempotency.completion_missing"] = new MessageTemplateParser().Parse("Idempotency completion record is missing."),
        ["reward.existence_check_failed"] = new MessageTemplateParser().Parse("Reward existence check failed; existing assumed."),
        ["experience.rank_query_failed"] = new MessageTemplateParser().Parse("Experience rank query failed; zero returned."),
        ["experience.governance_snapshot_invalid"] = new MessageTemplateParser().Parse("Experience governance snapshot could not be read."),
        ["experience.leaderboard_incomplete"] = new MessageTemplateParser().Parse("Experience leaderboard omitted missing users."),
        ["experience.calculator_cache_fallback"] = new MessageTemplateParser().Parse("Experience calculator cache operation failed."),
        [Unknown] = new MessageTemplateParser().Parse("Unrecognized reward, coin or experience event.")
    };

    public void Emit(LogEvent value)
    {
        if (Scalar(value, "SourceContext") is not { Value: string source } ||
            source is not (Reward or Coin or Idempotency or CoinReward or Experience or Calculator))
        {
            next.Emit(value);
            return;
        }

        var domain = Scalar(value, "rewardDomain");
        var operation = Scalar(value, "rewardOperation");
        var outcome = Scalar(value, "outcome");
        var completion = Scalar(value, "completionKind");
        var code = (source, Scalar(value, "EventCode")?.Value, value.Level) switch
        {
            (Reward, "reward.retrying", LogEventLevel.Warning) when domain?.Value is "coin" or "experience" => "reward.retrying",
            (Reward, "reward.failed", LogEventLevel.Error) when domain?.Value is "experience" &&
                operation?.Value is "grant" or "grant-once" or "initialize" => "reward.failed",
            (Reward, "reward.conflict", LogEventLevel.Warning) when domain?.Value is "experience" &&
                operation?.Value is "grant-once" => "reward.conflict",
            (Reward, "reward.cache_fallback", LogEventLevel.Warning) when domain?.Value is "experience" &&
                operation?.Value is "cache-read" or "cache-write" or "cache-invalidate" => "reward.cache_fallback",
            (Reward, "reward.batch_completed", LogEventLevel.Information) when domain?.Value is "coin" or "experience" &&
                outcome?.Value is "succeeded" or "partial" or "failed" => "reward.batch_completed",
            (Reward, "reward.batch_failed", LogEventLevel.Error) when domain?.Value is "coin" or "experience" &&
                outcome?.Value is "partial" or "failed" => "reward.batch_failed",
            (Coin, "coin.transfer_completion_recovered", LogEventLevel.Warning) => "coin.transfer_completion_recovered",
            (Idempotency, "idempotency.completion_missing", LogEventLevel.Warning) when completion?.Value is "success" or "failure" => "idempotency.completion_missing",
            (CoinReward, "reward.existence_check_failed", LogEventLevel.Error) => "reward.existence_check_failed",
            (Experience, "experience.rank_query_failed", LogEventLevel.Error) => "experience.rank_query_failed",
            (Experience, "experience.governance_snapshot_invalid", LogEventLevel.Warning) => "experience.governance_snapshot_invalid",
            (Experience, "experience.leaderboard_incomplete", LogEventLevel.Warning) => "experience.leaderboard_incomplete",
            (Calculator, "experience.calculator_cache_fallback", LogEventLevel.Warning) when
                operation?.Value is "cache-read" or "cache-write" or "cache-invalidate" => "experience.calculator_cache_fallback",
            _ => Unknown
        };
        var properties = new List<LogEventProperty>
        {
            new("SourceContext", new ScalarValue(source)),
            new("EventCode", new ScalarValue(code)),
            new("SourceCategory", new ScalarValue("application")),
            new("Diagnostic", new ScalarValue(code == Unknown && value.Level < LogEventLevel.Warning))
        };

        if (code != Unknown)
        {
            if (source == Reward) properties.Add(new("rewardDomain", domain!));
            if (code is "reward.failed" or "reward.conflict" or "reward.cache_fallback" or "experience.calculator_cache_fallback")
                properties.Add(new("rewardOperation", operation!));
            if (code == "idempotency.completion_missing") properties.Add(new("completionKind", completion!));
            if (code is "reward.batch_completed" or "reward.batch_failed")
            {
                properties.Add(new("outcome", outcome!));
                foreach (var key in new[] { "processedCount", "rejectedCount", "failedCount" })
                    if (Scalar(value, key) is { Value: int and >= 0 } count) properties.Add(new(key, count));
            }
            if (code == "reward.retrying")
            {
                if (Scalar(value, "attempt") is { Value: int and >= 0 and <= 1_000_000 } attempt)
                    properties.Add(new("attempt", attempt));
                if (Scalar(value, "delayMs") is { Value: int and >= 0 and <= 60_000 } delay)
                    properties.Add(new("delayMs", delay));
            }
            if (code == "experience.leaderboard_incomplete" && Scalar(value, "skippedCount") is { Value: int and >= 0 } skipped)
                properties.Add(new("skippedCount", skipped));
            if (code is "reward.failed" or "reward.conflict" or "reward.cache_fallback" or "reward.batch_failed" or
                "coin.transfer_completion_recovered" or "reward.existence_check_failed" or "experience.rank_query_failed" or
                "experience.governance_snapshot_invalid" or "experience.calculator_cache_fallback" && Scalar(value, "failureKind") is
                { Value: "aggregate" or "database" or "cancelled" or "timeout" or "io" or "access-denied" or "argument" or "invalid-operation" or "other" } failure)
                properties.Add(new("failureKind", failure));
        }

        var activity = Activity.Current is { IdFormat: ActivityIdFormat.W3C } current ? current : null;
        next.Emit(new LogEvent(value.Timestamp, value.Level, null, Templates[code], properties,
            value.TraceId ?? activity?.TraceId ?? default, value.SpanId ?? activity?.SpanId ?? default));
    }

    private static ScalarValue? Scalar(LogEvent value, string key) => value.Properties.TryGetValue(key, out var property)
        ? property as ScalarValue : null;
}
