using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class RoutingPipelineLoggingTests
{
    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task MatchMissFallbackAndPresetEndpoint_ShouldKeepSelectionAndResponse(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var scope = capture.SensitiveScope();
        using var fixture = new RoutingFixture(capture, endpoints => endpoints.MapGet("/items/{id}", c =>
        {
            Assert.Equal(HttpBoundaryCapture.Secret, c.Request.RouteValues["id"]);
            c.Response.StatusCode = 202;
            return Task.CompletedTask;
        }).WithDisplayName(HttpBoundaryCapture.Secret));
        var context = fixture.Context("/items/" + HttpBoundaryCapture.Secret);
        await fixture.Pipeline(context);
        Assert.Equal(202, context.Response.StatusCode);
        fixture.Contains("routing.pipeline_matched", "routing.pipeline_size_metadata_missing", "routing.endpoint_entered", "routing.endpoint_exited", "routing.matcher_candidates_found", "routing.matcher_candidate_valid");
        capture.Clear();
        context = fixture.Context("/missing/" + HttpBoundaryCapture.Secret);
        await fixture.Pipeline(context);
        Assert.Null(context.GetEndpoint());
        Assert.Equal(404, context.Response.StatusCode);
        fixture.Contains("routing.pipeline_unmatched", "routing.matcher_candidates_missing");
        Assert.DoesNotContain("routing.endpoint_entered", capture.Codes);
        capture.Clear();
        context = fixture.Context("/ignored");
        var preset = new Endpoint(c => { c.Response.StatusCode = 203; return Task.CompletedTask; }, EndpointMetadataCollection.Empty, HttpBoundaryCapture.Secret);
        context.SetEndpoint(preset);
        await fixture.Pipeline(context);
        Assert.Same(preset, context.GetEndpoint());
        Assert.Equal(203, context.Response.StatusCode);
        fixture.Contains("routing.pipeline_matching_skipped", "routing.endpoint_entered", "routing.endpoint_exited");
        Assert.DoesNotContain("routing.pipeline_matched", capture.Codes);
        capture.Clear();
        using var fallback = new RoutingFixture(capture, e => e.MapFallback(c => { c.Response.StatusCode = 204; return Task.CompletedTask; }));
        context = fallback.Context("/missing/" + HttpBoundaryCapture.Secret);
        await fallback.Pipeline(context);
        Assert.Equal(204, context.Response.StatusCode);
        fallback.Contains("routing.pipeline_fallback_matched", "routing.endpoint_exited");
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task ConstraintsComplexSegmentsAndAmbiguity_ShouldKeepRejection(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var scope = capture.SensitiveScope();
        using var fixture = new RoutingFixture(capture, e =>
        {
            e.MapGet("/number/{id:int}", _ => Task.CompletedTask);
            e.MapGet("/complex/{left}-{right}", _ => Task.CompletedTask);
            // 本用例刻意构造歧义，验证框架异常仍会传播。
#pragma warning disable ASP0022
            e.MapGet("/ambiguous/{first}", _ => Task.CompletedTask);
            e.MapGet("/ambiguous/{second}", _ => Task.CompletedTask);
#pragma warning restore ASP0022
        });
        foreach (var (path, code) in new[] { ("/number/" + HttpBoundaryCapture.Secret, "routing.matcher_candidate_constraint_rejected"), ("/complex/no_separator", "routing.matcher_candidate_complex_rejected") })
        {
            var context = fixture.Context(path);
            await fixture.Pipeline(context);
            Assert.Equal(404, context.Response.StatusCode);
            Assert.Null(context.GetEndpoint());
            fixture.Contains(code, "routing.matcher_candidate_invalid", "routing.pipeline_unmatched");
            capture.Clear();
        }
        var exception = await Record.ExceptionAsync(() => fixture.Pipeline(fixture.Context("/ambiguous/" + HttpBoundaryCapture.Secret)));
        Assert.Equal("Microsoft.AspNetCore.Routing.Matching.AmbiguousMatchException", exception?.GetType().FullName);
        Assert.DoesNotContain("routing.endpoint_entered", capture.Codes);
        RoutingFrameworkLoggingTests.AssertSafe(capture);
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task RequestSizeMetadata_ShouldKeepMissingReadonlySetAndDisabledBehavior(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var scope = capture.SensitiveScope();
        foreach (var kind in new[] { "missing", "readonly", "set", "disabled" })
        {
            var limit = kind == "disabled" ? (long?)null : 1234L;
            using var fixture = new RoutingFixture(capture, e => e.MapGet("/size", _ => Task.CompletedTask).WithMetadata(new SizeMetadata(limit)));
            var context = fixture.Context("/size");
            var feature = new SizeFeature { IsReadOnly = kind == "readonly", MaxRequestBodySize = 99 };
            if (kind != "missing") context.Features.Set<IHttpMaxRequestBodySizeFeature>(feature);
            await fixture.Pipeline(context);
            Assert.Equal(200, context.Response.StatusCode);
            Assert.Equal(kind is "missing" or "readonly" ? 99 : limit, feature.MaxRequestBodySize);
            fixture.Contains(kind switch
            {
                "missing" => "routing.pipeline_size_feature_missing",
                "readonly" => "routing.pipeline_size_feature_read_only",
                "set" => "routing.pipeline_body_size_set",
                _ => "routing.pipeline_body_size_disabled"
            });
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task EndpointAndShortCircuitFailures_ShouldPropagateOriginalAndLogExit(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var scope = capture.SensitiveScope();
        foreach (var shortCircuit in new[] { false, true })
        foreach (var kind in new[] { "success", "sync", "async", "cancel" })
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            var original = kind == "cancel" ? (Exception)new OperationCanceledException(cancellation.Token) : new UnreadableRoutingException();
            var ranBetween = false;
            using var fixture = new RoutingFixture(capture, e =>
            {
                var endpoint = e.MapGet("/execute", (RequestDelegate)(c => kind switch
                {
                    "success" => Task.CompletedTask,
                    "sync" => throw original,
                    "async" => Task.FromException(original),
                    _ => Task.FromException(original)
                })).WithDisplayName(HttpBoundaryCapture.Secret);
                if (shortCircuit) endpoint.ShortCircuit(202);
            }, between: app => app.Use((context, next) => { ranBetween = true; return next(context); }));
            var context = fixture.Context("/execute");
            context.RequestAborted = cancellation.Token;
            Exception? thrown = null;
            try { await fixture.Pipeline(context); } catch (Exception exception) { thrown = exception; }
            Assert.True(kind == "success" ? thrown == null : ReferenceEquals(original, thrown));
            Assert.Equal(!shortCircuit, ranBetween);
            Assert.Equal(shortCircuit ? 202 : 200, context.Response.StatusCode);
            fixture.Contains(shortCircuit ? "routing.pipeline_short_circuit_entered" : "routing.endpoint_entered",
                shortCircuit ? "routing.pipeline_short_circuit_exited" : "routing.endpoint_exited");
            if (shortCircuit) Assert.DoesNotContain("routing.endpoint_entered", capture.Codes);
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task ShortCircuitWithoutDelegate_AndSecurityMetadataGuards_ShouldRemainEffective(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var scope = capture.SensitiveScope();
        // 普通 RouteEndpoint 构造器禁止 null delegate；该条件分支只能直接调用真实框架方法。
        IEndpointRouteBuilder? routes = null;
        using (var fixture = new RoutingFixture(capture, e =>
        {
            routes = e;
            e.MapGet("/metadata", _ => Task.CompletedTask).ShortCircuit(205);
        }))
        {
            var metadata = routes!.DataSources.SelectMany(s => s.Endpoints).Single().Metadata;
            var shortCircuit = metadata.Single(m => m.GetType().Name == "ShortCircuitMetadata");
            var endpoint = new Endpoint(null, metadata, HttpBoundaryCapture.Secret);
            var type = Assembly.Load("Microsoft.AspNetCore.Routing").GetType("Microsoft.AspNetCore.Routing.EndpointRoutingMiddleware", true)!;
            var reachedNext = false;
            var middleware = ActivatorUtilities.CreateInstance(fixture.Services, type, routes,
                (RequestDelegate)(_ => { reachedNext = true; return Task.CompletedTask; }));
            var context = fixture.Context("/empty");
            var task = (Task)type.GetMethod("ExecuteShortCircuit", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(middleware, new object[] { shortCircuit, endpoint, context })!;
            await task;
            Assert.False(reachedNext);
            Assert.Equal(205, context.Response.StatusCode);
            fixture.Contains("routing.pipeline_short_circuit_without_delegate");
            Assert.DoesNotContain("routing.endpoint_entered", capture.Codes);
        }
        capture.Clear();
        foreach (var shortCircuit in new[] { false, true })
        foreach (var metadata in new object[] { new AuthorizeAttribute(), new EnableCorsAttribute("private"), new AntiforgeryMetadata() })
        {
            var executed = false;
            using var fixture = new RoutingFixture(capture, e =>
            {
                var endpoint = e.Map("/guard", (RequestDelegate)(_ => { executed = true; return Task.CompletedTask; })).WithMetadata(metadata);
                if (shortCircuit) endpoint.ShortCircuit();
            });
            var context = fixture.Context("/guard");
            context.Request.Method = "POST";
            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Pipeline(context));
            Assert.False(executed);
            Assert.DoesNotContain("routing.endpoint_entered", capture.Codes);
            Assert.DoesNotContain("routing.pipeline_short_circuit_entered", capture.Codes);
            RoutingFrameworkLoggingTests.AssertSafe(capture);
            capture.Clear();
        }
    }

    private sealed record SizeMetadata(long? MaxRequestBodySize) : IRequestSizeLimitMetadata;
    private sealed class SizeFeature : IHttpMaxRequestBodySizeFeature
    {
        public bool IsReadOnly { get; init; }
        public long? MaxRequestBodySize { get; set; }
    }
    private sealed class AntiforgeryMetadata : IAntiforgeryMetadata
    {
        public bool RequiresValidation => true;
    }
}
