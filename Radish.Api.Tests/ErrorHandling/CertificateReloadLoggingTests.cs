using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class CertificateReloadLoggingTests
{
    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task RealRebind_ShouldRetainEndpointOnCertificateFailureAndRecoverWithSafePhaseLogs(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var scope = capture.SensitiveScope();
        using var files = new CertificateTestFiles();
        await using var fixture = new MemoryCertificateReloadFixture(capture, files);
        await fixture.Start();
        var first = Assert.Single(fixture.Listeners);
        Assert.False(first.Disposed);
        Assert.Equal(1, fixture.Provider.ActiveRegistrations);
        capture.Clear();

        fixture.Configuration["Certificates:Default:Path"] = files.CertificatePath + ".missing";
        await fixture.Rebind();
        Assert.Same(first, Assert.Single(fixture.Listeners));
        Assert.False(first.Disposed);
        var failure = Assert.Single(capture.Events, e => !e.Diagnostic);
        Assert.Equal("http.server_unclassified", failure.Code);
        Assert.Equal(candidate ? "Error" : "Fatal", failure.Level);
        Assert.Equal("io", failure.FailureKind);
        CertificateLoggingFramework.AssertSafe(capture);
        capture.Clear();

        fixture.Configuration["Certificates:Default:Path"] = files.CertificatePath;
        fixture.Configuration["Endpoints:Main:Url"] = "http://127.0.0.1:41002";
        await fixture.Rebind();
        Assert.Equal(2, fixture.Listeners.Count);
        Assert.True(first.Disposed);
        Assert.False(fixture.Listeners[1].Disposed);
        Assert.Equal(new IPEndPoint(IPAddress.Loopback, 41002), fixture.Listeners[1].EndPoint);
        Assert.DoesNotContain(capture.Events, e => !e.Diagnostic);
        CertificateLoggingFramework.AssertSafe(capture);
        capture.Clear();

        fixture.Configuration["Endpoints:Main:Url"] = "http://127.0.0.1:41003";
        fixture.BindFailure = new IOException(HttpBoundaryCapture.Secret);
        await fixture.Rebind();
        Assert.True(fixture.Listeners[1].Disposed);
        Assert.Equal(2, fixture.Listeners.Count);
        failure = Assert.Single(capture.Events, e => !e.Diagnostic);
        Assert.Equal("http.server_unclassified", failure.Code);
        Assert.Equal(candidate ? "Error" : "Fatal", failure.Level);
        Assert.Equal("io", failure.FailureKind);
        CertificateLoggingFramework.AssertSafe(capture);
        await fixture.Stop();
        Assert.All(fixture.Listeners, listener => Assert.True(listener.Disposed));
        Assert.True(fixture.Provider.Disposed);
        Assert.Equal(0, fixture.Provider.ActiveRegistrations);
    }
}

/// <summary>真实配置加载 / RebindAsync，唯一 transport 是内存工厂；HTTP endpoint 不执行 TLS 或系统 bind。</summary>
internal sealed class MemoryCertificateReloadFixture : IConnectionListenerFactory, IAsyncDisposable
{
    public readonly IConfigurationRoot Configuration;
    public readonly ControlledCertificateFileProvider Provider = new();
    public readonly List<Listener> Listeners = new();
    public Exception? BindFailure;
    private readonly ServiceProvider _services;
    private readonly IDisposable _watcher;
    private readonly KestrelServer _server;
    private readonly KestrelConfigurationLoader _loader;
    private readonly HashSet<X509Certificate2> _loadedCertificates = new(ReferenceEqualityComparer.Instance);
    private bool _stopped;

    public MemoryCertificateReloadFixture(HttpBoundaryCapture capture, CertificateTestFiles files)
    {
        var environment = new CertificateTestEnvironment(files.Root);
        var https = Activator.CreateInstance(CertificateLoggingFramework.Type("Microsoft.AspNetCore.Server.Kestrel.Core.HttpsConfigurationService"))!;
        CertificateLoggingFramework.Call(https, "Initialize", environment,
            CertificateLoggingFramework.Logger(CertificateLoggingFramework.Config, capture.Factory),
            CertificateLoggingFramework.Logger("Microsoft.AspNetCore.Server.Kestrel.Https.Internal.HttpsConnectionMiddleware", capture.Factory));
        _services = new ServiceCollection()
            .AddSingleton<IHostEnvironment>(environment)
            .AddSingleton(capture.Factory)
            .AddSingleton(typeof(ILogger<>), typeof(Logger<>))
            .AddSingleton(CertificateLoggingFramework.Type("Microsoft.AspNetCore.Server.Kestrel.Core.IHttpsConfigurationService"), https)
            .BuildServiceProvider();
        Configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Certificates:Default:Path"] = files.CertificatePath,
            ["Endpoints:Main:Url"] = "http://127.0.0.1:41001",
            ["Endpoints:Main:Protocols"] = "Http1"
        }).Build();
        _watcher = CertificateWatcherLoggingTests.CreateWatcher(capture, _ => Provider);
        var options = new KestrelServerOptions { ApplicationServices = _services };
        // 显式提供 Default 公钥证书和初始化后的配置服务，绝不进入开发证书 / 系统证书库回退。
        _loader = (KestrelConfigurationLoader)Activator.CreateInstance(typeof(KestrelConfigurationLoader), CertificateLoggingFramework.Members,
            null, new object[] { options, Configuration, https, _watcher, true }, null)!;
        options.ConfigurationLoader = _loader;
        _server = new KestrelServer(Options.Create(options), this, capture.Factory);
    }

    public async Task Start()
    {
        try { await _server.StartAsync(new NoRequests(), TestContext.Current.CancellationToken); }
        finally { RememberCertificates(); }
    }
    public async Task Rebind()
    {
        var inner = typeof(KestrelServer).GetFields(CertificateLoggingFramework.Members)
            .Single(f => f.FieldType.Name == "KestrelServerImpl").GetValue(_server)!;
        // 等待真实重载方法完成，不靠异步 change callback 的调度时间猜测结果。
        try { await ((Task)CertificateLoggingFramework.Call(inner, "RebindAsync")!).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken); }
        finally { RememberCertificates(); }
    }
    private void RememberCertificates()
    {
        if (CertificateLoggingFramework.Get<X509Certificate2?>(_loader, "DefaultCertificate") is { } certificate)
            _loadedCertificates.Add(certificate);
        if (CertificateLoggingFramework.Get<X509Certificate2Collection?>(_loader, "DefaultCertificateChain") is { } chain)
            foreach (var item in chain) _loadedCertificates.Add(item);
    }
    public ValueTask<IConnectionListener> BindAsync(EndPoint endpoint, CancellationToken cancellationToken = default)
    {
        if (BindFailure != null) throw BindFailure;
        var listener = new Listener(endpoint);
        Listeners.Add(listener);
        return ValueTask.FromResult<IConnectionListener>(listener);
    }
    public async Task Stop()
    {
        if (_stopped) return;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await _server.StopAsync(timeout.Token);
        _server.Dispose();
        _watcher.Dispose();
        _stopped = true;
    }
    public async ValueTask DisposeAsync()
    {
        try { await Stop(); }
        finally
        {
            _watcher.Dispose();
            foreach (var certificate in _loadedCertificates) certificate.Dispose();
            (Configuration as IDisposable)?.Dispose();
            await _services.DisposeAsync();
        }
    }
    internal sealed class Listener(EndPoint endpoint) : IConnectionListener
    {
        private readonly Channel<ConnectionContext> _connections = Channel.CreateUnbounded<ConnectionContext>();
        public bool Disposed;
        public EndPoint EndPoint => endpoint;
        public async ValueTask<ConnectionContext?> AcceptAsync(CancellationToken cancellationToken = default) =>
            await _connections.Reader.WaitToReadAsync(cancellationToken) ? throw new InvalidOperationException("No test connection expected") : null;
        public ValueTask UnbindAsync(CancellationToken cancellationToken = default) { _connections.Writer.TryComplete(); return ValueTask.CompletedTask; }
        public ValueTask DisposeAsync() { Disposed = true; return UnbindAsync(); }
    }
    private sealed class NoRequests : IHttpApplication<object>
    {
        public object CreateContext(IFeatureCollection features) => throw new InvalidOperationException("No test request expected");
        public Task ProcessRequestAsync(object context) => throw new InvalidOperationException("No test request expected");
        public void DisposeContext(object context, Exception? exception) => throw new InvalidOperationException("No test request expected");
    }
}
