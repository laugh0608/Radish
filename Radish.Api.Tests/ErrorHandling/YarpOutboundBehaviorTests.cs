using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Model;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class YarpOutboundBehaviorTests
{
    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task Forwarder_ShouldCopyResponseAndRetainFailureFeature(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "gateway");
        using var scope = capture.SensitiveScope();
        using var services = Services(capture);
        var forwarder = services.GetRequiredService<IHttpForwarder>();
        var context = Context();
        using var successHandler = new ControlledHttpHandler((request, _) =>
        {
            Assert.Equal("/" + HttpBoundaryCapture.Secret, request.RequestUri!.AbsolutePath);
            var response = new HttpResponseMessage(HttpStatusCode.Accepted) { Content = new StringContent("unaltered-response") };
            response.Headers.Add("X-Private", HttpBoundaryCapture.Secret);
            return Task.FromResult(response);
        });
        using var successClient = new HttpMessageInvoker(successHandler);
        Assert.Equal(ForwarderError.None, await forwarder.SendAsync(context, "https://example.invalid/", successClient, ForwarderRequestConfig.Empty, HttpTransformer.Default, TestContext.Current.CancellationToken));
        Assert.Equal(202, context.Response.StatusCode);
        Assert.Equal(HttpBoundaryCapture.Secret, context.Response.Headers["X-Private"]);
        Assert.Equal("unaltered-response", System.Text.Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray()));
        Assert.Null(context.Features.Get<IForwarderErrorFeature>());
        AssertCodes(capture, "proxy.forwarder_forwarding", "proxy.forwarder_response_received");
        capture.Clear();

        var failure = new HttpRequestException(HttpBoundaryCapture.Secret);
        using var failingHandler = new ControlledHttpHandler((_, _) => Task.FromException<HttpResponseMessage>(failure));
        using var failingClient = new HttpMessageInvoker(failingHandler);
        context = Context();
        Assert.Equal(ForwarderError.Request, await forwarder.SendAsync(context, "https://example.invalid/", failingClient, ForwarderRequestConfig.Empty, HttpTransformer.Default, TestContext.Current.CancellationToken));
        Assert.Equal(502, context.Response.StatusCode);
        Assert.Same(failure, context.Features.Get<IForwarderErrorFeature>()!.Exception);
        Assert.Equal(ForwarderError.Request, context.Features.Get<IForwarderErrorFeature>()!.Error);
        AssertCodes(capture, "proxy.forwarder_forwarding", "proxy.forwarder_failed");
        Assert.Equal("Warning", capture.Events.Last().Level);
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task ForwarderCancellation_ShouldKeepStatusAndOriginalException(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "gateway");
        using var scope = capture.SensitiveScope();
        using var services = Services(capture);
        var forwarder = services.GetRequiredService<IHttpForwarder>();
        foreach (var requestAborted in new[] { true, false })
        {
            using var cancellation = new CancellationTokenSource();
            var context = Context();
            if (requestAborted) context.RequestAborted = cancellation.Token;
            var failure = new OperationCanceledException(HttpBoundaryCapture.Secret, cancellation.Token);
            using var handler = new ControlledHttpHandler((_, token) =>
            {
                cancellation.Cancel();
                Assert.True(token.IsCancellationRequested);
                return Task.FromException<HttpResponseMessage>(failure);
            });
            using var client = new HttpMessageInvoker(handler);
            Assert.Equal(ForwarderError.RequestCanceled, await forwarder.SendAsync(context, "https://example.invalid/", client,
                ForwarderRequestConfig.Empty, HttpTransformer.Default, requestAborted ? CancellationToken.None : cancellation.Token));
            Assert.Equal(requestAborted ? 400 : 502, context.Response.StatusCode);
            Assert.Same(failure, context.Features.Get<IForwarderErrorFeature>()!.Exception);
            AssertCodes(capture, "proxy.forwarder_forwarding", "proxy.forwarder_cancelled");
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task ResponseBodyFailureAndTrailerException_ShouldRetainDistinctErrorBoundaries(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "gateway");
        using var scope = capture.SensitiveScope();
        using var services = Services(capture);
        var forwarder = services.GetRequiredService<IHttpForwarder>();
        var failure = new IOException(HttpBoundaryCapture.Secret);
        using var handler = new ControlledHttpHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StreamContent(new FailingReadStream(failure)) }));
        using var client = new HttpMessageInvoker(handler);
        var context = Context();
        var result = await forwarder.SendAsync(context, "https://example.invalid/", client, ForwarderRequestConfig.Empty, HttpTransformer.Default, TestContext.Current.CancellationToken);
        Assert.Equal(ForwarderError.ResponseBodyDestination, result);
        Assert.Equal(502, context.Response.StatusCode);
        Assert.Same(failure, context.Features.Get<IForwarderErrorFeature>()!.Exception);
        AssertCodes(capture, "proxy.forwarder_forwarding", "proxy.forwarder_response_received", "proxy.forwarder_failed");
        capture.Clear();

        var trailerFailure = new InvalidOperationException(HttpBoundaryCapture.Secret);
        using var trailerHandler = new ControlledHttpHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent("body") }));
        using var trailerClient = new HttpMessageInvoker(trailerHandler);
        context = Context();
        var thrown = await Record.ExceptionAsync(async () => await forwarder.SendAsync(context, "https://example.invalid/", trailerClient,
            ForwarderRequestConfig.Empty, new FailingTrailerTransformer(trailerFailure), TestContext.Current.CancellationToken));
        Assert.Same(trailerFailure, thrown);
        Assert.Null(context.Features.Get<IForwarderErrorFeature>());
        AssertCodes(capture, "proxy.forwarder_forwarding", "proxy.forwarder_response_received");
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task Middleware_ShouldKeepSeparateNoDestinationWarningsAnd503(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "gateway");
        using var scope = capture.SensitiveScope();
        using var services = Services(capture);
        using var client = new HttpMessageInvoker(new ControlledHttpHandler((_, _) => throw new InvalidOperationException("No send expected")));
        var cluster = new ClusterState(HttpBoundaryCapture.Secret, new ClusterModel(new ClusterConfig { ClusterId = HttpBoundaryCapture.Secret }, client));
        var route = new RouteModel(new RouteConfig { RouteId = HttpBoundaryCapture.Secret }, cluster, HttpTransformer.Default);
        var context = Context();
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(route), HttpBoundaryCapture.Secret));
        context.Features.Set<IReverseProxyFeature>(new ReverseProxyFeature
        { Route = route, Cluster = cluster.Model, AllDestinations = Array.Empty<DestinationState>(), AvailableDestinations = Array.Empty<DestinationState>() });
        var terminal = Middleware(services, "Yarp.ReverseProxy.Forwarder.ForwarderMiddleware", _ => throw new InvalidOperationException("Terminal middleware must not call next"));
        var balance = Middleware(services, "Yarp.ReverseProxy.LoadBalancing.LoadBalancingMiddleware", terminal);
        await balance(context);
        Assert.Equal(503, context.Response.StatusCode);
        Assert.Equal(ForwarderError.NoAvailableDestinations, context.Features.Get<IForwarderErrorFeature>()!.Error);
        AssertCodes(capture, "proxy.balancing_no_destinations", "proxy.middleware_no_destinations");
        Assert.All(capture.Events, e => Assert.Equal("Warning", e.Level));
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task InitializerAndLimits_ShouldKeepMissingClusterTimeoutAndSizeBehavior(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "gateway");
        using var scope = capture.SensitiveScope();
        using var services = Services(capture);
        var calls = 0;
        var initialize = Middleware(services, "Yarp.ReverseProxy.Model.ProxyPipelineInitializerMiddleware", _ => { calls++; return Task.CompletedTask; });
        var route = new RouteModel(new RouteConfig { RouteId = HttpBoundaryCapture.Secret }, null, HttpTransformer.Default);
        var context = Context();
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(route), "test"));
        await initialize(context);
        Assert.Equal(503, context.Response.StatusCode);
        Assert.Equal(0, calls);
        AssertCodes(capture, "proxy.pipeline_cluster_missing");
        capture.Clear();

        using var client = new HttpMessageInvoker(new ControlledHttpHandler((_, _) => throw new InvalidOperationException("No send expected")));
        var cluster = new ClusterState(HttpBoundaryCapture.Secret, new ClusterModel(new ClusterConfig { ClusterId = HttpBoundaryCapture.Secret }, client));
        route = new RouteModel(new RouteConfig { RouteId = HttpBoundaryCapture.Secret, MaxRequestBodySize = 42 }, cluster, HttpTransformer.Default);
        context = Context();
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(route, new RequestTimeoutAttribute(1000)), "test"));
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => initialize(context));
        Assert.Contains("UseRequestTimeouts", failure.Message);
        Assert.Equal(0, calls);
        AssertCodes(capture, "proxy.pipeline_timeout_not_applied");
        capture.Clear();

        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(route), "test"));
        await initialize(context);
        Assert.Equal(1, calls);
        Assert.Same(route, context.Features.Get<IReverseProxyFeature>()!.Route);
        calls = 0;
        var size = new BodySizeFeature();
        context.Features.Set<IHttpMaxRequestBodySizeFeature>(size);
        var limits = Middleware(services, "Yarp.ReverseProxy.Limits.LimitsMiddleware", _ => { calls++; return Task.CompletedTask; });
        await limits(context);
        Assert.Equal(42, size.MaxRequestBodySize);
        Assert.Equal(1, calls);
        AssertCodes(capture, "proxy.limits_body_size_set");
        capture.Clear();
        size.IsReadOnly = true;
        await limits(context);
        Assert.Equal(2, calls);
        Assert.Empty(capture.Events);
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public void ClientFactory_ShouldReuseUnchangedConfigurationWithoutSending(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "gateway");
        using var scope = capture.SensitiveScope();
        var factory = new ForwarderHttpClientFactory(capture.Factory.CreateLogger<ForwarderHttpClientFactory>());
        var config = new HttpClientConfig();
        using var first = factory.CreateClient(new ForwarderHttpClientContext { ClusterId = HttpBoundaryCapture.Secret, NewConfig = config });
        Assert.Same(first, factory.CreateClient(new ForwarderHttpClientContext
        { ClusterId = HttpBoundaryCapture.Secret, OldClient = first, OldConfig = config, NewConfig = config }));
        using var changed = factory.CreateClient(new ForwarderHttpClientContext
        { ClusterId = HttpBoundaryCapture.Secret, OldClient = first, OldConfig = config, NewConfig = config with { MaxConnectionsPerServer = 7 } });
        Assert.NotSame(first, changed);
        AssertCodes(capture, "proxy.client_created", "proxy.client_reused", "proxy.client_created");
    }

    private static ServiceProvider Services(HttpBoundaryCapture capture)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(capture.Factory);
        services.AddReverseProxy(); // 仅注册/解析依赖；不构建或启动宿主，也不加载代理配置。
        return services.BuildServiceProvider();
    }
    private static DefaultHttpContext Context()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("example.invalid");
        context.Request.Path = "/" + HttpBoundaryCapture.Secret;
        context.Request.Protocol = "HTTP/1.1";
        context.Response.Body = new MemoryStream();
        return context;
    }
    private static RequestDelegate Middleware(IServiceProvider services, string name, RequestDelegate next)
    {
        var type = typeof(IHttpForwarder).Assembly.GetType(name, true)!;
        var instance = ActivatorUtilities.CreateInstance(services, type, next);
        var invoke = type.GetMethod("Invoke")!;
        return context =>
        {
            try { return (Task)invoke.Invoke(instance, [context])!; }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            { ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); throw; }
        };
    }
    private static void AssertCodes(HttpBoundaryCapture capture, params string[] expected)
    {
        var warningOrError = new[] { "proxy.forwarder_failed", "proxy.balancing_no_destinations", "proxy.middleware_no_destinations", "proxy.pipeline_timeout_not_applied" };
        Assert.Equal(capture.Candidate && capture.Environment == "Production" ? expected.Where(warningOrError.Contains) : expected, capture.Codes);
        TransportFrameworkLoggingTests.AssertSafe(capture);
    }
    private sealed class BodySizeFeature : IHttpMaxRequestBodySizeFeature
    {
        public bool IsReadOnly { get; set; }
        public long? MaxRequestBodySize { get; set; }
    }
    private sealed class FailingReadStream(Exception failure) : MemoryStream
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => ValueTask.FromException<int>(failure);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => Task.FromException<int>(failure);
    }
    private sealed class FailingTrailerTransformer(Exception failure) : HttpTransformer
    {
        public override ValueTask TransformResponseTrailersAsync(HttpContext context, HttpResponseMessage proxyResponse, CancellationToken cancellationToken) => ValueTask.FromException(failure);
    }
}
