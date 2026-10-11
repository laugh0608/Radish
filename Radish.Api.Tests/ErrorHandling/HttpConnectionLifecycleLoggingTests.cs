using System;
using System.IO;
using System.Net.WebSockets;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class HttpConnectionLifecycleLoggingTests
{
    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task Negotiate_ShouldPreserveJsonVersionsAndMissingConnectionResponses(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var scope = capture.SensitiveScope();
        using var fixture = new HttpTransportFixture(capture);
        foreach (var (version, code) in new[] { (HttpBoundaryCapture.Secret, "invalid"), ("0", "mismatch") })
        {
            var context = fixture.Context("POST", "?NegotiateVersion=" + version);
            await HttpTransportFixture.Run(fixture.Dispatcher, "ExecuteNegotiateAsync", context, new HttpConnectionDispatcherOptions { MinimumProtocolVersion = 1 });
            Assert.Equal(200, context.Response.StatusCode);
            using var json = JsonDocument.Parse(((MemoryStream)context.Response.Body).ToArray());
            Assert.True(json.RootElement.TryGetProperty("error", out _));
            Assert.False(json.RootElement.TryGetProperty("connectionId", out _));
            if (candidate && environment == "Production") Assert.Empty(capture.Events);
            TransportFrameworkLoggingTests.AssertSafe(capture);
            if (!candidate || environment == "Development")
                Assert.Equal(new[] { "signalr.http_dispatcher_negotiate_version_" + code, "signalr.http_dispatcher_negotiating" }, capture.Codes);
            capture.Clear();
        }
        var success = fixture.Context("POST", "?NegotiateVersion=99");
        await HttpTransportFixture.Run(fixture.Dispatcher, "ExecuteNegotiateAsync", success, new HttpConnectionDispatcherOptions());
        using var payload = JsonDocument.Parse(((MemoryStream)success.Response.Body).ToArray());
        Assert.Equal(1, payload.RootElement.GetProperty("negotiateVersion").GetInt32());
        var token = payload.RootElement.GetProperty("connectionToken").GetString()!;
        Assert.True(fixture.Contains(token));
        Assert.Equal(200, success.Response.StatusCode);
        Assert.DoesNotContain(token, capture.Output.ToString());
        capture.Clear();
        var missing = fixture.Context("GET", "?id=" + HttpBoundaryCapture.Secret);
        await fixture.Dispatch(missing);
        Assert.Equal(404, missing.Response.StatusCode);
        Assert.Empty(capture.Events);
        TransportFrameworkLoggingTests.AssertSafe(capture);
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task DeleteAndTransportRejections_ShouldKeepStatusAndCleanup(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var scope = capture.SensitiveScope();
        using var fixture = new HttpTransportFixture(capture);
        var connection = fixture.CreateConnection();
        var token = HttpTransportFixture.Get<string>(connection, "ConnectionToken");
        HttpTransportFixture.Set(connection, "TransportType", HttpTransportType.WebSockets);
        capture.Clear();
        var rejected = fixture.Context("DELETE", "?id=" + token);
        await fixture.Dispatch(rejected);
        Assert.Equal(400, rejected.Response.StatusCode);
        Assert.True(fixture.Contains(token));
        ExpectDiagnostic(capture, "signalr.http_dispatcher_delete_unsupported");
        capture.Clear();
        HttpTransportFixture.Set(connection, "TransportType", HttpTransportType.LongPolling);
        var accepted = fixture.Context("DELETE", "?id=" + token);
        await fixture.Dispatch(accepted);
        await HttpTransportFixture.Get<Task>(connection, "DisposeAndRemoveTask").WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(202, accepted.Response.StatusCode);
        Assert.False(fixture.Contains(token));
        ExpectDiagnostic(capture, "signalr.http_dispatcher_terminating");
        ExpectDiagnostic(capture, "signalr.http_manager_removed");
        TransportFrameworkLoggingTests.AssertSafe(capture);

        foreach (var scenario in new[] { "unsupported", "changed", "active" })
        {
            var item = fixture.CreateConnection();
            var id = HttpTransportFixture.Get<string>(item, "ConnectionToken");
            if (scenario == "changed") HttpTransportFixture.Set(item, "TransportType", HttpTransportType.ServerSentEvents);
            if (scenario == "active")
            {
                HttpTransportFixture.Set(item, "TransportType", HttpTransportType.ServerSentEvents);
                HttpTransportFixture.Set(item, "Status", Enum.Parse(HttpTransportFixture.Type("HttpConnectionStatus"), "Active"));
            }
            capture.Clear();
            var request = fixture.Context("GET", "?id=" + id);
            if (scenario == "active") request.Request.Headers.Accept = "text/event-stream";
            await fixture.Dispatch(request, new HttpConnectionDispatcherOptions
            {
                Transports = scenario == "unsupported" ? HttpTransportType.WebSockets : HttpTransportType.LongPolling | HttpTransportType.ServerSentEvents
            });
            Assert.Equal(scenario == "active" ? 409 : scenario == "unsupported" ? 404 : 400, request.Response.StatusCode);
            ExpectDiagnostic(capture, "signalr.http_dispatcher_" + (scenario == "active" ? "already_active" : scenario == "changed" ? "transport_change_rejected" : "transport_unsupported"));
            await fixture.DisposeConnection(item);
        }
        TransportFrameworkLoggingTests.AssertSafe(capture);
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task Dispose_ShouldClassifyResetWithoutPromotingItAndAlwaysRemoveConnection(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var scope = capture.SensitiveScope();
        using var fixture = new HttpTransportFixture(capture);
        foreach (var error in new Exception[] { new IOException(HttpBoundaryCapture.Secret), new WebSocketException(HttpBoundaryCapture.Secret, new IOException()), new InvalidOperationException(HttpBoundaryCapture.Secret) })
        {
            var connection = fixture.CreateConnection();
            HttpTransportFixture.Set(connection, "ApplicationTask", Task.CompletedTask);
            HttpTransportFixture.Set(connection, "TransportTask", Task.FromException<bool>(error));
            capture.Clear();
            await fixture.DisposeConnection(connection);
            if (error is InvalidOperationException)
            {
                var failure = Assert.Single(capture.Events, e => e.Code == "signalr.http_manager_dispose_failed");
                Assert.Equal("Error", failure.Level);
            }
            else
            {
                ExpectDiagnostic(capture, "signalr.http_manager_reset");
                Assert.DoesNotContain(capture.Events, e => e.Level is "Warning" or "Error" or "Fatal");
            }
            TransportFrameworkLoggingTests.AssertSafe(capture);
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task RequestBodyFailure_ShouldKeepConsumedStatusAndReleaseWriteLock(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var scope = capture.SensitiveScope();
        using var fixture = new HttpTransportFixture(capture);
        foreach (var error in new Exception[] { new IOException(HttpBoundaryCapture.Secret), new InvalidOperationException(HttpBoundaryCapture.Secret) })
        {
            var connection = fixture.CreateConnection();
            var context = fixture.Context("POST", "?id=" + HttpTransportFixture.Get<string>(connection, "ConnectionToken"));
            context.Request.Body = new FailingRequestStream(error);
            capture.Clear();
            await fixture.Dispatch(context);
            Assert.Equal(error is IOException ? 400 : 404, context.Response.StatusCode);
            Assert.Equal(1, HttpTransportFixture.Get<SemaphoreSlim>(connection, "WriteLock").CurrentCount);
            ExpectDiagnostic(capture, error is IOException ? "signalr.http_dispatcher_request_read_failed" : "signalr.http_dispatcher_disposed_during_write");
            Assert.DoesNotContain(capture.Events, e => e.Level is "Warning" or "Error" or "Fatal");
            TransportFrameworkLoggingTests.AssertSafe(capture);
            await fixture.DisposeConnection(connection);
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task AuthenticationExpiryAndUserChange_ShouldKeepCloseSignalAndWarning(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var scope = capture.SensitiveScope();
        using var fixture = new HttpTransportFixture(capture);
        var connection = fixture.CreateConnection(new HttpConnectionDispatcherOptions { CloseOnAuthenticationExpiration = true });
        HttpTransportFixture.Set(connection, "AuthenticationExpiration", DateTimeOffset.MinValue);
        HttpTransportFixture.Set(connection, "Status", Enum.Parse(HttpTransportFixture.Type("HttpConnectionStatus"), "Active"));
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = HttpTransportFixture.Get<CancellationToken>(connection, "ConnectionClosedRequested").Register(() => closed.TrySetResult());
        capture.Clear();
        HttpTransportFixture.Call(fixture.Manager, "Scan");
        await closed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        ExpectDiagnostic(capture, "signalr.http_manager_authentication_expired");
        await fixture.DisposeConnection(connection);

        connection = fixture.CreateConnection();
        var previous = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(new[]
        {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, HttpBoundaryCapture.Secret)
        }));
        HttpTransportFixture.Set(connection, "User", previous);
        var request = fixture.Context();
        request.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(new[]
        {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, HttpBoundaryCapture.Secret + "-new")
        }));
        var logScope = HttpTransportFixture.New("ConnectionLogScope", HttpBoundaryCapture.Secret);
        capture.Clear();
        var state = (Task<bool>)HttpTransportFixture.Call(fixture.Dispatcher, "EnsureConnectionStateAsync", connection, request,
            HttpTransportType.LongPolling, HttpTransportType.LongPolling, logScope)!;
        Assert.True(await state.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        var warning = Assert.Single(capture.Events, e => e.Code == "signalr.http_dispatcher_user_changed");
        Assert.Equal("Warning", warning.Level);
        Assert.Equal(HttpBoundaryCapture.Secret + "-new", HttpTransportFixture.Get<System.Security.Claims.ClaimsPrincipal>(connection, "User")
            .FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        TransportFrameworkLoggingTests.AssertSafe(capture);
        await fixture.DisposeConnection(connection);
    }

    private sealed class FailingRequestStream(Exception error) : MemoryStream
    {
        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) => Task.FromException(error);
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public void Resolver_ShouldKeepLastRegistrationCaseInsensitiveLookupAndQuietRejection(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var scope = capture.SensitiveScope();
        var type = Assembly.Load("Microsoft.AspNetCore.SignalR.Core").GetType("Microsoft.AspNetCore.SignalR.Internal.DefaultHubProtocolResolver", true)!;
        var logger = Activator.CreateInstance(typeof(Logger<>).MakeGenericType(type), capture.Factory)!;
        var first = new JsonHubProtocol();
        var last = new JsonHubProtocol();
        var resolver = Activator.CreateInstance(type, new object[] { new IHubProtocol[] { first, last }, logger })!;
        ExpectDiagnostic(capture, "signalr.protocol_registered");
        capture.Clear();
        Assert.Same(last, HttpTransportFixture.Call(resolver, "GetProtocol", "JSON", new[] { "json" }));
        ExpectDiagnostic(capture, "signalr.protocol_resolved");
        capture.Clear();
        Assert.Null(HttpTransportFixture.Call(resolver, "GetProtocol", HttpBoundaryCapture.Secret, null));
        Assert.Null(HttpTransportFixture.Call(resolver, "GetProtocol", "json", Array.Empty<string>()));
        Assert.Throws<ArgumentNullException>(() => HttpTransportFixture.Call(resolver, "GetProtocol", null, null));
        Assert.Empty(capture.Events);
        TransportFrameworkLoggingTests.AssertSafe(capture);
    }

    internal static void ExpectDiagnostic(HttpBoundaryCapture capture, string code)
    {
        if (!capture.Candidate || capture.Environment == "Development") Assert.Contains(code, capture.Codes);
        else Assert.DoesNotContain(code, capture.Codes);
    }
}
