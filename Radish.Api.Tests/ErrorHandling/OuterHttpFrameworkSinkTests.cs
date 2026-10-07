using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Radish.Extension.Log;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class OuterHttpFrameworkSinkTests
{
    private const string Developer = "Microsoft.AspNetCore.Diagnostics.DeveloperExceptionPageMiddleware";
    private const string Hosting = "Microsoft.AspNetCore.Hosting.Diagnostics";
    private const string Kestrel = "Microsoft.AspNetCore.Server.Kestrel";

    [Theory]
    [InlineData(Developer, 1, "UnhandledException", "http.developer_exception")]
    [InlineData(Developer, 2, "ResponseStarted", "http.developer_response_started")]
    [InlineData(Developer, 3, "DisplayErrorPageException", "http.developer_page_failed")]
    [InlineData(Developer, 4, "RequestAborted", "http.developer_request_aborted")]
    [InlineData(Hosting, 1, null, "http.request_started")]
    [InlineData(Hosting, 2, null, "http.request_finished")]
    [InlineData(Hosting, 16, null, "http.request_unhandled")]
    [InlineData(Hosting, 6, null, "http.host_pipeline_failed")]
    [InlineData(Hosting, 11, null, "http.hosting_startup_failed")]
    [InlineData(Hosting, 15, null, "http.host_binding_overridden")]
    [InlineData(Kestrel, 13, "ApplicationError", "http.server_application_failed")]
    [InlineData(Kestrel, 22, "HeartbeatSlow", "http.server_heartbeat_slow")]
    [InlineData(Kestrel, 23, "ApplicationNeverCompleted", "http.server_application_incomplete")]
    [InlineData(Kestrel, 41, "InvalidResponseHeaderRemoved", "http.server_response_headers_removed")]
    [InlineData(Kestrel, 64, "Http2DisabledWithHttp1AndNoTls", "http.server_protocol_downgraded")]
    [InlineData(Kestrel, 65, "Http3DisabledWithHttp1AndNoTls", "http.server_protocol_downgraded")]
    [InlineData(Kestrel, 66, "RequestAborted", "http.server_request_aborted")]
    [InlineData(Kestrel, 67, "RequestBodyDrainBodyReaderInvalidState", "http.server_body_drain_failed")]
    [InlineData(Developer, 1, null, "http.developer_unclassified")]
    [InlineData(Hosting, 6, "ApplicationError", "http.hosting_unclassified")]
    [InlineData(Hosting, 6, "", "http.hosting_unclassified")]
    [InlineData(Kestrel, 999, "ApplicationError", "http.server_unclassified")]
    public void Adapter_ShouldKeepMetadataAndNeverReadOrMutateOriginalPayload(string source, int id, string? name, string expected)
    {
        var sink = new HttpBoundaryTestSink();
        var original = Event(source, id, new ScalarValue(name));

        new HttpServerFrameworkLogSink(sink).Emit(original);

        var result = Assert.Single(sink.Events);
        Assert.NotSame(original, result);
        Assert.Equal(original.Timestamp, result.Timestamp);
        Assert.Equal(original.Level, result.Level);
        Assert.Equal(original.TraceId, result.TraceId);
        Assert.Equal(original.SpanId, result.SpanId);
        Assert.Equal(expected, ((ScalarValue)result.Properties["EventCode"]).Value);
        Assert.Equal(source, ((ScalarValue)result.Properties["SourceContext"]).Value);
        Assert.Equal("other", ((ScalarValue)result.Properties["failureKind"]).Value);
        Assert.Null(result.Exception);
        Assert.DoesNotContain(HttpBoundaryCapture.Secret, result.RenderMessage() + string.Join(" ", result.Properties));
        Assert.IsType<UnreadableException>(original.Exception);
        Assert.True(original.Properties.ContainsKey("payload"));
    }

    [Fact]
    public void HostingMalformedName_ShouldNotBeTreatedAsMissingName()
    {
        foreach (var name in new LogEventPropertyValue[] { new ScalarValue(6), new ScalarValue(new UnreadableValue()), new SequenceValue(new[] { new ScalarValue("private") }) })
        {
            var sink = new HttpBoundaryTestSink();
            new HttpServerFrameworkLogSink(sink).Emit(Event(Hosting, 6, name));
            Assert.Equal("http.hosting_unclassified", ((ScalarValue)Assert.Single(sink.Events).Properties["EventCode"]).Value);
        }
    }

    [Theory]
    [InlineData("Microsoft.AspNetCore.Server.Kestrel.Connections")]
    [InlineData("Microsoft.AspNetCore.Server.Kestrel.BadRequests")]
    [InlineData("Microsoft.AspNetCore.Server.Kestrel.Http2")]
    [InlineData("Microsoft.AspNetCore.Server.Kestrel.Http3")]
    [InlineData("Microsoft.Extensions.Hosting.Internal.Host")]
    [InlineData("Microsoft.Hosting.Lifetime")]
    [InlineData("Microsoft.AspNetCore.Hosting.Diagnostics.Custom")]
    [InlineData("Microsoft.AspNetCore.Diagnostics.DeveloperExceptionPageMiddleware.Custom")]
    [InlineData(HttpBoundaryCapture.Source)]
    public void OtherSources_ShouldPassThroughSameInstance(string source)
    {
        var sink = new HttpBoundaryTestSink();
        var original = Event(source, 1, new ScalarValue("UnhandledException"));
        new HttpServerFrameworkLogSink(sink).Emit(original);
        Assert.Same(original, Assert.Single(sink.Events));
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public void UnknownEvents_ShouldKeepFaultsVisibleAndRequireDiagnosticOptIn(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, diagnostics: false);
        using var scope = capture.SensitiveScope();
        foreach (var (source, code) in new[] { (Developer, "http.developer_unclassified"), (Hosting, "http.hosting_unclassified"), (Kestrel, "http.server_unclassified") })
        {
            var logger = capture.Factory.CreateLogger(source);
            logger.LogWarning(new EventId(990, "FutureWarning"), new UnreadableException(), "{secret}", HttpBoundaryCapture.Secret);
            logger.LogError(new EventId(991, "FutureError"), new UnreadableException(), "{secret}", HttpBoundaryCapture.Secret);
            logger.LogCritical(new EventId(992, "FutureCritical"), new UnreadableException(), "{secret}", HttpBoundaryCapture.Secret);
            logger.LogInformation(new EventId(993, "FutureInformation"), "{secret}", HttpBoundaryCapture.Secret);
            logger.LogDebug(new EventId(994, "FutureDebug"), "{secret}", HttpBoundaryCapture.Secret);
            Assert.Equal(candidate ? 3 : 5, capture.Events.Length);
            Assert.All(capture.Events, e => Assert.Equal(code, e.Code));
            Assert.Equal(new[] { "Warning", "Error", candidate ? "Error" : "Fatal" }, capture.Events.Take(3).Select(e => e.Level));
            capture.AssertSafe();
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task HostingStartupAssemblyEvent_ShouldNotUseGenericHostStartupFailureFilter(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        var exit = await RuntimeProcess.RunAsync("api", () =>
        {
            capture.Factory.CreateLogger(Hosting).LogCritical(new EventId(11), new UnreadableException(), "{secret}", HttpBoundaryCapture.Secret);
            capture.Factory.CreateLogger("Microsoft.Extensions.Hosting.Internal.Host").LogError(new EventId(11), new UnreadableException(), "{secret}", HttpBoundaryCapture.Secret);
            return Task.CompletedTask;
        }, capture.Emergency);
        Assert.Equal(0, exit);
        var entry = Assert.Single(capture.Events);
        Assert.Equal("http.hosting_startup_failed", entry.Code);
        Assert.Equal(candidate ? "Error" : "Fatal", entry.Level);
        capture.AssertSafe();
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public void HostingGenerationMethods_ShouldKeepCriticalAndWarningPhases(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var scope = capture.SensitiveScope();
        var owner = Assembly.Load("Microsoft.AspNetCore.Hosting").GetType("Microsoft.AspNetCore.Hosting.HostingLoggerExtensions", true)!;
        var logger = capture.Factory.CreateLogger(Hosting);
        owner.GetMethod("ApplicationError", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, new[] { typeof(ILogger), typeof(Exception) })!
            .Invoke(null, new object[] { logger, new UnreadableException() });
        owner.GetMethod("HostingStartupAssemblyError", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, new[] { typeof(ILogger), typeof(Exception) })!
            .Invoke(null, new object[] { logger, new UnreadableException() });
        owner.GetMethod("PortsOverridenByUrls", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, new[] { typeof(ILogger), typeof(string), typeof(string), typeof(string) })!
            .Invoke(null, new object[] { logger, HttpBoundaryCapture.Secret, HttpBoundaryCapture.Secret, HttpBoundaryCapture.Secret });
        Assert.Equal(new[] { "http.host_pipeline_failed", "http.hosting_startup_failed", "http.host_binding_overridden" }, capture.Codes);
        Assert.Equal(new[] { candidate ? "Error" : "Fatal", candidate ? "Error" : "Fatal", "Warning" }, capture.Events.Select(e => e.Level));
        capture.AssertSafe();
    }

    [Fact]
    public void ConfiguredAuditOutput_ShouldReceiveAllThreeAdaptedSources()
    {
        using var capture = new HttpBoundaryCapture(false, "Production", audit: true);
        using var scope = capture.SensitiveScope();
        foreach (var source in new[] { Developer, Hosting, Kestrel })
            capture.Factory.CreateLogger(source).LogError(new EventId(999, "FutureEvent"), new UnreadableException(), "{secret}", HttpBoundaryCapture.Secret);
        Assert.Equal(new[] { "http.developer_unclassified", "http.hosting_unclassified", "http.server_unclassified" }, capture.Codes);
        capture.AssertSafe();
    }

    private static LogEvent Event(string source, int id, LogEventPropertyValue name) => new(DateTimeOffset.UtcNow, LogEventLevel.Fatal,
        new UnreadableException(), new MessageTemplateParser().Parse(HttpBoundaryCapture.Secret + " {payload}"), new[]
        {
            new LogEventProperty("SourceContext", new ScalarValue(source)),
            new LogEventProperty("EventCode", new ScalarValue("http.failed")),
            new LogEventProperty("EventId", new StructureValue(new[]
            {
                new LogEventProperty("Id", new ScalarValue(id)), new LogEventProperty("Name", name)
            })),
            new LogEventProperty("payload", new ScalarValue(new UnreadableValue()))
        }, ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom());

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
