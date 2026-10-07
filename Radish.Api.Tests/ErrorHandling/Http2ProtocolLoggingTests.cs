using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class Http2ProtocolLoggingTests
{
    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task StreamRejection_ShouldResetOnlyStreamAndAllowNextRequest(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        var calls = 0;
        await using var pipeline = new OuterHttpTestPipeline(capture, context =>
        {
            calls++;
            context.Response.ContentLength = 0;
            return Task.CompletedTask;
        });
        await using var server = new MemoryKestrelFixture(pipeline, capture, endpoint: listen => listen.Protocols = HttpProtocols.Http2);
        await server.Start();
        await server.Exchange(async stream =>
        {
            using var wire = new Http2Wire(stream);
            await wire.Initialize();
            await wire.Send(1, 5, 1, new byte[] { 0x82, 0x86 }); // 缺少 :path，流级协议拒绝。
            var reset = await wire.Receive(3, 1);
            Assert.Equal(1u, BinaryPrimitives.ReadUInt32BigEndian(reset));
            Assert.Equal(0, calls);
            await wire.Send(1, 5, 3, Http2Wire.RequestHeaders());
            var headers = await wire.Receive(1, 3);
            Assert.Equal(0x88, headers[0]); // HPACK 静态表 :status 200。
            Assert.Equal(1, calls);
            await wire.Send(7, 0, 0, new byte[8]);
        });
        Assert.Equal(1, pipeline.Application.DisposeCount);
        Assert.DoesNotContain(capture.Events, e => !e.Diagnostic);
        if (!candidate || environment == "Development")
        {
            Assert.Contains("http.http2_stream_error", capture.Codes);
            Assert.Contains("http.http2_connection_closed", capture.Codes);
        }
        KestrelHttpLoggingTests.AssertSafeTargetEvents(capture);
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task ConnectionAndCompressionErrors_ShouldKeepGoAwayCodesAndSkipApplication(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        foreach (var scenario in new[] { "ping", "hpack", "zero-index" })
        {
            var calls = 0;
            await using var pipeline = new OuterHttpTestPipeline(capture, _ => { calls++; return Task.CompletedTask; });
            await using var server = new MemoryKestrelFixture(pipeline, capture, endpoint: listen => listen.Protocols = HttpProtocols.Http2);
            await server.Start();
            await server.Exchange(async stream =>
            {
                using var wire = new Http2Wire(stream);
                await wire.Initialize();
                if (scenario != "ping") await wire.Send(1, 5, 1, new byte[] { scenario == "hpack" ? (byte)0xff : (byte)0x80 }); // 截断索引与索引 0 的真实异常边界不同。
                else await wire.Send(6, 0, 1, new byte[8]); // PING 不能指定请求流。
                var goaway = await wire.Receive(7, 0);
                Assert.Equal(scenario == "hpack" ? 9u : scenario == "zero-index" ? 2u : 1u, BinaryPrimitives.ReadUInt32BigEndian(goaway.AsSpan(4)));
            });
            Assert.Equal(0, calls);
            Assert.Equal(0, pipeline.Application.DisposeCount);
            if (scenario == "zero-index")
            {
                var warning = Assert.Single(capture.Events, e => !e.Diagnostic);
                Assert.Equal("http.server_unclassified", warning.Code);
                Assert.Equal("Warning", warning.Level);
            }
            else
            {
                Assert.DoesNotContain(capture.Events, e => !e.Diagnostic);
                if (!candidate || environment == "Development")
                    Assert.Contains(scenario == "hpack" ? "http.http2_headers_decode_failed" : "http.http2_connection_error", capture.Codes);
            }
            KestrelHttpLoggingTests.AssertSafeTargetEvents(capture);
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task ApplicationFailure_ShouldKeep500AndExistingHttpOwner(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        var original = new IOException(HttpBoundaryCapture.Secret);
        await using var pipeline = new OuterHttpTestPipeline(capture, _ => throw original);
        await using var server = new MemoryKestrelFixture(pipeline, capture, endpoint: listen => listen.Protocols = HttpProtocols.Http2);
        await server.Start();
        await server.Exchange(async stream =>
        {
            using var wire = new Http2Wire(stream);
            await wire.Initialize();
            await wire.Send(1, 5, 1, Http2Wire.RequestHeaders());
            var headers = await wire.Receive(1, 1);
            Assert.Equal(0x8e, headers[0]); // HPACK 静态表 :status 500。
            await wire.Send(7, 0, 0, new byte[8]);
        });
        Assert.Equal(1, pipeline.Application.DisposeCount);
        Assert.Same(environment == "Development" ? null : original, pipeline.Application.DisposedException);
        Assert.Equal(new[] { environment == "Development" ? "http.developer_exception" : "http.server_application_failed" },
            capture.Events.Where(e => !e.Diagnostic).Select(e => e.Code));
        KestrelHttpLoggingTests.AssertSafeTargetEvents(capture);
    }
}

internal sealed class Http2Wire(Stream stream) : IDisposable
{
    private readonly CancellationTokenSource _timeout = new(TimeSpan.FromSeconds(10));
    public async Task Initialize()
    {
        await stream.WriteAsync(Encoding.ASCII.GetBytes("PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n"), _timeout.Token);
        await Send(4, 0, 0, Array.Empty<byte>());
        await Receive(4, 0);
        await Send(4, 1, 0, Array.Empty<byte>());
    }
    public async Task Send(byte type, byte flags, int id, byte[] payload)
    {
        var frame = new byte[9 + payload.Length];
        frame[0] = (byte)(payload.Length >> 16);
        frame[1] = (byte)(payload.Length >> 8);
        frame[2] = (byte)payload.Length;
        frame[3] = type;
        frame[4] = flags;
        BinaryPrimitives.WriteInt32BigEndian(frame.AsSpan(5), id);
        payload.CopyTo(frame, 9);
        await stream.WriteAsync(frame, _timeout.Token);
    }
    public async Task<byte[]> Receive(byte type, int id)
    {
        for (var count = 0; count < 100; count++)
        {
            var header = new byte[9];
            await stream.ReadExactlyAsync(header, _timeout.Token);
            var payload = new byte[(header[0] << 16) | (header[1] << 8) | header[2]];
            await stream.ReadExactlyAsync(payload, _timeout.Token);
            if (header[3] == type && BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(5)) == id) return payload;
            Assert.NotEqual(7, header[3]); // 未预期的 GOAWAY 必须失败，不能跳过真实协议故障。
        }
        throw new InvalidOperationException("Expected HTTP/2 frame was not received");
    }
    public static byte[] RequestHeaders()
    {
        var path = Encoding.ASCII.GetBytes("/" + HttpBoundaryCapture.Secret);
        return new byte[] { 0x82, 0x86, 0x01, 0x0c }.Concat(Encoding.ASCII.GetBytes("example.test"))
            .Concat(new byte[] { 0x04, (byte)path.Length }).Concat(path).ToArray();
    }
    public void Dispose() => _timeout.Dispose();
}
