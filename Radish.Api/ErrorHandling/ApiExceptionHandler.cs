using Microsoft.AspNetCore.Diagnostics;
using Radish.Common.Exceptions;
using Radish.Shared.Constants;

namespace Radish.Api.ErrorHandling;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (httpContext.Response.HasStarted || !ApiErrorResultFactory.IsMessageModelApiRequest(httpContext))
        {
            return false;
        }

        if (exception is BusinessException businessException)
        {
            if (businessException.StatusCode >= StatusCodes.Status500InternalServerError)
            {
                logger.LogError(
                    businessException,
                    "API server error {ErrorCode} at {Method} {Path}; TraceId: {TraceId}",
                    businessException.ErrorCode,
                    httpContext.Request.Method,
                    httpContext.Request.Path,
                    httpContext.TraceIdentifier);
            }
            else
            {
                logger.LogWarning(
                    "API business error {ErrorCode} at {Method} {Path}; TraceId: {TraceId}",
                    businessException.ErrorCode,
                    httpContext.Request.Method,
                    httpContext.Request.Path,
                    httpContext.TraceIdentifier);
            }

            await ApiErrorResultFactory.WriteAsync(
                httpContext,
                businessException.StatusCode,
                businessException.Message,
                businessException.ErrorCode ?? ApiErrorCodes.ValidationFailed,
                businessException.MessageKey,
                businessException.MessageArguments.ToArray(),
                cancellationToken: cancellationToken);
            return true;
        }

        logger.LogError(
            exception,
            "Unhandled API exception at {Method} {Path}; TraceId: {TraceId}",
            httpContext.Request.Method,
            httpContext.Request.Path,
            httpContext.TraceIdentifier);

        await ApiErrorResultFactory.WriteAsync(
            httpContext,
            StatusCodes.Status500InternalServerError,
            "服务器处理请求时发生错误，请稍后重试",
            ApiErrorCodes.UnexpectedError,
            "error.system.unexpected_error",
            cancellationToken: cancellationToken);
        return true;
    }
}
