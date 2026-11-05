using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

namespace Radish.Extension.Log;

/// <summary>重建当前用户、客户端管理、引导奖励、通知去重、限流及直接 API 异常消费边界的精确自有来源。</summary>
public sealed class ApplicationOperationLogSink(ILogEventSink next, bool diagnostics = false) : ILogEventSink
{
    private static readonly MessageTemplate EventTemplate = new MessageTemplateParser().Parse("Application operation {EventCode}.");
    private static readonly MessageTemplate FailureTemplate = new MessageTemplateParser().Parse("Application operation {EventCode}; failure={failureKind}.");
    private static readonly MessageTemplate HttpFailureTemplate = new MessageTemplateParser().Parse("API request failure {EventCode}; status={statusCode}; failure={failureKind}.");
    private static readonly MessageTemplate ConfigurationTemplate = new MessageTemplateParser().Parse("Application operation {EventCode}; state={rateLimitState}.");

    public void Emit(LogEvent value)
    {
        if (!value.Properties.TryGetValue("SourceContext", out var source) || source is not ScalarValue { Value: string category })
        { next.Emit(value); return; }
        var owner = category switch
        {
            "Radish.Api.ErrorHandling.ApiExceptionHandler" => "http.handler",
            "Radish.Api.Controllers.UserController" => "user.current",
            "Radish.Api.Controllers.ClientController" => "client.management",
            "Radish.Service.BootstrapService" => "bootstrap",
            "Radish.Service.NotificationDedupService" => "notification.dedup",
            "Radish.Extension.RateLimitExtension.RateLimitSetup" => "rate_limit",
            _ => null
        };
        if (owner == null) { next.Emit(value); return; }

        var rawCode = value.Properties.TryGetValue("EventCode", out var raw) && raw is ScalarValue { Value: string text } ? text : null;
        var code = owner switch
        {
            "user.current" when rawCode is "user.current_permissions_degraded" or "user.current_avatar_degraded" => rawCode,
            "bootstrap" when rawCode == "bootstrap.reward_degraded" => rawCode,
            "rate_limit" when rawCode is "rate_limit.configured" or "rate_limit.whitelist_observed" or
                "rate_limit.blacklist_observed" or "rate_limit.rejected" => rawCode,
            "http.handler" => Identity(value) == (1, "ApiRequestFailed") ? "http.failed" : "http.handler.unknown",
            "client.management" => Identity(value) switch
            {
                (1, "ClientListFailed") => "client.management.list_failed",
                (2, "ClientDetailFailed") => "client.management.detail_failed",
                (3, "ClientCreated") => "client.management.created",
                (4, "ClientUpdated") => "client.management.updated",
                (5, "ClientSoftDeleted") => "client.management.soft_deleted",
                (6, "ClientSecretReset") => "client.management.secret_reset",
                _ => "client.management.unknown"
            },
            "notification.dedup" => Identity(value) switch
            {
                (1, "DedupCheckFailed") => "notification.dedup.check_failed",
                (2, "DedupRecordFailed") => "notification.dedup.record_failed",
                (3, "DedupClearFailed") => "notification.dedup.clear_failed",
                _ => "notification.dedup.unknown"
            },
            _ => owner + ".unknown"
        };
        var diagnostic = code is "rate_limit.whitelist_observed" or "rate_limit.blacklist_observed" or "rate_limit.rejected" ||
            code.EndsWith(".unknown", StringComparison.Ordinal) && value.Level < LogEventLevel.Warning;
        // 旧链也执行诊断门禁；任意 scope 中的 Diagnostic 不能关闭或打开此判断。
        if (diagnostic && !diagnostics) return;
        var properties = new List<LogEventProperty>
        {
            new("SourceContext", new ScalarValue(category)), new("EventCode", new ScalarValue(code)),
            new("SourceCategory", new ScalarValue(owner is "rate_limit" or "http.handler" ? "http" : "application")),
            new("Diagnostic", new ScalarValue(diagnostic))
        };
        var template = EventTemplate;
        if (code is "http.failed" or "user.current_permissions_degraded" or "user.current_avatar_degraded" or "bootstrap.reward_degraded" or
            "client.management.list_failed" or "client.management.detail_failed" or "notification.dedup.check_failed" or
            "notification.dedup.record_failed" or "notification.dedup.clear_failed")
        {
            template = FailureTemplate;
            if (value.Properties.TryGetValue("failureKind", out var failure) && failure is ScalarValue
                { Value: "aggregate" or "database" or "cancelled" or "timeout" or "io" or "access-denied" or "argument" or "invalid-operation" or "other" })
                properties.Add(new("failureKind", failure));
        }
        if (code == "http.failed")
        {
            template = HttpFailureTemplate;
            if (value.Properties.TryGetValue("statusCode", out var status) && status is ScalarValue { Value: int number } && number is >= 500 and <= 599)
                properties.Add(new("statusCode", status));
        }
        if (code == "rate_limit.configured")
        {
            template = ConfigurationTemplate;
            if (value.Properties.TryGetValue("rateLimitState", out var state) && state is ScalarValue { Value: "enabled" or "disabled" })
                properties.Add(new("rateLimitState", state));
        }
        next.Emit(new LogEvent(value.Timestamp, value.Level, null, template, properties,
            value.TraceId ?? Activity.Current?.TraceId ?? default, value.SpanId ?? Activity.Current?.SpanId ?? default));
    }

    private static (int?, string?) Identity(LogEvent value)
    {
        if (!value.Properties.TryGetValue("EventId", out var identity) || identity is not StructureValue details)
            return (null, null);
        var ids = details.Properties.Where(p => p.Name == "Id").ToArray();
        var names = details.Properties.Where(p => p.Name == "Name").ToArray();
        return ids.Length == 1 && ids[0].Value is ScalarValue { Value: int id } &&
            names.Length == 1 && names[0].Value is ScalarValue { Value: string name } ? (id, name) : (null, null);
    }
}
