using System.Reflection;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

public sealed class HttpExceptionFrameworkMetadataTests
{
    [Theory]
    [InlineData("UnhandledException", 1, "UnhandledException", LogLevel.Error)]
    [InlineData("ResponseStartedErrorHandler", 2, "ResponseStarted", LogLevel.Warning)]
    [InlineData("ErrorHandlerException", 3, "Exception", LogLevel.Error)]
    [InlineData("RequestAbortedException", 4, "RequestAborted", LogLevel.Debug)]
    public void Mapping_ShouldMatchInstalledFramework(string method, int id, string name, LogLevel level)
    {
        var type = typeof(ExceptionHandlerMiddleware).Assembly.GetType("Microsoft.AspNetCore.Diagnostics.DiagnosticsLoggerExtensions", true)!;
        var attribute = type.GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!
            .GetCustomAttribute<LoggerMessageAttribute>()!;
        Assert.Equal(id, attribute.EventId);
        Assert.Equal(name, attribute.EventName);
        Assert.Equal(level, attribute.Level);
    }
}
