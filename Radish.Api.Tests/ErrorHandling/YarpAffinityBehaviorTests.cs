using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.SessionAffinity;
using Yarp.ReverseProxy.Transforms;
using Xunit;
using static Radish.Api.Tests.ErrorHandling.YarpConditionalTestSupport;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class YarpAffinityBehaviorTests
{
    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task FourPolicies_ShouldKeepRoundTripMissingDestinationAndResponseRules(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "gateway");
        using var scope = capture.SensitiveScope();
        var clock = new ControlledTime();
        using var services = BuildServices(capture, clock);
        using var client = NoSendClient();
        foreach (var policy in services.GetServices<ISessionAffinityPolicy>())
        {
            var config = new SessionAffinityConfig { Enabled = true, Policy = policy.Name, AffinityKeyName = "affinity-" + HttpBoundaryCapture.Secret };
            var cluster = Cluster(services, client, new ClusterConfig { SessionAffinity = config }, "target-" + HttpBoundaryCapture.Secret);
            var target = Assert.Single(cluster.Destinations).Value;
            var all = cluster.DestinationsState.AllDestinations;
            var empty = Context(cluster);
            Assert.Equal(AffinityStatus.AffinityKeyNotSet, (await policy.FindAffinitizedDestinationsAsync(empty, cluster, config, all, CancellationToken.None)).Status);
            await policy.AffinitizeResponseAsync(empty, cluster, config, target, CancellationToken.None);
            var key = ReadResponseKey(empty, policy.Name);
            Assert.NotEmpty(key);
            var matching = Context(cluster, target);
            SetRequestKey(matching, policy.Name, config.AffinityKeyName!, key);
            var found = await policy.FindAffinitizedDestinationsAsync(matching, cluster, config, all, CancellationToken.None);
            Assert.Equal(AffinityStatus.OK, found.Status);
            Assert.Same(target, Assert.Single(found.Destinations!));
            // 真实响应 transform 保留已匹配的 affinity，不重复设置 cookie/header。
            var transform = (ResponseTransform)Create(services, "SessionAffinity.AffinitizeTransform", policy);
            await transform.ApplyAsync(new ResponseTransformContext { HttpContext = matching, CancellationToken = CancellationToken.None });
            Assert.Empty(matching.Response.Headers);
            Assert.Empty(capture.Events);

            var missing = Context(cluster);
            SetRequestKey(missing, policy.Name, config.AffinityKeyName!, key);
            Assert.Equal(AffinityStatus.DestinationNotFound, policy.FindAffinitizedDestinations(missing, cluster, config, []).Status);
            var owner = Owner(policy.Name);
            AssertCodes(capture, owner + "_no_destinations");
            capture.Clear();
            var other = Cluster(services, client, new ClusterConfig { SessionAffinity = config }, "other");
            Assert.Equal(AffinityStatus.DestinationNotFound, policy.FindAffinitizedDestinations(missing, cluster, config, other.DestinationsState.AllDestinations).Status);
            AssertCodes(capture, owner + "_destination_missing");
            capture.Clear();

            var cancelled = Context(cluster, target);
            cancelled.RequestAborted = new CancellationToken(true);
            await transform.ApplyAsync(new ResponseTransformContext { HttpContext = cancelled });
            Assert.Empty(cancelled.Response.Headers);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await policy.FindAffinitizedDestinationsAsync(cancelled, cluster, config, all, cancelled.RequestAborted));
            Assert.Empty(capture.Events);
        }
        Assert.Empty(clock.Timers);
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task MalformedKeysAndMultipleHeaders_ShouldKeepExtractionFailureAndFailurePolicies(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "gateway");
        using var scope = capture.SensitiveScope();
        using var services = BuildServices(capture, new ControlledTime());
        using var client = NoSendClient();
        foreach (var policyName in new[] { "Cookie", "CustomHeader" })
        foreach (var failurePolicy in new[] { "Redistribute", "Return503Error" })
        {
            var config = new SessionAffinityConfig { Enabled = true, Policy = policyName, FailurePolicy = failurePolicy, AffinityKeyName = "key" };
            var cluster = Cluster(services, client, new ClusterConfig { SessionAffinity = config }, HttpBoundaryCapture.Secret);
            var calls = 0;
            var middleware = Middleware(services, "SessionAffinity.SessionAffinityMiddleware", _ => { calls++; return Task.CompletedTask; });
            foreach (var multiValue in policyName == "CustomHeader" ? new[] { false, true } : new[] { false })
            {
                var context = Context(cluster);
                SetRequestKey(context, policyName, "key", "not-base64!");
                if (multiValue) context.Request.Headers["key"] = new StringValues(["one", "two"]);
                var original = context.Features.Get<IReverseProxyFeature>()!.AvailableDestinations;
                await middleware(context);
                Assert.Equal(failurePolicy == "Redistribute" ? 200 : 503, context.Response.StatusCode);
                Assert.Equal(failurePolicy == "Redistribute" ? 1 : 0, calls);
                Assert.Same(original, context.Features.Get<IReverseProxyFeature>()!.AvailableDestinations);
                AssertCodes(capture, Owner(policyName) + (multiValue ? "_multiple_values" : "_decrypt_failed"),
                    failurePolicy == "Redistribute" ? "proxy.affinity_failure_handled" : "proxy.affinity_rejected");
                calls = 0;
                capture.Clear();
            }
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task DisabledMissingAndMatchedAffinity_ShouldKeepPipelineAndExceptionPropagation(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "gateway");
        using var scope = capture.SensitiveScope();
        using var services = BuildServices(capture, new ControlledTime());
        using var client = NoSendClient();
        var config = new SessionAffinityConfig { Enabled = true, AffinityKeyName = "key" }; // 默认 HashCookie。
        var cluster = Cluster(services, client, new ClusterConfig { SessionAffinity = config }, "one", "two");
        var target = cluster.Destinations["two"];
        var policy = services.GetServices<ISessionAffinityPolicy>().Single(p => p.Name == "HashCookie");
        var response = Context(cluster);
        policy.AffinitizeResponse(response, cluster, config, target);
        var context = Context(cluster);
        SetRequestKey(context, policy.Name, "key", ReadResponseKey(response, policy.Name));
        var calls = 0;
        var middleware = Middleware(services, "SessionAffinity.SessionAffinityMiddleware", ctx =>
        {
            calls++;
            Assert.Same(target, Assert.Single(ctx.Features.Get<IReverseProxyFeature>()!.AvailableDestinations));
            return Task.CompletedTask;
        });
        await middleware(context);
        Assert.Equal(1, calls);
        Assert.Empty(capture.Events);

        var failure = new InvalidOperationException(HttpBoundaryCapture.Secret);
        middleware = Middleware(services, "SessionAffinity.SessionAffinityMiddleware", _ => throw failure);
        foreach (var enabled in new[] { true, false })
        {
            var unbound = Cluster(services, client, new ClusterConfig { SessionAffinity = config with { Enabled = enabled } }, "one");
            Assert.Same(failure, await Record.ExceptionAsync(() => middleware(Context(unbound))));
            Assert.Empty(capture.Events);
        }
        var disabled = Cluster(services, client, new ClusterConfig { SessionAffinity = config with { Enabled = false } }, "one");
        var disabledContext = Context(disabled, disabled.Destinations["one"]);
        var transform = (ResponseTransform)Create(services, "SessionAffinity.AffinitizeTransform", policy);
        await transform.ApplyAsync(new ResponseTransformContext { HttpContext = disabledContext });
        Assert.Empty(disabledContext.Response.Headers);
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task ProtectionFailure_ShouldKeepDecryptionConsumptionAndResponseException(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "gateway");
        using var scope = capture.SensitiveScope();
        var failure = new InvalidOperationException(HttpBoundaryCapture.Secret);
        using var services = BuildServices(capture, new ControlledTime(), s =>
        {
            s.RemoveAll<IDataProtectionProvider>();
            s.AddSingleton<IDataProtectionProvider>(new FailingProtector(failure));
        });
        using var client = NoSendClient();
        foreach (var name in new[] { "Cookie", "CustomHeader" })
        {
            var policy = services.GetServices<ISessionAffinityPolicy>().Single(p => p.Name == name);
            var config = new SessionAffinityConfig { Enabled = true, Policy = name, AffinityKeyName = "key" };
            var cluster = Cluster(services, client, new ClusterConfig { SessionAffinity = config }, HttpBoundaryCapture.Secret);
            var context = Context(cluster);
            SetRequestKey(context, name, "key", "YQ==");
            Assert.Equal(AffinityStatus.AffinityKeyExtractionFailed, policy.FindAffinitizedDestinations(context, cluster, config, cluster.DestinationsState.AllDestinations).Status);
            AssertCodes(capture, Owner(name) + "_decrypt_failed");
            capture.Clear();
            var transform = (ResponseTransform)Create(services, "SessionAffinity.AffinitizeTransform", policy);
            Assert.Same(failure, await Record.ExceptionAsync(async () => await transform.ApplyAsync(new ResponseTransformContext
            { HttpContext = Context(cluster, cluster.Destinations[HttpBoundaryCapture.Secret]) })));
            Assert.Empty(capture.Events);
        }
    }

    private static string Owner(string name) => "proxy.affinity_" + (name switch { "Cookie" => "cookie", "CustomHeader" => "header", "HashCookie" => "hash", "ArrCookie" => "arr", _ => throw new InvalidOperationException(name) });
    private static string ReadResponseKey(HttpContext context, string name) => name == "CustomHeader"
        ? context.Response.Headers.Single().Value.ToString()
        : Uri.UnescapeDataString(context.Response.Headers.SetCookie.ToString().Split(';')[0].Split('=', 2)[1]);
    private static void SetRequestKey(HttpContext context, string name, string keyName, string value)
    {
        if (name == "CustomHeader") context.Request.Headers[keyName] = value;
        else context.Request.Headers.Cookie = keyName + "=" + Uri.EscapeDataString(value);
    }
    private sealed class FailingProtector(Exception failure) : IDataProtectionProvider, IDataProtector
    {
        public IDataProtector CreateProtector(string purpose) => this;
        public byte[] Protect(byte[] plaintext) => throw failure;
        public byte[] Unprotect(byte[] protectedData) => throw failure;
    }
}
