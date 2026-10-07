using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Radish.Extension.Log;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class HttpExceptionFrameworkSinkTests
{
    [Theory]
    [InlineData(1, "UnhandledException", "http.framework_exception")]
    [InlineData(2, "ResponseStarted", "http.response_started")]
    [InlineData(3, "Exception", "http.exception_handler_failed")]
    [InlineData(4, "RequestAborted", "http.request_aborted")]
    [InlineData(1, "FutureEvent", "http.framework_unclassified")]
    [InlineData(999, "UnhandledException", "http.framework_unclassified")]
    public void Adapter_ShouldPreserveMetadataWithoutReadingOrMutatingPayload(int id, string name, string code)
    {
        var sink = new HttpBoundaryTestSink();
        var original = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Fatal, new UnreadableException(),
            new MessageTemplateParser().Parse(HttpBoundaryCapture.Secret + " {payload}"), new[]
            {
                new LogEventProperty("SourceContext", new ScalarValue(HttpBoundaryCapture.Source)),
                new LogEventProperty("EventCode", new ScalarValue("http.failed")),
                new LogEventProperty("EventId", new StructureValue(new[]
                {
                    new LogEventProperty("Id", new ScalarValue(id)),
                    new LogEventProperty("Name", new ScalarValue(name))
                })),
                new LogEventProperty("payload", new ScalarValue(new UnreadableValue()))
            }, ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom());

        new HttpExceptionFrameworkLogSink(sink).Emit(original);

        var result = Assert.Single(sink.Events);
        Assert.NotSame(original, result);
        Assert.Equal(original.Timestamp, result.Timestamp);
        Assert.Equal(original.Level, result.Level);
        Assert.Equal(original.TraceId, result.TraceId);
        Assert.Equal(original.SpanId, result.SpanId);
        Assert.Equal(code, ((ScalarValue)result.Properties["EventCode"]).Value);
        Assert.Equal("other", ((ScalarValue)result.Properties["failureKind"]).Value);
        Assert.Null(result.Exception);
        Assert.DoesNotContain(HttpBoundaryCapture.Secret, result.RenderMessage() + string.Join(" ", result.Properties));
        Assert.IsType<UnreadableException>(original.Exception);
        Assert.True(original.Properties.ContainsKey("payload"));
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public void UnknownEvents_ShouldKeepWarningsAndErrors_WhileDiagnosticsRequireOptIn(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, diagnostics: false);
        using var scope = capture.SensitiveScope();
        var logger = capture.Factory.CreateLogger(HttpBoundaryCapture.Source);
        logger.LogWarning(new EventId(990, "FutureWarning"), new UnreadableException(), "{secret}", HttpBoundaryCapture.Secret);
        logger.LogError(new EventId(991, "FutureError"), new UnreadableException(), "{secret}", HttpBoundaryCapture.Secret);
        logger.LogInformation(new EventId(992, "FutureInfo"), "{secret}", HttpBoundaryCapture.Secret);
        logger.LogDebug(new EventId(993, "FutureDebug"), "{secret}", HttpBoundaryCapture.Secret);
        Assert.Equal(candidate ? 2 : 4, capture.Events.Length);
        Assert.All(capture.Codes, code => Assert.Equal("http.framework_unclassified", code));
        Assert.Equal(new[] { "Warning", "Error" }, capture.Events.Take(2).Select(e => e.Level));
        capture.AssertSafe();
    }

    [Theory]
    [InlineData("Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware.Custom")]
    [InlineData("Microsoft.AspNetCore.Diagnostics.DeveloperExceptionPageMiddleware")]
    [InlineData("Radish.Api.ErrorHandling.ApiExceptionHandler")]
    public void OtherSources_ShouldPassThroughSameEvent(string source)
    {
        var sink = new HttpBoundaryTestSink();
        var original = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Error, new IOException("unchanged"),
            new MessageTemplateParser().Parse("Unchanged"), new[] { new LogEventProperty("SourceContext", new ScalarValue(source)) });
        new HttpExceptionFrameworkLogSink(sink).Emit(original);
        Assert.Same(original, Assert.Single(sink.Events));
    }

    [Fact]
    public void ConfiguredAuditOutput_ShouldReceiveOnlySafeFrameworkEvent()
    {
        using var capture = new HttpBoundaryCapture(false, "Production", audit: true);
        using var scope = capture.SensitiveScope();
        capture.Factory.CreateLogger(HttpBoundaryCapture.Source).LogError(new EventId(1, "UnhandledException"),
            new UnreadableException(), "{secret}", HttpBoundaryCapture.Secret);
        Assert.Equal("http.framework_exception", Assert.Single(capture.Events).Code);
        capture.AssertSafe();
    }

    private sealed class UnreadableValue
    {
        public override string ToString() => throw new InvalidOperationException("Must not render payload");
    }
    private sealed class UnreadableException : Exception
    {
        public override string Message => throw new InvalidOperationException("Must not read Message");
        public override string ToString() => throw new InvalidOperationException("Must not render exception");
    }
}
