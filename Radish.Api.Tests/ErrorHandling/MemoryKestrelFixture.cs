using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

/// <summary>只向 Kestrel 提供内存连接工厂；不注册 socket transport，不启动项目宿主或监听端口。</summary>
internal sealed class MemoryKestrelFixture : IAsyncDisposable
{
    private readonly MemoryListener _listener = new();
    private readonly KestrelServer _server;
    private readonly IHttpApplication<object> _application;
    public MemoryKestrelFixture(OuterHttpTestPipeline pipeline, HttpBoundaryCapture capture)
    {
        var options = new KestrelServerOptions { ApplicationServices = pipeline.Services, AddServerHeader = false };
        options.Limits.MinRequestBodyDataRate = null;
        options.Limits.MinResponseDataRate = null;
        // 此 endpoint 仅交给下方 MemoryListener 保存，永远不执行系统 bind。
        options.Listen(IPAddress.Loopback, 0, listen => listen.Protocols = HttpProtocols.Http1);
        _server = new KestrelServer(Options.Create(options), _listener, capture.Factory);
        _application = pipeline.Application;
    }

    public async Task Start()
    {
        await _server.StartAsync(_application, TestContext.Current.CancellationToken);
        Assert.Equal(1, _listener.BindCount);
    }

    public async Task<string> Request(string? path = null)
    {
        await using var connection = new MemoryConnection();
        await _listener.Connections.Writer.WriteAsync(connection, TestContext.Current.CancellationToken);
        var request = "GET " + (path ?? "/api/" + HttpBoundaryCapture.Secret) + "?token=" + HttpBoundaryCapture.Secret +
            " HTTP/1.1\r\nHost: example.test\r\nAuthorization: Bearer " + HttpBoundaryCapture.Secret + "\r\nConnection: close\r\n\r\n";
        await connection.Input.Writer.WriteAsync(Encoding.UTF8.GetBytes(request), TestContext.Current.CancellationToken);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var bytes = new MemoryStream();
        while (true)
        {
            var read = await connection.Output.Reader.ReadAsync(timeout.Token);
            foreach (var segment in read.Buffer) await bytes.WriteAsync(segment, timeout.Token);
            connection.Output.Reader.AdvanceTo(read.Buffer.End);
            if (read.IsCompleted) break;
        }
        await connection.Disposed.Task.WaitAsync(timeout.Token);
        await connection.Input.Writer.CompleteAsync();
        await connection.Output.Reader.CompleteAsync();
        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    public async ValueTask DisposeAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await _server.StopAsync(timeout.Token);
        _server.Dispose();
    }

    private sealed class MemoryListener : IConnectionListenerFactory, IConnectionListener
    {
        public readonly Channel<ConnectionContext> Connections = Channel.CreateUnbounded<ConnectionContext>();
        public int BindCount;
        public EndPoint EndPoint { get; private set; } = new IPEndPoint(IPAddress.Loopback, 0);
        public ValueTask<IConnectionListener> BindAsync(EndPoint endpoint, CancellationToken cancellationToken = default)
        {
            BindCount++;
            EndPoint = endpoint;
            return ValueTask.FromResult<IConnectionListener>(this);
        }
        public async ValueTask<ConnectionContext?> AcceptAsync(CancellationToken cancellationToken = default)
        {
            if (await Connections.Reader.WaitToReadAsync(cancellationToken) && Connections.Reader.TryRead(out var connection))
                return connection;
            return null;
        }
        public ValueTask UnbindAsync(CancellationToken cancellationToken = default)
        {
            Connections.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }
        public ValueTask DisposeAsync() => UnbindAsync();
    }

    private sealed class MemoryConnection : DefaultConnectionContext
    {
        public readonly Pipe Input = new();
        public readonly Pipe Output = new();
        public readonly TaskCompletionSource Disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public MemoryConnection() : base(HttpBoundaryCapture.Secret)
        {
            Transport = new Duplex(Input.Reader, Output.Writer);
            LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 0);
            RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, 0);
        }
        public override async ValueTask DisposeAsync()
        {
            if (Disposed.Task.IsCompleted) return;
            await Input.Reader.CompleteAsync();
            await Output.Writer.CompleteAsync();
            await base.DisposeAsync();
            Disposed.TrySetResult();
        }
    }
    private sealed record Duplex(PipeReader Input, PipeWriter Output) : IDuplexPipe;
}
