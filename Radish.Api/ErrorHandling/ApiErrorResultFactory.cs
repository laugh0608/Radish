using Microsoft.AspNetCore.Mvc;
using Radish.Model;

namespace Radish.Api.ErrorHandling;

public static class ApiErrorResultFactory
{
    public static ObjectResult Create(
        HttpContext httpContext,
        int statusCode,
        string message,
        string code,
        string? messageKey = null,
        object? responseData = null)
    {
        var traceId = ApplyTraceId(httpContext);
        var response = new MessageModel
        {
            StatusCode = statusCode,
            IsSuccess = false,
            MessageInfo = message,
            Code = code,
            MessageKey = messageKey,
            TraceId = traceId,
            ResponseData = responseData
        };

        return new ObjectResult(response)
        {
            StatusCode = statusCode
        };
    }

    public static async Task WriteAsync(
        HttpContext httpContext,
        int statusCode,
        string message,
        string code,
        string? messageKey = null,
        object? responseData = null,
        CancellationToken cancellationToken = default)
    {
        var result = Create(httpContext, statusCode, message, code, messageKey, responseData);
        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(
            result.Value,
            result.Value!.GetType(),
            cancellationToken: cancellationToken);
    }

    public static string ApplyTraceId(HttpContext httpContext)
    {
        var traceId = httpContext.TraceIdentifier;
        httpContext.Response.Headers["X-Correlation-ID"] = traceId;
        return traceId;
    }

    public static bool IsMessageModelApiRequest(HttpContext httpContext)
    {
        var path = httpContext.Request.Path;
        return path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
            && !path.StartsWithSegments("/api/health", StringComparison.OrdinalIgnoreCase);
    }
}
