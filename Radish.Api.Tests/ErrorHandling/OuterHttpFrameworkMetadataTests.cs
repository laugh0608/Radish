using System;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

public sealed class OuterHttpFrameworkMetadataTests
{
    private const string KestrelAssembly = "Microsoft.AspNetCore.Server.Kestrel.Core";
    private const string KestrelLog = "Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure.KestrelTrace+GeneralLog";
    private const string DiagnosticsAssembly = "Microsoft.AspNetCore.Diagnostics";
    private const string DeveloperLog = "Microsoft.AspNetCore.Diagnostics.DeveloperExceptionPageMiddlewareLoggerExtensions";
    private const string DiagnosticsLog = "Microsoft.AspNetCore.Diagnostics.DiagnosticsLoggerExtensions";

    [Theory]
    [InlineData(KestrelAssembly, KestrelLog, "ApplicationError", 13, "ApplicationError", LogLevel.Error)]
    [InlineData(KestrelAssembly, KestrelLog, "HeartbeatSlow", 22, "HeartbeatSlow", LogLevel.Warning)]
    [InlineData(KestrelAssembly, KestrelLog, "ApplicationNeverCompleted", 23, "ApplicationNeverCompleted", LogLevel.Critical)]
    [InlineData(KestrelAssembly, KestrelLog, "InvalidResponseHeaderRemoved", 41, "InvalidResponseHeaderRemoved", LogLevel.Warning)]
    [InlineData(KestrelAssembly, KestrelLog, "Http2DisabledWithHttp1AndNoTls", 64, "Http2DisabledWithHttp1AndNoTls", LogLevel.Warning)]
    [InlineData(KestrelAssembly, KestrelLog, "Http3DisabledWithHttp1AndNoTls", 65, "Http3DisabledWithHttp1AndNoTls", LogLevel.Warning)]
    [InlineData(KestrelAssembly, KestrelLog, "RequestAbortedException", 66, "RequestAborted", LogLevel.Debug)]
    [InlineData(KestrelAssembly, KestrelLog, "RequestBodyDrainBodyReaderInvalidState", 67, "RequestBodyDrainBodyReaderInvalidState", LogLevel.Error)]
    [InlineData(DiagnosticsAssembly, DeveloperLog, "ResponseStartedErrorPageMiddleware", 2, "ResponseStarted", LogLevel.Warning)]
    [InlineData(DiagnosticsAssembly, DeveloperLog, "DisplayErrorPageException", 3, "DisplayErrorPageException", LogLevel.Error)]
    [InlineData(DiagnosticsAssembly, DiagnosticsLog, "UnhandledException", 1, "UnhandledException", LogLevel.Error)]
    [InlineData(DiagnosticsAssembly, DiagnosticsLog, "RequestAbortedException", 4, "RequestAborted", LogLevel.Debug)]
    public void LoggerMapping_ShouldMatchInstalledFramework(string assembly, string owner, string method, int id, string name, LogLevel level)
    {
        var type = Assembly.Load(assembly).GetType(owner, true)!;
        var attribute = type.GetMethod(method, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!
            .GetCustomAttribute<LoggerMessageAttribute>()!;
        Assert.Equal(id, attribute.EventId);
        Assert.Equal(name, attribute.EventName);
        Assert.Equal(level, attribute.Level);
    }

    [Theory]
    [InlineData("RequestStarting", 1)]
    [InlineData("RequestFinished", 2)]
    [InlineData("ApplicationStartupException", 6)]
    [InlineData("HostingStartupAssemblyException", 11)]
    [InlineData("PortsOverridenByUrls", 15)]
    [InlineData("RequestUnhandled", 16)]
    public void HostingIds_ShouldMatchInstalledFrameworkConstants(string name, int id)
    {
        var type = Assembly.Load("Microsoft.AspNetCore.Hosting").GetType("Microsoft.AspNetCore.Hosting.LoggerEventIds", true)!;
        Assert.Equal(id, type.GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.GetRawConstantValue());
    }
}
