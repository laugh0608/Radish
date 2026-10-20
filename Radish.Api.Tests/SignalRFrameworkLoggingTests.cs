using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Serilog.Events;
using Xunit;

namespace Radish.Api.Tests;

[Collection("Runtime logging global state")]
public sealed class SignalRFrameworkLoggingTests
{
    public static TheoryData<bool, string> OutputModes => new()
    {
        { false, "Production" }, { true, "Production" }, { false, "Development" }, { true, "Development" }
    };

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task Invocation_ShouldPreserveCompletionAndConnection_WithoutRawPayload(bool candidate, string environment)
    {
        using var capture = new SignalRLogCapture(candidate, environment);
        using var scope = capture.Factory.CreateLogger("scope").BeginScope(new Dictionary<string, object>
        {
            ["credential"] = SignalRLogCapture.Secret, ["RequestPath"] = "/" + SignalRLogCapture.Secret
        });
        await using var fixture = new SignalRConnectionFixture(capture.Factory, environment);
        await fixture.Handshake();
        await fixture.Send("{\"type\":1,\"invocationId\":\"normal\",\"target\":\"Echo\",\"arguments\":[\"" + SignalRLogCapture.Secret + "\"]}");
        using (var response = JsonDocument.Parse(await fixture.Read()))
            Assert.Equal(SignalRLogCapture.Secret, response.RootElement.GetProperty("result").GetString());
        foreach (var failure in new Exception[] { new IOException(SignalRLogCapture.Secret), new HubException(SignalRLogCapture.Secret) })
        {
            fixture.State.Failure = failure;
            await fixture.Send("{\"type\":1,\"invocationId\":\"failure\",\"target\":\"Fail\",\"arguments\":[]}");
            using var response = JsonDocument.Parse(await fixture.Read());
            Assert.Equal(3, response.RootElement.GetProperty("type").GetInt32());
            var error = response.RootElement.GetProperty("error").GetString()!;
            Assert.Equal(environment == "Development" || failure is HubException, error.Contains(SignalRLogCapture.Secret));
        }
        await fixture.Close();
        Assert.Equal(new[] { "connect", "disconnect" }, fixture.State.Steps);
        Assert.Null(fixture.State.DisconnectReason);
        Assert.Equal(2, capture.Codes.Count(c => c == "signalr.invocation_failed"));
        capture.AssertSafe();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task BindingAndUnknownMethod_ShouldKeepErrorsAndOriginalDiagnosticLevel(bool candidate, string environment)
    {
        using var capture = new SignalRLogCapture(candidate, environment);
        await using var fixture = new SignalRConnectionFixture(capture.Factory, environment);
        await fixture.Handshake();
        foreach (var (target, args) in new[] { (SignalRLogCapture.Secret, "[]"), ("Number", "[\"" + SignalRLogCapture.Secret + "\"]") })
        {
            await fixture.Send($"{{\"type\":1,\"invocationId\":\"reject\",\"target\":\"{target}\",\"arguments\":{args}}}");
            using var response = JsonDocument.Parse(await fixture.Read());
            Assert.True(response.RootElement.TryGetProperty("error", out _));
        }
        await fixture.Close();
        Assert.Equal(candidate && environment == "Production" ? 0 : 2,
            capture.Codes.Count(c => c == "signalr.invocation_rejected"));
        capture.AssertSafe();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task ConnectFailure_ShouldCloseWithoutDisconnectCallback(bool candidate, string environment)
    {
        using var capture = new SignalRLogCapture(candidate, environment);
        await using var fixture = new SignalRConnectionFixture(capture.Factory, environment);
        fixture.State.ConnectFailure = new IOException(SignalRLogCapture.Secret);
        await fixture.Handshake();
        using var response = JsonDocument.Parse(await fixture.Read());
        Assert.Equal(7, response.RootElement.GetProperty("type").GetInt32());
        Assert.False(response.RootElement.TryGetProperty("allowReconnect", out var reconnect) && reconnect.GetBoolean());
        Assert.Equal(environment == "Development", response.RootElement.GetProperty("error").GetString()!.Contains(SignalRLogCapture.Secret));
        await fixture.Running.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(new[] { "connect" }, fixture.State.Steps);
        Assert.Single(capture.Codes, c => c == "signalr.connect_failed");
        capture.AssertSafe();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task ClientCloseAndCleanupFailure_ShouldPreserveReasonAndOriginalException(bool candidate, string environment)
    {
        using var capture = new SignalRLogCapture(candidate, environment);
        await using var fixture = new SignalRConnectionFixture(capture.Factory, environment);
        var failure = new IOException(SignalRLogCapture.Secret);
        fixture.State.DisconnectFailure = failure;
        await fixture.Handshake();
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => fixture.Close(SignalRLogCapture.Secret)));
        Assert.IsType<HubException>(fixture.State.DisconnectReason);
        Assert.Equal(SignalRLogCapture.Secret, fixture.State.DisconnectReason!.Message);
        Assert.Equal(new[] { "connect", "disconnect" }, fixture.State.Steps);
        Assert.Single(capture.Codes, c => c == "signalr.disconnect_failed");
        capture.AssertSafe();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task ClientErrorClose_ShouldReachCallbackWithoutInventingFrameworkError(bool candidate, string environment)
    {
        using var capture = new SignalRLogCapture(candidate, environment);
        await using var fixture = new SignalRConnectionFixture(capture.Factory, environment);
        await fixture.Handshake();
        await fixture.Close(SignalRLogCapture.Secret);
        Assert.IsType<HubException>(fixture.State.DisconnectReason);
        Assert.Equal(SignalRLogCapture.Secret, fixture.State.DisconnectReason!.Message);
        Assert.Equal(new[] { "connect", "disconnect" }, fixture.State.Steps);
        Assert.DoesNotContain(capture.Codes, code => code.EndsWith("_failed", StringComparison.Ordinal));
        capture.AssertSafe();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task InvalidMessage_ShouldDisconnectWithParserFailure(bool candidate, string environment)
    {
        using var capture = new SignalRLogCapture(candidate, environment);
        await using var fixture = new SignalRConnectionFixture(capture.Factory, environment);
        await fixture.Handshake();
        await fixture.Send("{\"type\":\"" + SignalRLogCapture.Secret + "\"}");
        using var response = JsonDocument.Parse(await fixture.Read());
        Assert.Equal(7, response.RootElement.GetProperty("type").GetInt32());
        await fixture.Running.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.NotNull(fixture.State.DisconnectReason);
        Assert.Equal(candidate && environment == "Production" ? 0 : 1, capture.Codes.Count(c => c == "signalr.message_failed"));
        capture.AssertSafe();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task BadHandshake_ShouldRejectBeforeHubCallbacks(bool candidate, string environment)
    {
        using var capture = new SignalRLogCapture(candidate, environment);
        await using var fixture = new SignalRConnectionFixture(capture.Factory, environment);
        fixture.Start();
        await fixture.Send("{\"protocol\":42,\"version\":1,\"secret\":\"" + SignalRLogCapture.Secret + "\"}");
        using var response = JsonDocument.Parse(await fixture.Read());
        Assert.True(response.RootElement.TryGetProperty("error", out _));
        await fixture.Running.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Empty(fixture.State.Steps);
        Assert.Equal(candidate && environment == "Production" ? 0 : 1, capture.Codes.Count(c => c == "signalr.handshake_failed"));
        capture.AssertSafe();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task WriteFailure_ShouldAbortAndRetainCleanup(bool candidate, string environment)
    {
        using var capture = new SignalRLogCapture(candidate, environment);
        await using var fixture = new SignalRConnectionFixture(capture.Factory, environment);
        await fixture.Handshake();
        fixture.Writer.Fail = true;
        await fixture.Send("{\"type\":1,\"invocationId\":\"write\",\"target\":\"Echo\",\"arguments\":[\"" + SignalRLogCapture.Secret + "\"]}");
        await fixture.Running.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(new[] { "connect", "disconnect" }, fixture.State.Steps);
        Assert.IsType<IOException>(fixture.State.DisconnectReason);
        // Completion 写入失败后，框架还尝试发送 Close；两个独立写入故障都保留。
        Assert.Equal(2, fixture.Writer.Failures.Count);
        Assert.Same(fixture.Writer.Failures[0], fixture.State.DisconnectReason);
        Assert.Equal(2, capture.Codes.Count(c => c == "signalr.write_failed"));
        capture.AssertSafe();
    }
}
