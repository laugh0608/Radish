using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
using Microsoft.AspNetCore.Diagnostics;

namespace Radish.Api.ErrorHandling;

public static class ApiExceptionHandlerExtensions
{
    public static IApplicationBuilder UseApiExceptionHandler(this IApplicationBuilder app)
    {
        return app.UseExceptionHandler(exceptionApp =>
        {
            exceptionApp.Run(async context =>
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
            });
        });
    }

    [DoesNotReturn]
    private static void Rethrow(Exception exception)
    {
        ExceptionDispatchInfo.Capture(exception).Throw();
        throw new InvalidOperationException("Unreachable exception rethrow path.");
    }
}
