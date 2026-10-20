using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Health;
using Yarp.ReverseProxy.Model;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

/// <summary>真实配置 manager / validator，配置与变更通知可控；客户端和健康探测均为内存替身。</summary>
internal sealed class YarpConfigurationTestSupport : IDisposable
{
    internal readonly ServiceProvider Services;
    internal readonly ControlledProxyConfigProvider Provider = new();
    internal readonly ConfigurationCallbacks Callbacks = new();
    internal readonly NoProbeMonitor Monitor = new();
    internal readonly NoSendClientFactory Clients = new();
    internal readonly object Manager;
    internal EndpointDataSource DataSource => (EndpointDataSource)Manager;
    private readonly Type _managerType;

    internal YarpConfigurationTestSupport(HttpBoundaryCapture capture)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(capture.Factory);
        // 在 YARP 注册之前提供替身，阻止 ActiveHealthCheckMonitor 注册和后台 scheduler 的构造。
        services.AddSingleton<IActiveHealthCheckMonitor>(Monitor);
        services.AddReverseProxy();
        // 亲和策略的验证依赖 DataProtection；使用临时内存密钥，避免访问用户密钥目录。
        services.RemoveAll<IDataProtectionProvider>();
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        services.RemoveAll<IClusterChangeListener>(); // 包括平台 HttpSys delegation，测试仅保留可控监听器。
        services.AddSingleton<IClusterChangeListener>(Callbacks);
        services.AddSingleton<IConfigChangeListener>(Callbacks);
        services.RemoveAll<IForwarderHttpClientFactory>();
        services.AddSingleton<IForwarderHttpClientFactory>(Clients);
        services.AddSingleton<IProxyConfigProvider>(Provider);
        Services = services.BuildServiceProvider();
        _managerType = typeof(IProxyConfigProvider).Assembly.GetType("Yarp.ReverseProxy.Management.ProxyConfigManager", true)!;
        Manager = Services.GetRequiredService(_managerType);
        var endpointType = typeof(IProxyConfigProvider).Assembly.GetType("Yarp.ReverseProxy.Routing.ProxyEndpointFactory", true)!;
        var endpointFactory = Services.GetRequiredService(endpointType);
        endpointType.GetMethod("SetProxyPipeline", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .Invoke(endpointFactory, [new RequestDelegate(_ => Task.CompletedTask)]);
        Assert.Same(Monitor, Services.GetRequiredService<IActiveHealthCheckMonitor>());
        Assert.Same(Callbacks, Assert.Single(Services.GetServices<IClusterChangeListener>()));
    }

    internal Task InitialLoad() => InvokeTask("InitialLoadAsync");
    internal Task Reload() => InvokeTask("ReloadConfigAsync");
    internal ClusterState[] Clusters() => ((IEnumerable<ClusterState>)_managerType.GetMethod("GetClusters")!.Invoke(Manager, null)!).ToArray();
    internal RouteModel[] Routes() => ((IEnumerable<RouteModel>)_managerType.GetMethod("GetRoutes")!.Invoke(Manager, null)!).ToArray();

    private Task InvokeTask(string name)
    {
        try { return (Task)_managerType.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(Manager, null)!; }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        { ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); throw; }
    }

    internal static ClusterConfig Cluster(string id = HttpBoundaryCapture.Secret, string address = "https://example.invalid/original") => new()
    {
        ClusterId = id,
        Destinations = new Dictionary<string, DestinationConfig> { ["destination-" + HttpBoundaryCapture.Secret] = new() { Address = address } }
    };
    internal static RouteConfig Route(string id = "route-" + HttpBoundaryCapture.Secret, int order = 0) => new()
    {
        RouteId = id, ClusterId = HttpBoundaryCapture.Secret, Order = order,
        Match = new RouteMatch { Path = "/" + HttpBoundaryCapture.Secret }
    };
    internal static ConfigurationSnapshot Snapshot(ClusterConfig? cluster = null, RouteConfig? route = null) =>
        new([route ?? Route()], [cluster ?? Cluster()]);

    internal static void AssertCodes(HttpBoundaryCapture capture, params string[] expected)
    {
        Assert.Equal(capture.Candidate && capture.Environment == "Production"
            ? expected.Where(code => code.EndsWith("_failed", StringComparison.Ordinal) || code == "proxy.config_validator_http10_detected") : expected, capture.Codes);
        TransportFrameworkLoggingTests.AssertSafe(capture);
    }

    public void Dispose()
    {
        // manager.Dispose 清除变更订阅和 CTS；其定时轮询不等待真实五分钟到期。
        Services.Dispose();
        Clients.Dispose();
    }

    internal sealed class ConfigurationSnapshot(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters) : IProxyConfig
    {
        public IReadOnlyList<RouteConfig> Routes { get; } = routes;
        public IReadOnlyList<ClusterConfig> Clusters { get; } = clusters;
        public IChangeToken ChangeToken => Token;
        internal readonly ControlledChangeToken Token = new();
    }

    internal sealed class ControlledProxyConfigProvider : IProxyConfigProvider
    {
        internal ConfigurationSnapshot Current = Snapshot();
        internal Exception? LoadFailure;
        internal int Loads;
        public IProxyConfig GetConfig()
        {
            Loads++;
            if (LoadFailure != null) throw LoadFailure;
            return Current;
        }
        internal void Replace(ConfigurationSnapshot next)
        {
            Current.Token.MarkChanged();
            Current = next;
        }
    }

    internal sealed class ControlledChangeToken : IChangeToken
    {
        public bool HasChanged { get; private set; }
        public bool ActiveChangeCallbacks => true;
        internal int Subscriptions;
        internal void MarkChanged() => HasChanged = true;
        public IDisposable RegisterChangeCallback(Action<object?> callback, object? state)
        {
            Subscriptions++;
            // 标记与执行分离：测试显式 await 真实 ReloadConfigAsync，避免 fire-and-forget 竞态。
            return new Subscription(this);
        }
        private sealed class Subscription(ControlledChangeToken owner) : IDisposable
        {
            private bool _disposed;
            public void Dispose()
            {
                if (_disposed) return;
                owner.Subscriptions--;
                _disposed = true;
            }
        }
    }

    internal sealed class ConfigurationCallbacks : IConfigChangeListener, IClusterChangeListener
    {
        internal Exception? LoadingFailure;
        internal Exception? ApplyingFailure;
        internal Exception? ThrowOnLoaded;
        internal Exception? ThrowOnApplied;
        internal int Applied;
        internal int ClusterChanges;
        public void ConfigurationLoadingFailed(IProxyConfigProvider provider, Exception exception) => LoadingFailure = exception;
        public void ConfigurationLoaded(IReadOnlyList<IProxyConfig> proxyConfigs)
        {
            if (ThrowOnLoaded != null) throw ThrowOnLoaded;
        }
        public void ConfigurationApplyingFailed(IReadOnlyList<IProxyConfig> proxyConfigs, Exception exception) => ApplyingFailure = exception;
        public void ConfigurationApplied(IReadOnlyList<IProxyConfig> proxyConfigs)
        {
            Applied++;
            if (ThrowOnApplied != null) throw ThrowOnApplied;
        }
        public void OnClusterAdded(ClusterState cluster) => ClusterChanges++;
        public void OnClusterChanged(ClusterState cluster) => ClusterChanges++;
        public void OnClusterRemoved(ClusterState cluster) => ClusterChanges++;
    }

    internal sealed class NoProbeMonitor : IActiveHealthCheckMonitor
    {
        public bool InitialProbeCompleted => Calls > 0;
        internal int Calls;
        public Task CheckHealthAsync(IEnumerable<ClusterState> clusters)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    internal sealed class NoSendClientFactory : IForwarderHttpClientFactory, IDisposable
    {
        internal Exception? Failure;
        internal int Calls;
        private readonly List<HttpMessageInvoker> _clients = [];
        public HttpMessageInvoker CreateClient(ForwarderHttpClientContext context)
        {
            Calls++;
            if (Failure != null) throw Failure;
            if (context.OldClient != null && context.OldConfig == context.NewConfig) return context.OldClient;
            var client = new HttpMessageInvoker(new ControlledHttpHandler((_, _) => throw new InvalidOperationException("Configuration tests must never send HTTP requests")));
            _clients.Add(client);
            return client;
        }
        public void Dispose()
        {
            foreach (var client in _clients) client.Dispose();
            _clients.Clear();
        }
    }
}
