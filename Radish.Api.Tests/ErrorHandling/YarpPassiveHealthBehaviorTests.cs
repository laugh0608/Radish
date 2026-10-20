using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Health;
using Yarp.ReverseProxy.Model;
using Xunit;
using static Radish.Api.Tests.ErrorHandling.YarpConditionalTestSupport;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class YarpPassiveHealthBehaviorTests
{
    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task PassiveUpdater_ShouldRetainStateChangesReactivationAndTimerCleanup(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "gateway");
        using var scope = capture.SensitiveScope();
        var clock = new ControlledTime();
        var services = BuildServices(capture, clock);
        using var client = NoSendClient();
        try
        {
            var cluster = Cluster(services, client, Config(), HttpBoundaryCapture.Secret);
            var destination = cluster.Destinations[HttpBoundaryCapture.Secret];
            var updater = services.GetRequiredService<IDestinationHealthUpdater>();
            var period = TimeSpan.FromSeconds(60);
            await SetPassive(updater, cluster, destination, DestinationHealth.Unhealthy, period);
            Assert.Equal(DestinationHealth.Unhealthy, destination.Health.Passive);
            Assert.Empty(cluster.DestinationsState.AvailableDestinations); // 显式 HealthyAndUnknown。
            AssertCodes(capture, "proxy.health_state_reactivation_scheduled");
            capture.Clear();
            var timer = Assert.Single(clock.Timers);
            Assert.Equal(period, timer.Due);
            await SetPassive(updater, cluster, destination, DestinationHealth.Unhealthy, period);
            Assert.Single(clock.Timers);
            Assert.Empty(capture.Events);
            clock.Advance(period);
            await timer.FireAsync();
            Assert.True(timer.Disposed);
            Assert.Equal(DestinationHealth.Unknown, destination.Health.Passive);
            Assert.Same(destination, Assert.Single(cluster.DestinationsState.AvailableDestinations));
            AssertCodes(capture, "proxy.health_state_passive_reset");
            capture.Clear();

            await SetPassive(updater, cluster, destination, DestinationHealth.Unhealthy, period);
            capture.Clear();
            await SetPassive(updater, cluster, destination, DestinationHealth.Healthy, period);
            await clock.Timers.Last().FireAsync(); // 提前恢复后，已排定的回调不覆盖 Healthy。
            Assert.Equal(DestinationHealth.Healthy, destination.Health.Passive);
            Assert.Empty(capture.Events);
            await SetPassive(updater, cluster, destination, DestinationHealth.Unhealthy, period);
            Assert.False(clock.Timers.Last().Disposed);
        }
        finally { services.Dispose(); }
        Assert.All(clock.Timers, t => Assert.True(t.Disposed));
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public void ActiveUpdater_ShouldKeepIndependentStatesNoOpUpdatesAndAvailabilityPolicy(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "gateway");
        using var scope = capture.SensitiveScope();
        using var services = BuildServices(capture, new ControlledTime());
        using var client = NoSendClient();
        var config = Config() with { HealthCheck = new HealthCheckConfig
        { Active = new ActiveHealthCheckConfig { Enabled = true }, Passive = new PassiveHealthCheckConfig { Enabled = true }, AvailableDestinationsPolicy = "HealthyAndUnknown" } };
        var cluster = Cluster(services, client, config, HttpBoundaryCapture.Secret);
        var destination = cluster.Destinations[HttpBoundaryCapture.Secret];
        var updater = services.GetRequiredService<IDestinationHealthUpdater>();
        updater.SetActive(cluster, [new NewActiveDestinationHealth(destination, DestinationHealth.Unhealthy)]);
        Assert.Empty(cluster.DestinationsState.AvailableDestinations);
        Assert.Equal(DestinationHealth.Unknown, destination.Health.Passive);
        AssertCodes(capture, "proxy.health_state_active_unhealthy");
        capture.Clear();
        var state = cluster.DestinationsState;
        updater.SetActive(cluster, [new NewActiveDestinationHealth(destination, DestinationHealth.Unhealthy)]);
        Assert.Same(state, cluster.DestinationsState);
        Assert.Empty(capture.Events);
        updater.SetActive(cluster, [new NewActiveDestinationHealth(destination, DestinationHealth.Healthy)]);
        Assert.Same(destination, Assert.Single(cluster.DestinationsState.AvailableDestinations));
        AssertCodes(capture, "proxy.health_state_active_changed");
        capture.Clear();
        updater.SetActive(cluster, [new NewActiveDestinationHealth(destination, DestinationHealth.Unknown)]);
        AssertCodes(capture, "proxy.health_state_active_changed");
        capture.Clear();
        updater.SetActive(cluster, [new NewActiveDestinationHealth(destination, DestinationHealth.Unhealthy)]);
        capture.Clear();
        SetClusterConfig(cluster, config with { HealthCheck = config.HealthCheck! with { AvailableDestinationsPolicy = null } });
        services.GetRequiredService<IClusterDestinationsUpdater>().UpdateAvailableDestinations(cluster);
        Assert.Same(destination, Assert.Single(cluster.DestinationsState.AvailableDestinations)); // 默认 HealthyOrPanic。
        SetClusterConfig(cluster, config with { HealthCheck = null });
        services.GetRequiredService<IClusterDestinationsUpdater>().UpdateAvailableDestinations(cluster);
        Assert.Same(destination, Assert.Single(cluster.DestinationsState.AvailableDestinations)); // 未启用时忽略健康状态。
        Assert.Empty(capture.Events);
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task PassiveMiddlewareAndPolicy_ShouldKeepErrorClassificationThresholdWindowAndPropagation(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "gateway");
        using var scope = capture.SensitiveScope();
        var clock = new ControlledTime();
        var updates = new CapturedUpdates();
        using var services = BuildServices(capture, clock, s =>
        { s.RemoveAll<IDestinationHealthUpdater>(); s.AddSingleton<IDestinationHealthUpdater>(updates); });
        using var client = NoSendClient();
        var config = Config();
        var cluster = Cluster(services, client, config, HttpBoundaryCapture.Secret);
        var destination = cluster.Destinations[HttpBoundaryCapture.Secret];
        var policy = Assert.Single(services.GetServices<IPassiveHealthCheckPolicy>());
        var context = Context(cluster, destination);
        context.Response.StatusCode = 500;
        for (var i = 0; i < 10; i++) policy.RequestProxied(context, cluster, destination);
        Assert.Equal(DestinationHealth.Healthy, updates.State); // HTTP 500 没有转发错误 feature，不是传输失败。
        // 新 destination 从空历史开始，默认最小样本 10、失败率阈值 0.3。
        cluster = Cluster(services, client, config, "fresh"); destination = cluster.Destinations["fresh"];
        context = Context(cluster, destination);
        context.Features.Set<IForwarderErrorFeature>(new ErrorFeature(ForwarderError.Request, new Exception(HttpBoundaryCapture.Secret)));
        for (var i = 0; i < 9; i++) policy.RequestProxied(context, cluster, destination);
        Assert.Equal(DestinationHealth.Healthy, updates.State);
        policy.RequestProxied(context, cluster, destination);
        Assert.Equal(DestinationHealth.Unhealthy, updates.State);
        Assert.Equal(TimeSpan.FromSeconds(60), updates.Period);
        clock.Advance(TimeSpan.FromSeconds(2));
        context.Features.Set<IForwarderErrorFeature>(null);
        policy.RequestProxied(context, cluster, destination); // 将前一组累积记录移入窗口历史。
        clock.Advance(TimeSpan.FromSeconds(61));
        policy.RequestProxied(context, cluster, destination);
        Assert.Equal(DestinationHealth.Healthy, updates.State);

        foreach (var failures in new[] { 2, 3 })
        {
            var boundary = Cluster(services, client, config, "rate-boundary");
            var ctx = Context(boundary, boundary.Destinations["rate-boundary"]);
            for (var i = 0; i < 10; i++)
            {
                ctx.Features.Set<IForwarderErrorFeature>(i < failures ? new ErrorFeature(ForwarderError.Request, null) : null);
                policy.RequestProxied(ctx, boundary, boundary.Destinations["rate-boundary"]);
            }
            Assert.Equal(failures == 2 ? DestinationHealth.Healthy : DestinationHealth.Unhealthy, updates.State);
        }

        foreach (var error in Enum.GetValues<ForwarderError>())
        foreach (var cancelled in new[] { false, true })
        {
            var immediate = Config() with { Metadata = new System.Collections.Generic.Dictionary<string, string> { ["TransportFailureRateHealthPolicy.RateLimit"] = "0.1" } };
            var item = Cluster(services, client, immediate, "classification");
            var ctx = Context(item, item.Destinations["classification"]);
            ctx.RequestAborted = cancelled ? new CancellationToken(true) : CancellationToken.None;
            ctx.Features.Set<IForwarderErrorFeature>(new ErrorFeature(error, new Exception(HttpBoundaryCapture.Secret)));
            for (var i = 0; i < 10; i++) policy.RequestProxied(ctx, item, item.Destinations["classification"]);
            var failed = !cancelled && error is ForwarderError.Request or ForwarderError.RequestTimedOut or ForwarderError.RequestBodyDestination
                or ForwarderError.ResponseBodyDestination or ForwarderError.UpgradeRequestDestination or ForwarderError.UpgradeResponseDestination;
            Assert.Equal(failed ? DestinationHealth.Unhealthy : DestinationHealth.Healthy, updates.State);
        }
        var calls = updates.Calls;
        var failure = new InvalidOperationException(HttpBoundaryCapture.Secret);
        var middleware = Middleware(services, "Health.PassiveHealthCheckMiddleware", _ => throw failure);
        Assert.Same(failure, await Record.ExceptionAsync(() => middleware(context)));
        Assert.Equal(calls, updates.Calls);
        middleware = Middleware(services, "Health.PassiveHealthCheckMiddleware", _ => Task.CompletedTask);
        await middleware(Context(cluster)); // 未选中 destination。
        var disabled = Cluster(services, client, config with { HealthCheck = null }, "disabled");
        await middleware(Context(disabled, disabled.Destinations["disabled"]));
        Assert.Equal(calls, updates.Calls);
        await middleware(Context(cluster, destination));
        Assert.Equal(calls + 1, updates.Calls);
        Assert.Empty(capture.Events); // 中间件和 policy 本身没有独立 logger。
        Assert.Empty(clock.Timers);
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task UpdaterFailure_ShouldPreserveAlreadyChangedStateAndExistingExceptionBoundaries(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "gateway");
        using var scope = capture.SensitiveScope();
        using var baseServices = BuildServices(capture, new ControlledTime());
        using var client = NoSendClient();
        var cluster = Cluster(baseServices, client, Config(), HttpBoundaryCapture.Secret);
        var destination = cluster.Destinations[HttpBoundaryCapture.Secret];
        var failure = new InvalidOperationException(HttpBoundaryCapture.Secret);
        using var services = BuildServices(capture, new ControlledTime(), s =>
        { s.RemoveAll<IClusterDestinationsUpdater>(); s.AddSingleton<IClusterDestinationsUpdater>(new FailingClusterUpdater(failure)); });
        var updater = services.GetRequiredService<IDestinationHealthUpdater>();
        Assert.Same(failure, Record.Exception(() => updater.SetActive(cluster, [new NewActiveDestinationHealth(destination, DestinationHealth.Unhealthy)])));
        Assert.Equal(DestinationHealth.Unhealthy, destination.Health.Active);
        AssertCodes(capture, "proxy.health_state_active_unhealthy");
        capture.Clear();
        Assert.Same(failure, await Record.ExceptionAsync(() => SetPassive(updater, cluster, destination, DestinationHealth.Unhealthy, TimeSpan.FromSeconds(60))));
        Assert.Equal(DestinationHealth.Unhealthy, destination.Health.Passive);
        AssertCodes(capture, "proxy.health_state_reactivation_scheduled");
        // 公共 SetPassive 丢弃这个 Task；测试等待内部方法，不改变或扩大产品异常传播承诺。
    }

    private static Task SetPassive(IDestinationHealthUpdater updater, ClusterState cluster, DestinationState destination, DestinationHealth health, TimeSpan period) =>
        (Task)Call(updater, "SetPassiveAsync", cluster, destination, health, period)!;
    private static ClusterConfig Config() => new()
    {
        ClusterId = HttpBoundaryCapture.Secret,
        HealthCheck = new HealthCheckConfig { Passive = new PassiveHealthCheckConfig { Enabled = true, ReactivationPeriod = TimeSpan.FromSeconds(5) }, AvailableDestinationsPolicy = "HealthyAndUnknown" }
    };
    private sealed record ErrorFeature(ForwarderError Error, Exception? Exception) : IForwarderErrorFeature;
    private sealed class CapturedUpdates : IDestinationHealthUpdater
    {
        internal int Calls;
        internal DestinationHealth State;
        internal TimeSpan Period;
        public void SetActive(ClusterState cluster, System.Collections.Generic.IEnumerable<NewActiveDestinationHealth> newHealthPairs) => throw new InvalidOperationException("No active updates expected");
        public void SetPassive(ClusterState cluster, DestinationState destination, DestinationHealth newHealth, TimeSpan reactivationPeriod)
        { Calls++; State = newHealth; Period = reactivationPeriod; }
    }
    private sealed class FailingClusterUpdater(Exception failure) : IClusterDestinationsUpdater
    {
        public void UpdateAllDestinations(ClusterState cluster) => throw failure;
        public void UpdateAvailableDestinations(ClusterState cluster) => throw failure;
    }
}
