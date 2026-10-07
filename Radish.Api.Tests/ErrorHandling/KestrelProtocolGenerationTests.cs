using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class KestrelProtocolGenerationTests
{
    public static IEnumerable<object[]> Events()
    {
        yield return new object[] { "Http2", "Http2ConnectionError", 29, "Http2ConnectionError", "http.http2_connection_error", LogLevel.Debug };
        yield return new object[] { "Http2", "Http2StreamError", 30, "Http2StreamError", "http.http2_stream_error", LogLevel.Debug };
        yield return new object[] { "Http2", "HPackDecodingError", 31, "HPackDecodingError", "http.http2_headers_decode_failed", LogLevel.Debug };
        yield return new object[] { "Http2", "Http2StreamResetAbort", 35, "Http2StreamResetAbort", "http.http2_stream_reset", LogLevel.Debug };
        yield return new object[] { "Http2", "Http2ConnectionClosing", 36, "Http2ConnectionClosing", "http.http2_connection_closing", LogLevel.Debug };
        yield return new object[] { "Http2", "Http2FrameReceived", 37, "Http2FrameReceived", "http.http2_frame_received", LogLevel.Trace };
        yield return new object[] { "Http2", "HPackEncodingError", 38, "HPackEncodingError", "http.http2_headers_encode_failed", LogLevel.Information };
        yield return new object[] { "Http2", "Http2MaxConcurrentStreamsReached", 40, "Http2MaxConcurrentStreamsReached", "http.http2_stream_limit_reached", LogLevel.Debug };
        yield return new object[] { "Http2", "Http2ConnectionClosed", 48, "Http2ConnectionClosed", "http.http2_connection_closed", LogLevel.Debug };
        yield return new object[] { "Http2", "Http2FrameSending", 49, "Http2FrameSending", "http.http2_frame_sending", LogLevel.Trace };
        yield return new object[] { "Http2", "Http2QueueOperationsExceeded", 60, "Http2QueueOperationsExceeded", "http.http2_output_queue_exceeded", LogLevel.Critical };
        yield return new object[] { "Http2", "Http2UnexpectedDataRemaining", 61, "Http2UnexpectedDataRemaining", "http.http2_output_data_remaining", LogLevel.Critical };
        yield return new object[] { "Http2", "Http2ConnectionQueueProcessingCompleted", 62, "Http2ConnectionQueueProcessingCompleted", "http.http2_output_queue_completed", LogLevel.Debug };
        yield return new object[] { "Http2", "Http2UnexpectedConnectionQueueError", 63, "Http2UnexpectedConnectionQueueError", "http.http2_output_queue_failed", LogLevel.Critical };
        yield return new object[] { "Http2", "Http2TooManyEnhanceYourCalms", 64, "Http2TooManyEnhanceYourCalms", "http.http2_reset_limit_exceeded", LogLevel.Debug };
        yield return new object[] { "Http2", "Http2FlowControlQueueOperationsExceeded", 65, "Http2FlowControlQueueOperationsExceeded", "http.http2_flow_queue_exceeded", LogLevel.Debug };
        yield return new object[] { "Http2", "Http2FlowControlQueueMaximumTooLow", 66, "Http2FlowControlQueueMaximumTooLow", "http.http2_flow_queue_adjusted", LogLevel.Debug };
        yield return new object[] { "Http3", "Http3ConnectionError", 42, "Http3ConnectionError", "http.http3_connection_error", LogLevel.Debug };
        yield return new object[] { "Http3", "Http3ConnectionClosing", 43, "Http3ConnectionClosing", "http.http3_connection_closing", LogLevel.Debug };
        yield return new object[] { "Http3", "Http3ConnectionClosed", 44, "Http3ConnectionClosed", "http.http3_connection_closed", LogLevel.Debug };
        yield return new object[] { "Http3", "Http3StreamAbort", 45, "Http3StreamAbort", "http.http3_stream_aborted", LogLevel.Debug };
        yield return new object[] { "Http3", "Http3FrameReceived", 46, "Http3FrameReceived", "http.http3_frame_received", LogLevel.Trace };
        yield return new object[] { "Http3", "Http3FrameSending", 47, "Http3FrameSending", "http.http3_frame_sending", LogLevel.Trace };
        yield return new object[] { "Http3", "Http3OutboundControlStreamError", 50, "Http3OutboundControlStreamError", "http.http3_control_stream_failed", LogLevel.Debug };
        yield return new object[] { "Http3", "QPackDecodingError", 51, "QPackDecodingError", "http.http3_headers_decode_failed", LogLevel.Debug };
        yield return new object[] { "Http3", "QPackEncodingError", 52, "QPackEncodingError", "http.http3_headers_encode_failed", LogLevel.Information };
        yield return new object[] { "Http3", "Http3GoAwayStreamId", 53, "Http3GoAwayHighestOpenedStreamId", "http.http3_goaway", LogLevel.Debug };
        yield return new object[] { "Https", "AuthenticationFailed", 1, "AuthenticationFailed", "http.tls_handshake_failed", LogLevel.Debug };
        yield return new object[] { "Https", "AuthenticationTimedOut", 2, "AuthenticationTimedOut", "http.tls_handshake_timed_out", LogLevel.Debug };
        yield return new object[] { "Https", "HttpsConnectionEstablished", 3, "HttpsConnectionEstablished", "http.tls_established", LogLevel.Debug };
        yield return new object[] { "Https", "Http2DefaultCiphersInsufficient", 4, "Http2DefaultCiphersInsufficient", "http.tls_protocol_fallback", LogLevel.Information };
        yield return new object[] { "Https", "LocatingCertWithPrivateKey", 5, "LocateCertWithPrivateKey", "http.tls_certificate_search", LogLevel.Debug };
        yield return new object[] { "Https", "FoundCertWithPrivateKey", 6, "FoundCertWithPrivateKey", "http.tls_certificate_found", LogLevel.Debug };
        yield return new object[] { "Https", "FailedToFindCertificateInStore", 7, "FailToLocateCertificate", "http.tls_certificate_search_failed", LogLevel.Debug };
        yield return new object[] { "Https", "FailedToOpenStore", 8, "FailToOpenStore", "http.tls_certificate_store_failed", LogLevel.Debug };
        yield return new object[] { "Https", "NoSubjectAlternativeName", 9, "NoSubjectAlternativeName", "http.tls_certificate_missing_san", LogLevel.Information };
    }

    [Theory]
    [MemberData(nameof(Events))]
    public void Identity_ShouldMatchInstalledFramework(string source, string method, int id, string name, string code, LogLevel level)
    {
        var generator = Generator(source, method);
        var metadata = generator.GetCustomAttribute<LoggerMessageAttribute>()!;
        Assert.Equal(id, metadata.EventId);
        Assert.Equal(name, metadata.EventName);
        Assert.Equal(level, metadata.Level);
        Assert.StartsWith("http.", code);
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public void RealGenerators_ShouldUseCorrectCategoryAndKeepCriticalVisible(bool candidate, string environment)
    {
        foreach (var diagnostics in new[] { true, false })
        {
            using var capture = new HttpBoundaryCapture(candidate, environment, diagnostics: diagnostics);
            using var scope = capture.SensitiveScope();
            var traceType = Framework("Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure.KestrelTrace");
            var trace = Activator.CreateInstance(traceType, capture.Factory)!;
            foreach (var row in Events())
            {
                var source = (string)row[0];
                var method = (string)row[1];
                var level = (LogLevel)row[5];
                var target = source == "Https" ? Generator(source, method) : traceType.GetMethod(method)!;
                var args = target.GetParameters().Select(p => Argument(p.ParameterType, capture.Factory)).ToArray();
                target.Invoke(target.IsStatic ? null : trace, args);
                if (!candidate || level == LogLevel.Critical || (environment == "Development" && diagnostics))
                {
                    var logged = Assert.Single(capture.Events);
                    Assert.Equal(row[4], logged.Code);
                    Assert.Equal(level < LogLevel.Warning, logged.Diagnostic);
                    Assert.Equal(candidate ? (level == LogLevel.Critical ? "Error" : "Info") :
                        level == LogLevel.Critical ? "Fatal" : level == LogLevel.Trace ? "Verbose" : level.ToString(), logged.Level);
                }
                else Assert.Empty(capture.Events);
                capture.AssertSafe();
                capture.Clear();
            }
        }
    }

    private static Type Framework(string name) => Assembly.Load("Microsoft.AspNetCore.Server.Kestrel.Core").GetType(name, true)!;
    private static MethodInfo Generator(string source, string method) => Framework(source == "Https"
        ? "Microsoft.AspNetCore.Server.Kestrel.Https.Internal.HttpsConnectionMiddlewareLoggerExtensions"
        : "Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure.KestrelTrace+" + source + "Log")
        .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
        .Single(m => m.Name == method && m.GetCustomAttribute<LoggerMessageAttribute>() != null);

    private static object? Argument(Type type, ILoggerFactory factory)
    {
        if (type == typeof(string) || type == typeof(object)) return HttpBoundaryCapture.Secret;
        if (type == typeof(int)) return 1;
        if (type == typeof(long)) return 1L;
        if (type == typeof(Exception)) return new IOException(HttpBoundaryCapture.Secret);
        if (type == typeof(ConnectionAbortedException)) return new ConnectionAbortedException(HttpBoundaryCapture.Secret);
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ILogger<>))
            return Activator.CreateInstance(typeof(Logger<>).MakeGenericType(type.GetGenericArguments()), factory);
        if (Nullable.GetUnderlyingType(type) is Type inner) return Argument(inner, factory);
        if (type.IsEnum) return Enum.ToObject(type, 0);
        // 仅为本表中内部协议异常与空帧构造参数；签名漂移立即失败，不跳过事件。
        var constructor = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .OrderBy(c => c.GetParameters().Length).First();
        return constructor.Invoke(constructor.GetParameters().Select(p => Argument(p.ParameterType, factory)).ToArray());
    }
}
