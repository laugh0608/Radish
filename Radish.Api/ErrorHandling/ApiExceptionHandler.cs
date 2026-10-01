using Microsoft.AspNetCore.Diagnostics;
using Radish.Common.Exceptions;
using Radish.Common.LogTool;
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
                WriteFailure(businessException, businessException.StatusCode);
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

        WriteFailure(exception, StatusCodes.Status500InternalServerError);

        await ApiErrorResultFactory.WriteAsync(
            httpContext,
            StatusCodes.Status500InternalServerError,
            "服务器处理请求时发生错误，请稍后重试",
            ApiErrorCodes.UnexpectedError,
            "error.system.unexpected_error",
            cancellationToken: cancellationToken);
        return true;
    }

    private void WriteFailure(Exception exception, int statusCode)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["EventCode"] = "http.failed", ["SourceCategory"] = "http"
        });
        // 不把 Exception、URL、业务错误文案交给旧 sink；候选与旧路径同样安全。
        logger.LogError("Request failed with {statusCode}; kind={failureKind}", statusCode, RuntimeFailureSummary.Classify(exception));
    }
}
