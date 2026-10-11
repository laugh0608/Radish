using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Health;
using Yarp.ReverseProxy.Model;
using Xunit;
using static Radish.Api.Tests.ErrorHandling.YarpConditionalTestSupport;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class YarpActiveHealthBehaviorTests
{
    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task RealProbeAndConsecutivePolicy_ShouldRetainResponseThresholdAndRecovery(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "gateway");
        using var scope = capture.SensitiveScope();
        var clock = new ControlledTime();
        using var services = BuildServices(capture, clock);
        var status = HttpStatusCode.InternalServerError;
        var calls = 0;
        var contents = new List<TrackedContent>();
        using var client = new HttpMessageInvoker(new ControlledHttpHandler((request, _) =>
        {
            calls++;
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("/" + HttpBoundaryCapture.Secret + "/probe", request.RequestUri!.AbsolutePath);
            Assert.Equal("?secret=" + HttpBoundaryCapture.Secret, request.RequestUri.Query);
            Assert.Equal(HttpVersion.Version20, request.Version);
            var content = new TrackedContent();
            contents.Add(content);
            return Task.FromResult(new HttpResponseMessage(status) { Content = content });
        }));
        var cluster = Cluster(services, client, Config(), HttpBoundaryCapture.Secret);
        var destination = cluster.Destinations[HttpBoundaryCapture.Secret];
        var monitor = services.GetRequiredService<IActiveHealthCheckMonitor>();
        await monitor.CheckHealthAsync([cluster]);
        Assert.Equal(DestinationHealth.Unknown, destination.Health.Active); // 默认连续失败阈值为 2。
        AssertCodes(capture, "proxy.health_probe_started", "proxy.health_probe_sending", "proxy.health_probe_completed", "proxy.health_probe_stopped");
        capture.Clear();
        await monitor.CheckHealthAsync([cluster]);
        Assert.Equal(DestinationHealth.Unhealthy, destination.Health.Active);
        AssertCodes(capture, "proxy.health_probe_started", "proxy.health_probe_sending", "proxy.health_probe_completed", "proxy.health_state_active_unhealthy", "proxy.health_probe_stopped");
        // 默认 HealthyOrPanic 在全部不健康时仍返回全部目标。
        Assert.Same(destination, Assert.Single(cluster.DestinationsState.AvailableDestinations));
        capture.Clear();
        status = HttpStatusCode.OK;
        await monitor.CheckHealthAsync([cluster]);
        Assert.Equal(DestinationHealth.Healthy, destination.Health.Active);
        AssertCodes(capture, "proxy.health_probe_started", "proxy.health_probe_sending", "proxy.health_probe_completed", "proxy.health_state_active_changed", "proxy.health_probe_stopped");
        capture.Clear();
        status = HttpStatusCode.InternalServerError;
        await monitor.CheckHealthAsync([cluster]);
        Assert.Equal(DestinationHealth.Healthy, destination.Health.Active); // 成功重置失败计数。
        AssertCodes(capture, "proxy.health_probe_started", "proxy.health_probe_sending", "proxy.health_probe_completed", "proxy.health_probe_stopped");
        Assert.Equal(4, calls);
        Assert.All(contents, c => Assert.True(c.Disposed));
        Assert.True(monitor.InitialProbeCompleted);
        Assert.Empty(clock.Timers);
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task ConstructionSendAndCancellationFailures_ShouldKeepOriginalProbeResults(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "gateway");
        using var scope = capture.SensitiveScope();
        var factory = new ProbeFactory();
        var policy = new ProbePolicy();
        using var services = BuildServices(capture, new ControlledTime(), s =>
        {
            s.RemoveAll<IProbingRequestFactory>(); s.AddSingleton<IProbingRequestFactory>(factory);
            s.RemoveAll<IActiveHealthCheckPolicy>(); s.AddSingleton<IActiveHealthCheckPolicy>(policy);
        });
        Exception failure = new IOException(HttpBoundaryCapture.Secret);
        var sends = 0;
        using var client = new HttpMessageInvoker(new ControlledHttpHandler((_, _) => { sends++; return Task.FromException<HttpResponseMessage>(failure); }));
        var cluster = Cluster(services, client, Config(), HttpBoundaryCapture.Secret);
        var monitor = services.GetRequiredService<IActiveHealthCheckMonitor>();
        factory.Failure = failure;
        await monitor.CheckHealthAsync([cluster]);
        Assert.Equal(0, sends);
        Assert.Same(failure, Assert.Single(policy.Results!).Exception);
        Assert.Null(Assert.Single(policy.Results!).Response);
        AssertCodes(capture, "proxy.health_probe_started", "proxy.health_probe_construction_failed", "proxy.health_probe_stopped");
        capture.Clear();
        factory.Failure = null;
        foreach (var error in new Exception[] { failure, new OperationCanceledException(HttpBoundaryCapture.Secret) })
        {
            failure = error;
            await monitor.CheckHealthAsync([cluster]);
            Assert.Same(error, Assert.Single(policy.Results!).Exception);
            AssertCodes(capture, "proxy.health_probe_started", "proxy.health_probe_sending", "proxy.health_probe_failed", "proxy.health_probe_stopped");
            capture.Clear();
        }
        Assert.Equal(2, sends);
        // 受控 handler 的取消异常只证明框架消费分支，不证明真实 Timeout 到期。
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task PolicyCleanupAndEnumerationFailures_ShouldKeepConsumptionAndInitialCompletion(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "gateway");
        using var scope = capture.SensitiveScope();
        var policy = new ProbePolicy { Failure = new IOException(HttpBoundaryCapture.Secret) };
        using var services = BuildServices(capture, new ControlledTime(), s =>
        { s.RemoveAll<IActiveHealthCheckPolicy>(); s.AddSingleton<IActiveHealthCheckPolicy>(policy); });
        var content = new TrackedContent { Failure = new InvalidOperationException(HttpBoundaryCapture.Secret) };
        using var client = new HttpMessageInvoker(new ControlledHttpHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content })));
        var cluster = Cluster(services, client, Config(), HttpBoundaryCapture.Secret);
        var monitor = services.GetRequiredService<IActiveHealthCheckMonitor>();
        await monitor.CheckHealthAsync([cluster]);
        Assert.True(content.Disposed);
        AssertCodes(capture, "proxy.health_probe_started", "proxy.health_probe_sending", "proxy.health_probe_completed", "proxy.health_probe_cluster_failed", "proxy.health_probe_cleanup_failed", "proxy.health_probe_stopped");
        capture.Clear();
        await monitor.CheckHealthAsync(BrokenClusters());
        Assert.True(monitor.InitialProbeCompleted);
        AssertCodes(capture, "proxy.health_probe_all_failed");
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task DisabledAndScheduledClusters_ShouldKeepRegistrationChangesAndDisposeTimers(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "gateway");
        using var scope = capture.SensitiveScope();
        var clock = new ControlledTime();
        var services = BuildServices(capture, clock);
        var calls = 0;
        using var client = new HttpMessageInvoker(new ControlledHttpHandler((_, _) => { calls++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)); }));
        try
        {
            var cluster = Cluster(services, client, Config(false), HttpBoundaryCapture.Secret);
            var monitor = services.GetRequiredService<IActiveHealthCheckMonitor>();
            var listener = (IClusterChangeListener)monitor;
            listener.OnClusterAdded(cluster);
            await monitor.CheckHealthAsync([cluster]);
            Assert.True(monitor.InitialProbeCompleted);
            Assert.Equal(0, calls);
            Assert.Empty(clock.Timers);
            Assert.Empty(capture.Events);

            SetClusterConfig(cluster, Config());
            listener.OnClusterChanged(cluster);
            var timer = Assert.Single(clock.Timers);
            Assert.Equal(TimeSpan.FromSeconds(15), timer.Due);
            await timer.FireAsync();
            Assert.Equal(1, calls);
            AssertCodes(capture, "proxy.health_probe_started", "proxy.health_probe_sending", "proxy.health_probe_completed", "proxy.health_state_active_changed", "proxy.health_probe_stopped");
            capture.Clear();
            SetClusterConfig(cluster, Config() with { HealthCheck = new HealthCheckConfig { Active = new ActiveHealthCheckConfig { Enabled = true, Interval = TimeSpan.FromSeconds(23), Timeout = Timeout.InfiniteTimeSpan } } });
            listener.OnClusterChanged(cluster);
            Assert.Equal(TimeSpan.FromSeconds(23), timer.Due);
            SetClusterConfig(cluster, Config(false));
            listener.OnClusterChanged(cluster);
            Assert.True(timer.Disposed);
            SetClusterConfig(cluster, Config());
            listener.OnClusterAdded(cluster);
            listener.OnClusterRemoved(cluster);
            Assert.True(clock.Timers.Last().Disposed);
            listener.OnClusterAdded(cluster); // 最后一个 entry 由 monitor.Dispose 清理。
            Assert.False(clock.Timers.Last().Disposed);
            Assert.Empty(capture.Events);
        }
        finally { services.Dispose(); }
        Assert.All(clock.Timers, t => Assert.True(t.Disposed));
    }

    internal static ClusterConfig Config(bool enabled = true) => new()
    {
        ClusterId = HttpBoundaryCapture.Secret,
        HealthCheck = new HealthCheckConfig { Active = new ActiveHealthCheckConfig
        { Enabled = enabled, Path = "/probe", Query = "?secret=" + HttpBoundaryCapture.Secret, Timeout = Timeout.InfiniteTimeSpan } }
    };
    private static IEnumerable<ClusterState> BrokenClusters()
    {
        yield return null!; // 外层 CheckHealthAsync 原有 catch 消费读取 cluster 时的错误。
    }
    private sealed class ProbeFactory : IProbingRequestFactory
    {
        internal Exception? Failure;
        public HttpRequestMessage CreateRequest(ClusterModel cluster, DestinationModel destination) => Failure != null ? throw Failure : new(HttpMethod.Get, "https://example.invalid/" + HttpBoundaryCapture.Secret);
    }
    private sealed class ProbePolicy : IActiveHealthCheckPolicy
    {
        public string Name => "ConsecutiveFailures";
        internal IReadOnlyList<DestinationProbingResult>? Results;
        internal Exception? Failure;
        public void ProbingCompleted(ClusterState cluster, IReadOnlyList<DestinationProbingResult> probingResults)
        { Results = probingResults; if (Failure != null) throw Failure; }
    }
    private sealed class TrackedContent : StringContent
    {
        internal bool Disposed;
        internal Exception? Failure;
        internal TrackedContent() : base("response") { }
        protected override void Dispose(bool disposing)
        { Disposed = true; base.Dispose(disposing); if (Failure != null) throw Failure; }
    }
}
