using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Health;
using Yarp.ReverseProxy.Model;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

internal static class YarpConditionalTestSupport
{
    internal static Type FrameworkType(string name) => typeof(IHttpForwarder).Assembly.GetType("Yarp.ReverseProxy." + name, true)!;
    internal static ServiceProvider BuildServices(HttpBoundaryCapture capture, ControlledTime clock, Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(capture.Factory);
        services.AddSingleton<TimeProvider>(clock);
        services.AddReverseProxy();
        services.RemoveAll<IDataProtectionProvider>();
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        // 仅解析测试使用的策略 / updater / monitor；不解析 manager、宿主或真实 HTTP 客户端工厂。
        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }
    internal static object Create(IServiceProvider services, string name, params object[] args) => ActivatorUtilities.CreateInstance(services, FrameworkType(name), args);
    internal static object? Call(object instance, string name, params object?[] args)
    {
        try { return instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.Invoke(instance, args); }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        { ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); throw; }
    }
    internal static RequestDelegate Middleware(IServiceProvider services, string name, RequestDelegate next)
    {
        var instance = Create(services, name, next);
        return context => (Task)Call(instance, "Invoke", context)!;
    }
    internal static ClusterState Cluster(IServiceProvider services, HttpMessageInvoker client, ClusterConfig config, params string[] destinationIds)
    {
        var cluster = new ClusterState(HttpBoundaryCapture.Secret, new ClusterModel(config with { ClusterId = HttpBoundaryCapture.Secret }, client));
        foreach (var id in destinationIds)
        {
            var destination = new DestinationState(id);
            typeof(DestinationState).GetProperty("Model")!.SetValue(destination, new DestinationModel(new DestinationConfig { Address = "https://example.invalid/" + HttpBoundaryCapture.Secret }));
            Assert.True(cluster.Destinations.TryAdd(id, destination));
        }
        services.GetRequiredService<IClusterDestinationsUpdater>().UpdateAllDestinations(cluster);
        return cluster;
    }
    internal static void SetClusterConfig(ClusterState cluster, ClusterConfig config) =>
        typeof(ClusterState).GetProperty("Model")!.SetValue(cluster, new ClusterModel(config, cluster.Model.HttpClient));
    internal static DefaultHttpContext Context(ClusterState cluster, DestinationState? proxied = null)
    {
        var route = new RouteModel(new RouteConfig { RouteId = HttpBoundaryCapture.Secret }, cluster, HttpTransformer.Default);
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("example.invalid");
        context.Request.Path = "/" + HttpBoundaryCapture.Secret;
        context.Response.Body = new MemoryStream();
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(route), "test"));
        context.Features.Set<IReverseProxyFeature>(new ReverseProxyFeature
        {
            Route = route, Cluster = cluster.Model, AllDestinations = cluster.DestinationsState.AllDestinations,
            AvailableDestinations = cluster.DestinationsState.AvailableDestinations, ProxiedDestination = proxied
        });
        return context;
    }
    internal static HttpMessageInvoker NoSendClient() => new(new ControlledHttpHandler((_, _) => throw new InvalidOperationException("No HTTP send expected")));
    internal static void AssertCodes(HttpBoundaryCapture capture, params string[] expected)
    {
        var serious = YarpConditionalFrameworkEvents.All.Where(e => e.Level >= LogLevel.Warning).Select(e => e.Code).ToHashSet();
        Assert.Equal(capture.Candidate && capture.Environment == "Production" ? expected.Where(serious.Contains) : expected, capture.Codes);
        TransportFrameworkLoggingTests.AssertSafe(capture);
    }

    // 手动推进时间和触发调度器；不创建系统 timer、不等待真实周期。
    internal sealed class ControlledTime : TimeProvider
    {
        private long _timestamp = TimeSpan.TicksPerSecond;
        internal readonly List<ControlledTimer> Timers = [];
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _timestamp;
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 10, 11, 0, 0, 0, TimeSpan.Zero).AddTicks(_timestamp);
        internal void Advance(TimeSpan delta) => _timestamp += delta.Ticks;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ControlledTimer(callback, state, dueTime, period);
            Timers.Add(timer);
            return timer;
        }
    }
    internal sealed class ControlledTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) : ITimer
    {
        internal bool Disposed;
        internal TimeSpan Due = dueTime;
        internal TimeSpan Period = period;
        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(ControlledTimer));
            Due = dueTime;
            Period = period;
            return true;
        }
        internal Task FireAsync()
        {
            Assert.False(Disposed);
            Assert.NotEqual(Timeout.InfiniteTimeSpan, Due);
            Assert.NotNull(callback);
            // 框架 timer 委托丢弃 Task；直接等待该委托调用的真实异步方法，验证结束前不留后台任务。
            var method = state!.GetType().GetMethod("TimerCallback", BindingFlags.Static | BindingFlags.NonPublic)!;
            Assert.NotNull(method);
            return (Task)method.Invoke(null, [state])!;
        }
        public void Dispose() => Disposed = true;
        public ValueTask DisposeAsync() { Dispose(); return default; }
    }
}
