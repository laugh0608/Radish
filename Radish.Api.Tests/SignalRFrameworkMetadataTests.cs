using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Radish.Api.Tests;

public sealed class SignalRFrameworkMetadataTests
{
    [Theory]
    [InlineData("Microsoft.AspNetCore.SignalR.HubConnectionHandlerLog", "ErrorDispatchingHubEvent", 1, LogLevel.Error)]
    [InlineData("Microsoft.AspNetCore.SignalR.HubConnectionHandlerLog", "ErrorProcessingRequest", 2, LogLevel.Debug)]
    [InlineData("Microsoft.AspNetCore.SignalR.HubConnectionHandlerLog", "ErrorSendingClose", 4, LogLevel.Debug)]
    [InlineData("Microsoft.AspNetCore.SignalR.Internal.DefaultHubDispatcherLog", "UnknownHubMethod", 3, LogLevel.Debug)]
    [InlineData("Microsoft.AspNetCore.SignalR.Internal.DefaultHubDispatcherLog", "FailedInvokingHubMethod", 8, LogLevel.Error)]
    [InlineData("Microsoft.AspNetCore.SignalR.Internal.DefaultHubDispatcherLog", "InvalidHubParameters", 22, LogLevel.Debug)]
    [InlineData("Microsoft.AspNetCore.SignalR.Internal.DefaultHubDispatcherLog", "FailedStreaming", 25, LogLevel.Error)]
    [InlineData("Microsoft.AspNetCore.SignalR.HubConnectionContext+Log", "HandshakeFailed", 5, LogLevel.Debug)]
    [InlineData("Microsoft.AspNetCore.SignalR.HubConnectionContext+Log", "FailedWritingMessage", 6, LogLevel.Error)]
    public void MappedEvents_ShouldMatchInstalledFrameworkMetadata(string type, string method, int id, LogLevel level)
    {
        // 升级框架时要求复核映射；这不是相应运行分支的验收证据。
        var owner = typeof(Hub).Assembly.GetType(type, throwOnError: true)!;
        var attribute = owner.GetMethod(method, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!
            .GetCustomAttribute<LoggerMessageAttribute>()!;
        Assert.Equal(id, attribute.EventId);
        Assert.Equal(level, attribute.Level);
        Assert.True(attribute.EventName == null || attribute.EventName == method);
    }
}
