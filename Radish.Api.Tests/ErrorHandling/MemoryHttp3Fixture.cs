using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

/// <summary>真实 Kestrel HTTP/3，仅注册内存 multiplexed transport；没有 Socket、QUIC 或 UDP。</summary>
internal sealed class MemoryHttp3Fixture : IAsyncDisposable
{
    private readonly Listener _listener = new();
    private readonly X509Certificate2 _certificate = HttpsProtocolLoggingTests.CreateCertificate();
    public readonly OuterHttpTestPipeline Pipeline;
    public readonly MemoryHttp3Connection Connection = new();
    private readonly IServer _server;
    public MemoryHttp3Fixture(HttpBoundaryCapture capture, RequestDelegate action)
    {
        Pipeline = new OuterHttpTestPipeline(capture, action, configure: builder =>
        {
            builder.Services.RemoveAll<IConnectionListenerFactory>();
            builder.Services.RemoveAll<IMultiplexedConnectionListenerFactory>();
            builder.Services.AddSingleton<IMultiplexedConnectionListenerFactory>(_listener);
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.Limits.MinRequestBodyDataRate = null;
                options.Limits.MinResponseDataRate = null;
                options.Listen(IPAddress.Loopback, 0, listen =>
                {
                    listen.Protocols = HttpProtocols.Http3;
                    // Kestrel 要求 HTTP/3 endpoint 具有 TLS options；内存 listener 不执行握手。
                    listen.UseHttps(_certificate);
                });
            });
        });
        Assert.Empty(Pipeline.Services.GetServices<IConnectionListenerFactory>());
        Assert.Same(_listener, Assert.Single(Pipeline.Services.GetServices<IMultiplexedConnectionListenerFactory>()));
        _server = Pipeline.Services.GetRequiredService<IServer>();
    }
    public async Task Start()
    {
        await _server.StartAsync(Pipeline.Application, TestContext.Current.CancellationToken);
        Assert.Equal(1, _listener.BindCount);
        await _listener.Connections.Writer.WriteAsync(Connection, TestContext.Current.CancellationToken);
        await Connection.OutboundCreated.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }
    public async ValueTask DisposeAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await _server.StopAsync(timeout.Token);
        await Connection.Disposed.Task.WaitAsync(timeout.Token);
        await Connection.CleanupClientPipes();
        await Pipeline.DisposeAsync();
        _certificate.Dispose();
    }
    private sealed class Listener : IMultiplexedConnectionListenerFactory, IMultiplexedConnectionListener
    {
        public readonly Channel<MultiplexedConnectionContext> Connections = Channel.CreateUnbounded<MultiplexedConnectionContext>();
        public EndPoint EndPoint { get; private set; } = new IPEndPoint(IPAddress.Loopback, 0);
        public int BindCount;
        public ValueTask<IMultiplexedConnectionListener> BindAsync(EndPoint endpoint, IFeatureCollection? features = null, CancellationToken cancellationToken = default)
        {
            BindCount++;
            EndPoint = endpoint;
            return ValueTask.FromResult<IMultiplexedConnectionListener>(this);
        }
        public async ValueTask<MultiplexedConnectionContext?> AcceptAsync(IFeatureCollection? features = null, CancellationToken cancellationToken = default) =>
            await Connections.Reader.WaitToReadAsync(cancellationToken) && Connections.Reader.TryRead(out var connection) ? connection : null;
        public ValueTask UnbindAsync(CancellationToken cancellationToken = default) { Connections.Writer.TryComplete(); return ValueTask.CompletedTask; }
        public ValueTask DisposeAsync() => UnbindAsync();
    }
}

internal sealed class MemoryHttp3Connection : MultiplexedConnectionContext, IProtocolErrorCodeFeature
{
    private readonly Channel<ConnectionContext> _incoming = Channel.CreateUnbounded<ConnectionContext>();
    private readonly ConcurrentBag<MemoryHttp3Stream> _streams = new();
    private readonly CancellationTokenSource _closed = new();
    private readonly Lock _lifetime = new();
    private Task? _abort;
    private Task? _dispose;
    private bool _disposing;
    public readonly TaskCompletionSource OutboundCreated = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public readonly TaskCompletionSource Disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public override string ConnectionId { get; set; } = HttpBoundaryCapture.Secret;
    public override IFeatureCollection Features { get; } = new FeatureCollection();
    public override IDictionary<object, object?> Items { get; set; } = new Dictionary<object, object?>();
    public override CancellationToken ConnectionClosed { get; set; }
    public long Error { get; set; } = -1;
    public MemoryHttp3Stream? Outbound { get; private set; }
    public MemoryHttp3Connection()
    {
        ConnectionClosed = _closed.Token;
        Features.Set<IProtocolErrorCodeFeature>(this);
        LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 0);
        RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, 0);
    }
    public async Task<MemoryHttp3Stream> Send(long id, bool bidirectional, byte[] bytes, bool finish = true)
    {
        var stream = new MemoryHttp3Stream(id, canRead: true, canWrite: bidirectional);
        _streams.Add(stream);
        await stream.Input.Writer.WriteAsync(bytes, TestContext.Current.CancellationToken);
        if (finish) await stream.Input.Writer.CompleteAsync();
        await _incoming.Writer.WriteAsync(stream, TestContext.Current.CancellationToken);
        return stream;
    }
    public override async ValueTask<ConnectionContext?> AcceptAsync(CancellationToken cancellationToken = default)
    {
        try { return await _incoming.Reader.ReadAsync(cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return null; }
        catch (ChannelClosedException) { return null; }
    }
    public override ValueTask<ConnectionContext> ConnectAsync(IFeatureCollection? features = null, CancellationToken cancellationToken = default)
    {
        Outbound = new MemoryHttp3Stream(3, canRead: false, canWrite: true);
        _streams.Add(Outbound);
        OutboundCreated.TrySetResult();
        return ValueTask.FromResult<ConnectionContext>(Outbound);
    }
    public override void Abort(ConnectionAbortedException abortReason)
    {
        lock (_lifetime)
        {
            if (_disposing || _abort != null) return;
            _incoming.Writer.TryComplete();
            foreach (var stream in _streams) stream.Abort(abortReason);
            _abort = _closed.CancelAsync();
        }
    }
    public override void Abort() => Abort(new ConnectionAbortedException("Memory connection closed"));
    public override ValueTask DisposeAsync()
    {
        TaskCompletionSource completion;
        lock (_lifetime)
        {
            if (_dispose != null) return new ValueTask(_dispose);
            _disposing = true;
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _dispose = completion.Task;
        }
        _ = CompleteDispose(completion);
        return new ValueTask(completion.Task);
    }
    private async Task CompleteDispose(TaskCompletionSource completion)
    {
        try
        {
            if (_abort != null) await _abort;
            foreach (var stream in _streams) await stream.DisposeAsync();
            _closed.Dispose();
            Disposed.TrySetResult();
            completion.TrySetResult();
        }
        catch (Exception exception)
        {
            Disposed.TrySetException(exception);
            completion.TrySetException(exception);
        }
    }
    public async Task CleanupClientPipes()
    {
        foreach (var stream in _streams)
        {
            await stream.Input.Writer.CompleteAsync();
            await stream.Output.Reader.CompleteAsync();
        }
    }
}

internal sealed class MemoryHttp3Stream : DefaultConnectionContext, IStreamIdFeature, IStreamDirectionFeature,
    IProtocolErrorCodeFeature, IStreamAbortFeature, IStreamClosedFeature, IPersistentStateFeature
{
    public readonly Pipe Input = new();
    public readonly Pipe Output = new();
    public readonly TaskCompletionSource Disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Lock _lifetime = new();
    private readonly List<(Action<object?> Callback, object? State)> _onClosed = new();
    private Task? _dispose;
    private bool _aborted;
    private bool _callbacksClosed;
    public long StreamId { get; }
    public bool CanRead { get; }
    public bool CanWrite { get; }
    public long Error { get; set; } = -1;
    public IDictionary<object, object?> State { get; } = new Dictionary<object, object?>();
    public MemoryHttp3Stream(long id, bool canRead, bool canWrite) : base(HttpBoundaryCapture.Secret)
    {
        StreamId = id;
        CanRead = canRead;
        CanWrite = canWrite;
        Transport = new Duplex(Input.Reader, Output.Writer);
        Features.Set<IStreamIdFeature>(this);
        Features.Set<IStreamDirectionFeature>(this);
        Features.Set<IProtocolErrorCodeFeature>(this);
        Features.Set<IStreamAbortFeature>(this);
        Features.Set<IStreamClosedFeature>(this);
        Features.Set<ITlsConnectionFeature>(new MemoryTlsFeature());
        Features.Set<IPersistentStateFeature>(this);
    }
    public void OnClosed(Action<object?> callback, object? state)
    {
        lock (_lifetime)
        {
            if (!_callbacksClosed)
            {
                _onClosed.Add((callback, state));
                return;
            }
        }
        callback(state);
    }
    public override void Abort(ConnectionAbortedException abortReason)
    {
        lock (_lifetime)
        {
            if (_aborted || _dispose != null) return;
            _aborted = true;
            // 让真实处理器结束读循环；协议错误码由真实 stream / connection 设置。
            Input.Writer.Complete();
            Output.Writer.CancelPendingFlush();
        }
    }
    public void AbortRead(long errorCode, ConnectionAbortedException abortReason) => Input.Writer.Complete();
    public void AbortWrite(long errorCode, ConnectionAbortedException abortReason) => Output.Writer.CancelPendingFlush();
    public override ValueTask DisposeAsync()
    {
        TaskCompletionSource completion;
        lock (_lifetime)
        {
            if (_dispose != null) return new ValueTask(_dispose);
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _dispose = completion.Task;
        }
        // DisposeCore 可能同步执行到 Kestrel 回调；不能持有 transport 锁调用它。
        _ = CompleteDispose(completion);
        return new ValueTask(completion.Task);
    }
    private async Task CompleteDispose(TaskCompletionSource completion)
    {
        try
        {
            await DisposeCore();
            completion.TrySetResult();
        }
        catch (Exception exception)
        {
            Disposed.TrySetException(exception);
            completion.TrySetException(exception);
        }
    }
    private async Task DisposeCore()
    {
        await Input.Reader.CompleteAsync();
        await Output.Writer.CompleteAsync();
        (Action<object?> Callback, object? State)[] callbacks;
        lock (_lifetime)
        {
            _callbacksClosed = true;
            callbacks = _onClosed.ToArray();
            _onClosed.Clear();
        }
        foreach (var (callback, state) in callbacks) callback(state);
        await base.DisposeAsync();
        Disposed.TrySetResult();
    }
    public async Task<byte[]> Response()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var bytes = new MemoryStream();
        await Output.Reader.AsStream(leaveOpen: true).CopyToAsync(bytes, timeout.Token);
        await Disposed.Task.WaitAsync(timeout.Token);
        return bytes.ToArray();
    }
    private sealed record Duplex(PipeReader Input, PipeWriter Output) : IDuplexPipe;
    // QUIC 已建立连接的 transport feature；此夹具不声称执行 QUIC TLS 握手。
    private sealed class MemoryTlsFeature : ITlsConnectionFeature
    {
        public X509Certificate2? ClientCertificate { get; set; }
        public Task<X509Certificate2?> GetClientCertificateAsync(CancellationToken cancellationToken) => Task.FromResult(ClientCertificate);
    }
}
