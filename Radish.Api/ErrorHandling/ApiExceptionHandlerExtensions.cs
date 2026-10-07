using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
using Microsoft.AspNetCore.Diagnostics;

namespace Radish.Api.ErrorHandling;

public static class ApiExceptionHandlerExtensions
{
    public static IApplicationBuilder UseApiExceptionHandler(this IApplicationBuilder app)
    {
        return app.UseExceptionHandler(new ExceptionHandlerOptions
        {
            // 业务 404 是合法的 JSON 错误结果；响应尚未开始时也应正常完成。
            AllowStatusCode404Response = true,
            // 已完成的 API 错误响应按契约记录；未处理路径仍由框架记录并传播。
            SuppressDiagnosticsCallback = _ => true,
            ExceptionHandler = async context =>
            {
                var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
                if (exception == null)
                {
                    return;
                }

                var handler = context.RequestServices.GetRequiredService<ApiExceptionHandler>();
                if (await handler.TryHandleAsync(context, exception, context.RequestAborted))
                {
                    return;
                }

                Rethrow(exception);
            }
        });
    }

    [DoesNotReturn]
    private static void Rethrow(Exception exception)
    {
        ExceptionDispatchInfo.Capture(exception).Throw();
        throw new InvalidOperationException("Unreachable exception rethrow path.");
    }
}
