using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class RoutingLinkLoggingTests
{
    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public void Links_ShouldKeepUriValuesAndFailureResults(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var scope = capture.SensitiveScope();
        using var fixture = new RoutingFixture(capture, e =>
        {
            e.MapGet("/account/{id:int}", _ => Task.CompletedTask).WithName("account").WithDisplayName(HttpBoundaryCapture.Secret);
            e.MapGet("/expand/{left:empty}-{right}", _ => Task.CompletedTask).WithName("expand");
        }, options => options.ConstraintMap["empty"] = typeof(EmptyTransformer));
        var generator = fixture.Services.GetRequiredService<LinkGenerator>();
        var values = new RouteValueDictionary { ["id"] = 42, ["email"] = HttpBoundaryCapture.Secret, ["returnUrl"] = "/private?key=" + HttpBoundaryCapture.Secret };
        var expected = "/account/42?email=" + HttpBoundaryCapture.Secret + "&returnUrl=%2Fprivate%3Fkey%3D" + HttpBoundaryCapture.Secret;
        Assert.Equal(expected, generator.GetPathByName("account", values));
        fixture.Contains("routing.link_endpoints_found", "routing.link_template_succeeded", "routing.link_generated");
        capture.Clear();
        Assert.Equal("https://routing.example.invalid" + expected, generator.GetUriByName("account", values, "https", new HostString("routing.example.invalid")));
        fixture.Contains("routing.link_generated");
        capture.Clear();
        Assert.Null(generator.GetPathByName("absent", values));
        fixture.Contains("routing.link_endpoints_missing");
        capture.Clear();
        values.Remove("id");
        Assert.Null(generator.GetPathByName("account", values));
        fixture.Contains("routing.link_required_values_rejected", "routing.link_generation_failed");
        capture.Clear();
        values["id"] = HttpBoundaryCapture.Secret;
        Assert.Null(generator.GetPathByName("account", values));
        fixture.Contains("routing.link_constraint_rejected", "routing.link_generation_failed");
        capture.Clear();
        Assert.Null(generator.GetPathByName("expand", new { left = HttpBoundaryCapture.Secret, right = "suffix" }));
        fixture.Contains("routing.link_expansion_failed", "routing.link_generation_failed");
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task FrameworkFormattingBeforeLogger_ShouldKeepOriginalFailurePropagation(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var scope = capture.SensitiveScope();
        using var fixture = new RoutingFixture(capture, e =>
        {
            e.MapGet("/reject/{id:poison}", _ => Task.CompletedTask);
            e.MapGet("/required/{id}", _ => Task.CompletedTask).WithName("required");
        }, options => options.ConstraintMap["poison"] = typeof(UnreadableConstraint));
        Exception? thrown = null;
        try { await fixture.Pipeline(fixture.Context("/reject/" + HttpBoundaryCapture.Secret)); }
        catch (Exception exception) { thrown = exception; }
        Assert.True(ReferenceEquals(UnreadableConstraint.Failure, thrown));
        Assert.DoesNotContain("routing.matcher_candidate_constraint_rejected", capture.Codes);
        Assert.DoesNotContain("routing.endpoint_entered", capture.Codes);
        RoutingFrameworkLoggingTests.AssertSafe(capture);
        capture.Clear();
        var generator = fixture.Services.GetRequiredService<LinkGenerator>();
        var value = new UnreadableValue();
        thrown = null;
        // 缺少 id 导致框架准备失败日志，在 FormatRouteValues 内先格式化额外值。
        try { generator.GetPathByName("required", new RouteValueDictionary { ["private"] = value }); }
        catch (Exception exception) { thrown = exception; }
        Assert.True(ReferenceEquals(value.Failure, thrown));
        Assert.DoesNotContain("routing.link_required_values_rejected", capture.Codes);
        Assert.DoesNotContain("routing.link_generation_failed", capture.Codes);
        RoutingFrameworkLoggingTests.AssertSafe(capture);
    }

    public sealed class EmptyTransformer : IOutboundParameterTransformer
    {
        public string? TransformOutbound(object? value) => null;
    }
    public sealed class UnreadableConstraint : IRouteConstraint
    {
        internal static readonly Exception Failure = new UnreadableRoutingException();
        public bool Match(HttpContext? httpContext, IRouter? route, string routeKey, RouteValueDictionary values, RouteDirection routeDirection) => false;
        public override string ToString() => throw Failure;
    }
    private sealed class UnreadableValue
    {
        internal readonly Exception Failure = new UnreadableRoutingException();
        public override string ToString() => throw Failure;
    }
}
