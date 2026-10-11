using Microsoft.AspNetCore.Authentication.JwtBearer;
using Radish.Api.ErrorHandling;
using Radish.Common.LogTool;
using Radish.Shared.Constants;
using Serilog;

namespace Radish.Api.Security;

/// <summary>API 的 JWT 凭据选择与拒绝响应；日志只观察回调进入，不表示响应已写入。</summary>
public sealed class ApiJwtBearerEvents : JwtBearerEvents
{
    public override Task MessageReceived(MessageReceivedContext context)
    {
        // 浏览器 SignalR 的凭据仅允许从三个既有 Hub 路径的 query string 读取。
        var accessToken = context.Request.Query["access_token"].ToString();
        var path = context.HttpContext.Request.Path;
        if (!string.IsNullOrWhiteSpace(accessToken)
            && (path.StartsWithSegments("/hub/notification") ||
                path.StartsWithSegments("/hub/chat") ||
                path.StartsWithSegments("/hub/comment")))
        {
            context.Token = accessToken;
        }
        return Task.CompletedTask;
    }

    public override Task AuthenticationFailed(AuthenticationFailedContext context)
    {
        Log.ForContext<ApiJwtBearerEvents>().ForContext("EventCode", "auth.jwt_failure_observed")
            .Warning("JWT authentication failure observed; kind={failureKind}", RuntimeFailureSummary.Classify(context.Exception));
        return Task.CompletedTask;
    }

    public override Task Challenge(JwtBearerChallengeContext context)
    {
        Log.ForContext<ApiJwtBearerEvents>().ForContext("EventCode", "auth.jwt_challenge_observed")
            .Warning("JWT challenge observed.");
        if (!ApiErrorResultFactory.IsMessageModelApiRequest(context.HttpContext)) return Task.CompletedTask;

        context.HandleResponse();
        return ApiErrorResultFactory.WriteAsync(context.HttpContext,
            StatusCodes.Status401Unauthorized, "请先登录后再继续操作", ApiErrorCodes.Unauthorized,
            "error.auth.unauthorized", cancellationToken: context.HttpContext.RequestAborted);
    }

    public override Task Forbidden(ForbiddenContext context)
    {
        if (!ApiErrorResultFactory.IsMessageModelApiRequest(context.HttpContext)) return Task.CompletedTask;

        return ApiErrorResultFactory.WriteAsync(context.HttpContext,
            StatusCodes.Status403Forbidden, "当前账号无权执行此操作", ApiErrorCodes.Forbidden,
            "error.auth.forbidden", cancellationToken: context.HttpContext.RequestAborted);
    }
}
