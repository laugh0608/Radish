using System.Diagnostics;
using Radish.Common.LogTool;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

namespace Radish.Extension.Log;

/// <summary>在上下文合并后重建四个认证 / 授权框架来源与 API 两处 JWT 观察事件。</summary>
public sealed class AuthenticationLogSink(ILogEventSink next) : ILogEventSink
{
    private static readonly MessageTemplate Template = new MessageTemplateParser().Parse("Authentication event {EventCode}.");
    private static readonly MessageTemplate FailureTemplate = new MessageTemplateParser().Parse("Authentication event {EventCode}; kind={failureKind}.");

    public void Emit(LogEvent logEvent)
    {
        if (!logEvent.Properties.TryGetValue("SourceContext", out var source) || source is not ScalarValue { Value: string category })
        {
            next.Emit(logEvent);
            return;
        }

        var owner = category switch
        {
            "Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerHandler" => "auth.jwt",
            "Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationHandler" => "auth.cookie",
            "Microsoft.AspNetCore.Authorization.DefaultAuthorizationService" => "auth.authorization",
            "Microsoft.AspNetCore.Authorization.AuthorizationMiddleware" => "auth.authorization_middleware",
            _ => null
        };
        string code;
        string? failureKind = null;
        if (owner != null)
        {
            var (id, name) = Identity(logEvent);
            code = (owner, id, name) switch
            {
                ("auth.jwt" or "auth.cookie", 7, "AuthenticationSchemeNotAuthenticatedWithFailure") => owner + "_authentication_failed",
                ("auth.jwt" or "auth.cookie", 8, "AuthenticationSchemeAuthenticated") => owner + "_authenticated",
                ("auth.jwt" or "auth.cookie", 9, "AuthenticationSchemeNotAuthenticated") => owner + "_not_authenticated",
                ("auth.jwt" or "auth.cookie", 12, "AuthenticationSchemeChallenged") => owner + "_challenged",
                ("auth.jwt" or "auth.cookie", 13, "AuthenticationSchemeForbidden") => owner + "_forbidden",
                ("auth.jwt", 1, "TokenValidationFailed") => "auth.jwt_validation_failed",
                ("auth.jwt", 2, "TokenValidationSucceeded") => "auth.jwt_validation_succeeded",
                ("auth.jwt", 3, "ProcessingMessageFailed") => "auth.jwt_processing_failed",
                ("auth.jwt", 4, "ForbiddenResponseHasStarted") => "auth.jwt_forbidden_response_started",
                ("auth.cookie", 10, "AuthenticationSchemeSignedIn") => "auth.cookie_signed_in",
                ("auth.cookie", 11, "AuthenticationSchemeSignedOut") => "auth.cookie_signed_out",
                ("auth.authorization", 1, "UserAuthorizationSucceeded") => "auth.authorization_succeeded",
                ("auth.authorization", 2, "UserAuthorizationFailed") => "auth.authorization_failed",
                // AuthorizationMiddleware 的直接 LogDebug 没有命名身份，不依赖模板猜测。
                _ => owner + "_unknown"
            };
            if (logEvent.Exception != null) failureKind = RuntimeFailureSummary.Classify(logEvent.Exception);
        }
        else if (category == "Radish.Api.Security.ApiJwtBearerEvents" &&
                 logEvent.Properties.TryGetValue("EventCode", out var callbackCode) &&
                 callbackCode is ScalarValue { Value: "auth.jwt_failure_observed" or "auth.jwt_challenge_observed" })
        {
            code = (string)((ScalarValue)callbackCode).Value!;
            // 自有回调在生成端完成异常分类；这里只复制封闭枚举，不读取原异常或任意上下文。
            if (code == "auth.jwt_failure_observed" && logEvent.Properties.TryGetValue("failureKind", out var kind) &&
                kind is ScalarValue { Value: "aggregate" or "database" or "cancelled" or "timeout" or "io" or
                    "access-denied" or "argument" or "invalid-operation" or "other" })
                failureKind = (string)((ScalarValue)kind).Value!;
        }
        else
        {
            next.Emit(logEvent);
            return;
        }

        var properties = new List<LogEventProperty>
        {
            new("EventCode", new ScalarValue(code)),
            new("SourceCategory", new ScalarValue("http")),
            new("SourceContext", new ScalarValue(category)),
            new("Diagnostic", new ScalarValue(logEvent.Level < LogEventLevel.Warning))
        };
        if (failureKind != null) properties.Add(new LogEventProperty("failureKind", new ScalarValue(failureKind)));
        next.Emit(new LogEvent(logEvent.Timestamp, logEvent.Level, null,
            failureKind == null ? Template : FailureTemplate, properties,
            logEvent.TraceId ?? Activity.Current?.TraceId ?? default,
            logEvent.SpanId ?? Activity.Current?.SpanId ?? default));
    }

    private static (int?, string?) Identity(LogEvent logEvent)
    {
        if (!logEvent.Properties.TryGetValue("EventId", out var value) || value is not StructureValue details)
            return (null, null);
        int? id = null;
        string? name = null;
        var seenId = false;
        var seenName = false;
        foreach (var property in details.Properties)
        {
            if (property.Name == "Id")
            {
                if (seenId || property.Value is not ScalarValue { Value: int number }) return (null, null);
                seenId = true;
                id = number;
            }
            if (property.Name == "Name")
            {
                if (seenName || property.Value is not ScalarValue { Value: string text }) return (null, null);
                seenName = true;
                name = text;
            }
        }
        return (id, name);
    }
}
