using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class Http3ProtocolLoggingTests
{
    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task RealRequestStreams_ShouldKeepSuccessAndApplicationFailureOwnership(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        foreach (var failure in new[] { false, true })
        {
            var calls = 0;
            var original = new IOException(HttpBoundaryCapture.Secret);
            await using (var server = new MemoryHttp3Fixture(capture, context =>
            {
                calls++;
                if (failure) throw original;
                context.Response.ContentLength = 0;
                return Task.CompletedTask;
            }))
            {
                await server.Start();
                var request = await server.Connection.Send(0, true, Headers());
                var response = await request.Response();
                Assert.True(calls == 1, $"Application calls: {calls}; stream error: {request.Error}; connection error: {server.Connection.Error}; events: {string.Join(",", capture.Codes)}");
                Assert.Equal(1, response[0]); // HEADERS 帧。
                var offset = 1;
                var length = ReadInteger(response, ref offset);
                Assert.True(length >= 3);
                Assert.Equal(0, response[offset++]); // QPACK required insert count。
                Assert.Equal(0, response[offset++]); // Delta base。
                if (failure)
                {
                    Assert.Equal(0xff, response[offset++]);
                    Assert.Equal(8, response[offset]); // 静态表 :status 500 (71)。
                }
                else Assert.Equal(0xd9, response[offset]); // 静态表 :status 200 (25)。
                Assert.Equal(1, server.Pipeline.Application.DisposeCount);
                Assert.Same(failure && environment == "Production" ? original : null, server.Pipeline.Application.DisposedException);
                Assert.Equal(-1, request.Error);
            }
            Assert.Equal(failure ? new[] { environment == "Development" ? "http.developer_exception" : "http.server_application_failed" } : Array.Empty<string>(),
                capture.Events.Where(e => !e.Diagnostic).Select(e => e.Code));
            if (!candidate || environment == "Development") Assert.Contains("http.http3_connection_closed", capture.Codes);
            KestrelHttpLoggingTests.AssertSafeTargetEvents(capture);
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task InvalidControlStream_ShouldCloseConnectionWithProtocolCode(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        var calls = 0;
        await using (var server = new MemoryHttp3Fixture(capture, _ => { calls++; return Task.CompletedTask; }))
        {
            await server.Start();
            // 单向控制流类型 0；控制流禁止 DATA，H3_FRAME_UNEXPECTED。
            await server.Connection.Send(2, false, new byte[] { 0, 0, 0 });
            await server.Connection.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Assert.Equal(0x105, server.Connection.Error);
            Assert.Equal(0, calls);
        }
        Assert.DoesNotContain(capture.Events, e => !e.Diagnostic);
        if (!candidate || environment == "Development")
        {
            Assert.Contains("http.http3_connection_error", capture.Codes);
            Assert.Contains("http.http3_connection_closed", capture.Codes);
        }
        KestrelHttpLoggingTests.AssertSafeTargetEvents(capture);
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task ApplicationAbort_ShouldResetStreamAndAllowNextRequest(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        var calls = 0;
        await using (var server = new MemoryHttp3Fixture(capture, context =>
        {
            calls++;
            if (calls == 1) context.Abort();
            else context.Response.ContentLength = 0;
            return Task.CompletedTask;
        }))
        {
            await server.Start();
            var aborted = await server.Connection.Send(0, true, Headers());
            await aborted.Response();
            Assert.Equal(0x102, aborted.Error);
            var next = await server.Connection.Send(4, true, Headers());
            Assert.NotEmpty(await next.Response());
            Assert.Equal(2, calls);
            Assert.Equal(2, server.Pipeline.Application.DisposeCount);
        }
        Assert.DoesNotContain(capture.Events, e => !e.Diagnostic);
        if (!candidate || environment == "Development") Assert.Contains("http.http3_stream_aborted", capture.Codes);
        KestrelHttpLoggingTests.AssertSafeTargetEvents(capture);
    }

    private static byte[] Headers()
    {
        var path = Encoding.ASCII.GetBytes("/" + HttpBoundaryCapture.Secret);
        var block = new byte[] { 0, 0, 0xd1, 0xd7, 0x50, 12 }.Concat(Encoding.ASCII.GetBytes("example.test"))
            .Concat(new byte[] { 0x51, (byte)path.Length }).Concat(path).ToArray();
        Assert.True(block.Length < 64);
        return new byte[] { 1, (byte)block.Length }.Concat(block).ToArray();
    }
    private static long ReadInteger(byte[] bytes, ref int offset)
    {
        var count = 1 << (bytes[offset] >> 6);
        long value = bytes[offset++] & 0x3f;
        for (var i = 1; i < count; i++) value = (value << 8) | bytes[offset++];
        return value;
    }
}
