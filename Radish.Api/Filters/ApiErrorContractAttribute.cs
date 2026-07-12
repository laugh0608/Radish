using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Radish.Api.ErrorHandling;
using Radish.Model;
using Radish.Shared.Constants;

namespace Radish.Api.Filters;

/// <summary>
/// 在已迁移的 JSON Controller 上同步 MessageModel 与真实 HTTP 状态。
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ApiErrorContractAttribute : Attribute, IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Result is ObjectResult { Value: IMessageModel messageModel } objectResult)
        {
            if (messageModel.StatusCode is >= 100 and <= 599)
            {
                objectResult.StatusCode = messageModel.StatusCode;
            }

            if (!messageModel.IsSuccess && string.IsNullOrWhiteSpace(messageModel.TraceId))
            {
                messageModel.TraceId = ApiErrorResultFactory.ApplyTraceId(context.HttpContext);
            }

            if (!messageModel.IsSuccess && string.IsNullOrWhiteSpace(messageModel.Code))
            {
                (messageModel.Code, messageModel.MessageKey) = ResolveDefaultError(messageModel.StatusCode);
            }
        }

        await next();
    }

    private static (string Code, string MessageKey) ResolveDefaultError(int statusCode)
    {
        return statusCode switch
        {
            StatusCodes.Status401Unauthorized => (ApiErrorCodes.Unauthorized, "error.auth.unauthorized"),
            StatusCodes.Status403Forbidden => (ApiErrorCodes.Forbidden, "error.auth.forbidden"),
            StatusCodes.Status404NotFound => (ApiErrorCodes.NotFound, "error.common.not_found"),
            StatusCodes.Status409Conflict => (ApiErrorCodes.Conflict, "error.common.conflict"),
            StatusCodes.Status429TooManyRequests => (ApiErrorCodes.RateLimitExceeded, "error.rate_limit.exceeded"),
            StatusCodes.Status502BadGateway => (ApiErrorCodes.DependencyBadGateway, "error.dependency.bad_gateway"),
            StatusCodes.Status503ServiceUnavailable => (ApiErrorCodes.DependencyUnavailable, "error.dependency.unavailable"),
            StatusCodes.Status500InternalServerError => (ApiErrorCodes.UnexpectedError, "error.system.unexpected_error"),
            _ => (ApiErrorCodes.ValidationFailed, "error.common.validation_failed")
        };
    }
}
