using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Radish.Gateway.HealthChecks;
using Radish.Gateway.PublicHead;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class OutboundHttpClientBehaviorTests
{
    private static readonly (string Name, string Owner)[] Clients =
    [ ("PublicHeadSnapshotClient", "public_head"), ("api-service", "api_health"), ("auth-service", "auth_health"), ("console-service", "console_health") ];

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task RegisteredClients_ShouldUseBothRealLoggingHandlersAndPreserveResponses(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "gateway");
        using var scope = capture.SensitiveScope();
        foreach (var row in Clients)
        foreach (var status in new[] { HttpStatusCode.OK, HttpStatusCode.InternalServerError })
        {
            using var response = new HttpResponseMessage(status) { Content = new StringContent("unchanged-body") };
            response.Headers.Add("X-Private", HttpBoundaryCapture.Secret);
            using var handler = new ControlledHttpHandler((_, _) => Task.FromResult(response));
            using var services = Services(capture, handler).BuildServiceProvider();
            using var client = services.GetRequiredService<IHttpClientFactory>().CreateClient(row.Name);
            using var request = Request();
            Assert.Same(response, await client.SendAsync(request, TestContext.Current.CancellationToken));
            Assert.Equal("unchanged-body", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            Assert.Equal(1, handler.Calls);
            AssertCodes(capture,
                $"http.{row.Owner}_pipeline_started", $"http.{row.Owner}_pipeline_request_headers",
                $"http.{row.Owner}_handler_started", $"http.{row.Owner}_handler_request_headers",
                $"http.{row.Owner}_handler_completed", $"http.{row.Owner}_handler_response_headers",
                $"http.{row.Owner}_pipeline_completed", $"http.{row.Owner}_pipeline_response_headers");
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task HandlerFailures_ShouldKeepBothStagesAtInformationAndRethrowSameException(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "gateway");
        using var scope = capture.SensitiveScope();
        foreach (var row in Clients)
        {
            var failure = new HttpRequestException(HttpBoundaryCapture.Secret);
            using var handler = new ControlledHttpHandler((_, _) => Task.FromException<HttpResponseMessage>(failure));
            using var services = Services(capture, handler).BuildServiceProvider();
            using var invoker = new HttpMessageInvoker(services.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(row.Name));
            using var request = Request();
            Assert.Same(failure, await Assert.ThrowsAsync<HttpRequestException>(() => invoker.SendAsync(request, CancellationToken.None)));
            AssertCodes(capture,
                $"http.{row.Owner}_pipeline_started", $"http.{row.Owner}_pipeline_request_headers",
                $"http.{row.Owner}_handler_started", $"http.{row.Owner}_handler_request_headers",
                $"http.{row.Owner}_handler_failed", $"http.{row.Owner}_pipeline_failed");
            Assert.All(capture.Events.Where(e => e.Code.EndsWith("_failed", StringComparison.Ordinal)), e =>
            {
                Assert.Equal(candidate ? "Info" : "Information", e.Level);
                Assert.True(e.Diagnostic);
            });
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task CancellationAndOtherExceptions_ShouldPropagateWithoutSyntheticFailureEvents(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "gateway");
        using var scope = capture.SensitiveScope();
        foreach (var row in Clients)
        foreach (var cancel in new[] { true, false })
        {
            using var cancellation = new CancellationTokenSource();
            Exception failure = cancel ? new OperationCanceledException(HttpBoundaryCapture.Secret, cancellation.Token) : new InvalidOperationException(HttpBoundaryCapture.Secret);
            using var handler = new ControlledHttpHandler((_, token) =>
            {
                Assert.Equal(cancellation.Token, token);
                if (cancel) cancellation.Cancel();
                return Task.FromException<HttpResponseMessage>(failure);
            });
            using var services = Services(capture, handler).BuildServiceProvider();
            using var invoker = new HttpMessageInvoker(services.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(row.Name));
            using var request = Request();
            Assert.Same(failure, await Record.ExceptionAsync(() => invoker.SendAsync(request, cancellation.Token)));
            Assert.Equal(cancel, cancellation.IsCancellationRequested);
            AssertCodes(capture,
                $"http.{row.Owner}_pipeline_started", $"http.{row.Owner}_pipeline_request_headers",
                $"http.{row.Owner}_handler_started", $"http.{row.Owner}_handler_request_headers");
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public void FactoryExpiryAndCleanup_ShouldKeepLiveEntriesAndConsumeDisposeFailures(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "gateway");
        using var scope = capture.SensitiveScope();
        foreach (var failDispose in new[] { false, true })
        {
            var handler = new ControlledHttpHandler((_, _) => throw new InvalidOperationException("No request expected"))
            { DisposeFailure = failDispose ? new IOException(HttpBoundaryCapture.Secret) : null };
            using var services = Services(capture, handler).BuildServiceProvider();
            var factory = services.GetRequiredService<IHttpClientFactory>();
            var handlerFactory = (IHttpMessageHandlerFactory)factory;
            var first = handlerFactory.CreateHandler(Clients[0].Name);
            Assert.Same(first, handlerFactory.CreateHandler(Clients[0].Name));
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var type = factory.GetType();
            var active = type.GetField("_activeHandlers", flags)!.GetValue(factory)!;
            var lazy = active.GetType().GetProperty("Item")!.GetValue(active, [Clients[0].Name])!;
            var entry = lazy.GetType().GetProperty("Value")!.GetValue(lazy)!;
            try
            {
                type.GetMethod("ExpiryTimer_Tick", flags)!.Invoke(factory, [entry]);
                type.GetMethod("CleanupTimer_Tick", flags)!.Invoke(factory, null);
                Assert.Equal(0, handler.Disposals);
                var queue = type.GetField("_expiredHandlers", flags)!.GetValue(factory)!;
                var expired = Assert.Single((Array)queue.GetType().GetMethod("ToArray")!.Invoke(queue, null)!);
                // 可控 weak-reference 边界；不依赖 GC 调度或真实 timer 等待。
                var weak = Assert.Single(expired!.GetType().GetFields(flags), f => f.FieldType == typeof(WeakReference));
                ((WeakReference)weak.GetValue(expired)!).Target = null;
                type.GetMethod("CleanupTimer_Tick", flags)!.Invoke(factory, null);
                Assert.Equal(1, handler.Disposals);
                Assert.Equal(0, (int)queue.GetType().GetProperty("Count")!.GetValue(queue)!);
                var expected = new List<string>
                {
                    "http.client_factory_handler_expired", "http.client_factory_cleanup_started", "http.client_factory_cleanup_completed",
                    "http.client_factory_cleanup_started"
                };
                if (failDispose) expected.Add("http.client_factory_cleanup_failed");
                expected.Add("http.client_factory_cleanup_completed");
                AssertCodes(capture, expected.ToArray());
            }
            finally
            {
                (type.GetField("_cleanupTimer", flags)!.GetValue(factory) as IDisposable)?.Dispose();
                GC.KeepAlive(first);
            }
            capture.Clear();
        }
    }

    private static ServiceCollection Services(HttpBoundaryCapture capture, HttpMessageHandler handler)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(capture.Factory);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DownstreamServices:ApiService:BaseUrl"] = "https://example.invalid",
            ["DownstreamServices:ApiService:HealthCheckPath"] = "/health",
            ["DownstreamServices:AuthService:BaseUrl"] = "https://example.invalid",
            ["DownstreamServices:AuthService:HealthCheckPath"] = "/health",
            ["ReverseProxy:Clusters:consoleCluster:Destinations:console:Address"] = "https://example.invalid"
        }).Build();
        services.AddGatewayHostHealthChecks(config, GatewayHostHealthChecks.CreateTags(config));
        services.AddHttpClient<PublicHeadSnapshotClient>();
        foreach (var row in Clients)
            services.AddHttpClient(row.Name).ConfigurePrimaryHttpMessageHandler(() => handler)
                .SetHandlerLifetime(Timeout.InfiniteTimeSpan).RedactLoggedHeaders(_ => false);
        return services;
    }

    private static HttpRequestMessage Request()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.invalid/" + HttpBoundaryCapture.Secret);
        request.Headers.Add("X-Private", HttpBoundaryCapture.Secret);
        return request;
    }

    internal static void AssertCodes(HttpBoundaryCapture capture, params string[] expected)
    {
        // 候选生产仍只输出 Warning+；清理失败属于 Error。
        Assert.Equal(capture.Candidate && capture.Environment == "Production"
            ? expected.Where(code => code == "http.client_factory_cleanup_failed") : expected, capture.Codes);
        TransportFrameworkLoggingTests.AssertSafe(capture);
    }
}

internal sealed class ControlledHttpHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
{
    internal int Calls;
    internal int Disposals;
    internal Exception? DisposeFailure;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        return send(request, cancellationToken);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Disposals++;
            if (DisposeFailure != null) throw DisposeFailure;
        }
        base.Dispose(disposing);
    }
}
