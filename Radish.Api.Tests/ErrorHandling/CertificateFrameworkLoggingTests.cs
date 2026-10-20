using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Radish.Extension.Log;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class CertificateFrameworkLoggingTests
{
    public static IEnumerable<object[]> Events()
    {
        yield return Row(true, 1, "DirectoryDoesNotExist", "directory_missing", LogLevel.Warning);
        yield return Row(true, 2, "UnknownFile", "file_unknown", LogLevel.Warning);
        yield return Row(true, 3, "UnknownObserver", "observer_unknown", LogLevel.Warning);
        yield return Row(true, 4, "CreatedDirectoryWatcher", "directory_created", LogLevel.Debug);
        yield return Row(true, 5, "CreatedFileWatcher", "file_created", LogLevel.Debug);
        yield return Row(true, 6, "RemovedDirectoryWatcher", "directory_removed", LogLevel.Debug);
        yield return Row(true, 7, "RemovedFileWatcher", "file_removed", LogLevel.Debug);
        yield return Row(true, 8, "LastModifiedTimeError", "timestamp_failed", LogLevel.Debug);
        yield return Row(true, 9, "UntrackedFileEvent", "untracked_event", LogLevel.Debug);
        yield return Row(true, 10, "ReusedObserver", "observer_reused", LogLevel.Trace);
        yield return Row(true, 11, "AddedObserver", "observer_added", LogLevel.Trace);
        yield return Row(true, 12, "RemovedObserver", "observer_removed", LogLevel.Trace);
        yield return Row(true, 13, "ObserverCount", "observer_count", LogLevel.Trace);
        yield return Row(true, 14, "FileCount", "file_count", LogLevel.Trace);
        yield return Row(true, 15, "FlaggedObservers", "changed", LogLevel.Trace);
        yield return Row(true, 16, "EventWithoutFile", "file_unavailable", LogLevel.Trace);
        yield return Row(false, 0, "LocatedDevelopmentCertificate", "development_selected", LogLevel.Debug);
        yield return Row(false, 1, "UnableToLocateDevelopmentCertificate", "development_missing", LogLevel.Debug);
        yield return Row(false, 2, "FailedToLocateDevelopmentCertificateFile", "development_file_missing", LogLevel.Debug);
        yield return Row(false, 3, "FailedToLoadDevelopmentCertificate", "development_load_failed", LogLevel.Debug);
        yield return Row(false, 4, "BadDeveloperCertificateState", "development_invalid", LogLevel.Error);
        yield return Row(false, 5, "DeveloperCertificateFirstRun", "development_setup_required", LogLevel.Warning);
        yield return Row(false, 6, "MissingOrInvalidCertificateFile", "file_invalid", LogLevel.Error, "FailedToLoadCertificate");
        yield return Row(false, 7, "MissingOrInvalidCertificateKeyFile", "key_invalid", LogLevel.Error, "FailedToLoadCertificateKey");
        yield return Row(false, 8, "DeveloperCertificateNotTrusted", "development_untrusted", LogLevel.Warning);
        yield return Row(false, 9, "DeveloperCertificatePartiallyTrusted", "development_partially_trusted", LogLevel.Warning);
    }

    private static object[] Row(bool watcher, int id, string name, string suffix, LogLevel level, string? method = null) =>
        new object[] { watcher, id, name, "http.certificate_" + (watcher ? "watch_" : "config_") + suffix, level, method ?? name };

    [Theory]
    [MemberData(nameof(Events))]
    public void IdentityAndSafeMapping_ShouldMatchInstalledFramework(bool watcher, int id, string name, string code, LogLevel level, string method)
    {
        var metadata = Generator(watcher, method).GetCustomAttribute<LoggerMessageAttribute>()!;
        Assert.Equal(id, metadata.EventId);
        Assert.Equal(name, metadata.EventName);
        Assert.Equal(level, metadata.Level);
        var original = Event(watcher ? CertificateLoggingFramework.Watcher : CertificateLoggingFramework.Config,
            new StructureValue(new[] { new LogEventProperty("Id", new ScalarValue(id)), new LogEventProperty("Name", new ScalarValue(name)) }));
        var sink = new HttpBoundaryTestSink();
        new HttpServerFrameworkLogSink(sink).Emit(original);
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
        Assert.IsType<UnreadableCertificateException>(original.Exception);
        Assert.True(original.Properties.ContainsKey("payload"));
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public void RealGenerators_ShouldRespectLevelsAndDiagnosticSwitch(bool candidate, string environment)
    {
        foreach (var diagnostics in new[] { true, false })
        {
            using var capture = new HttpBoundaryCapture(candidate, environment, diagnostics: diagnostics);
            using var scope = capture.SensitiveScope();
            foreach (var row in Events())
            {
                var generator = Generator((bool)row[0], (string)row[5]);
                var args = generator.GetParameters().Select(p => Argument(p.ParameterType, capture.Factory)).ToArray();
                generator.Invoke(null, args);
                var level = (LogLevel)row[4];
                if (!candidate || level >= LogLevel.Warning || (environment == "Development" && diagnostics))
                {
                    var logged = Assert.Single(capture.Events);
                    Assert.Equal(row[3], logged.Code);
                    Assert.Equal(level < LogLevel.Warning, logged.Diagnostic);
                    Assert.Equal(candidate && level < LogLevel.Warning ? "Info" : level == LogLevel.Trace ? "Verbose" : level.ToString(), logged.Level);
                    Assert.Equal((bool)row[0] && (int)row[1] == 8 ? "io" : null, logged.FailureKind);
                }
                else Assert.Empty(capture.Events);
                CertificateLoggingFramework.AssertSafe(capture);
                capture.Clear();
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConfiguredOutputs_ShouldReceiveRealGenerators(bool audit)
    {
        using var capture = new HttpBoundaryCapture(false, "Production", audit: audit);
        using var scope = capture.SensitiveScope();
        foreach (var row in Events())
        {
            var method = Generator((bool)row[0], (string)row[5]);
            method.Invoke(null, method.GetParameters().Select(p => Argument(p.ParameterType, capture.Factory)).ToArray());
        }
        Assert.Equal(Events().Select(row => (string)row[3]), capture.Codes);
        CertificateLoggingFramework.AssertSafe(capture);
    }

    [Fact]
    public void MissingZeroId_ShouldOnlyMatchExactSourceAndNameAndRejectMalformedIdentity()
    {
        var name = new LogEventProperty("Name", new ScalarValue("LocatedDevelopmentCertificate"));
        var nameOnly = new StructureValue(new[] { name });
        AssertCode(CertificateLoggingFramework.Config, nameOnly, "http.certificate_config_development_selected");
        AssertCode(CertificateLoggingFramework.Watcher, nameOnly, "http.certificate_watch_unclassified");
        AssertCode("Microsoft.AspNetCore.Server.Kestrel", nameOnly, "http.server_unclassified");
        foreach (var id in new LogEventPropertyValue[] { new ScalarValue(null), new ScalarValue("0"), new ScalarValue(0L),
            new ScalarValue(new UnreadableCertificateValue()), new SequenceValue(Array.Empty<LogEventPropertyValue>()) })
            AssertCode(CertificateLoggingFramework.Config, new StructureValue(new[] { name, new LogEventProperty("Id", id) }), "http.certificate_config_unclassified");
        foreach (var malformed in new LogEventPropertyValue[] { new ScalarValue(0), new ScalarValue(null),
            new StructureValue(Array.Empty<LogEventProperty>()), new StructureValue(new[] { new LogEventProperty("Name", new ScalarValue(1)) }) })
            AssertCode(CertificateLoggingFramework.Config, malformed, "http.certificate_config_unclassified");
        foreach (var row in Events())
        {
            var source = (bool)row[0] ? CertificateLoggingFramework.Watcher : CertificateLoggingFramework.Config;
            foreach (var wrongName in new string?[] { null, "", (string)row[2] + "Wrong" })
                AssertCode(source, new StructureValue(new[] { new LogEventProperty("Id", new ScalarValue(row[1])),
                    new LogEventProperty("Name", new ScalarValue(wrongName)) }), (bool)row[0] ? "http.certificate_watch_unclassified" : "http.certificate_config_unclassified");
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public void UnknownEvents_ShouldKeepFaultsVisibleWithoutDiagnosticOptIn(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, diagnostics: false);
        using var scope = capture.SensitiveScope();
        foreach (var source in new[] { CertificateLoggingFramework.Watcher, CertificateLoggingFramework.Config })
        {
            var logger = capture.Factory.CreateLogger(source);
            foreach (var level in new[] { LogLevel.Warning, LogLevel.Error, LogLevel.Critical, LogLevel.Information, LogLevel.Debug, LogLevel.Trace })
                logger.Log(level, new EventId(999, "FutureEvent"), new UnreadableCertificateException(), "{payload}", HttpBoundaryCapture.Secret);
            Assert.Equal(candidate ? 3 : 6, capture.Events.Length);
            Assert.Equal(new[] { "Warning", "Error", candidate ? "Error" : "Fatal" }, capture.Events.Take(3).Select(e => e.Level));
            Assert.All(capture.Events, e => Assert.Equal(source == CertificateLoggingFramework.Watcher ? "http.certificate_watch_unclassified" : "http.certificate_config_unclassified", e.Code));
            CertificateLoggingFramework.AssertSafe(capture);
            capture.Clear();
        }
    }

    private static MethodInfo Generator(bool watcher, string method) => CertificateLoggingFramework.Type(watcher
        ? "Microsoft.AspNetCore.Server.Kestrel.Core.Internal.CertificatePathWatcherLoggerExtensions"
        : "Microsoft.AspNetCore.Server.Kestrel.Core.Internal.LoggerExtensions").GetMethods(CertificateLoggingFramework.Members)
        .Single(m => m.Name == method && m.GetCustomAttribute<LoggerMessageAttribute>() != null);

    private static object Argument(Type type, ILoggerFactory factory)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ILogger<>))
            return CertificateLoggingFramework.Logger(type.GetGenericArguments()[0].FullName!, factory);
        if (type == typeof(string)) return HttpBoundaryCapture.Secret;
        if (type == typeof(int)) return 2;
        if (type == typeof(Exception)) return new IOException(HttpBoundaryCapture.Secret);
        throw new InvalidOperationException("Unexpected framework generator signature: " + type.Name);
    }

    private static void AssertCode(string source, LogEventPropertyValue identity, string code)
    {
        var sink = new HttpBoundaryTestSink();
        new HttpServerFrameworkLogSink(sink).Emit(Event(source, identity));
        Assert.Equal(code, ((ScalarValue)Assert.Single(sink.Events).Properties["EventCode"]).Value);
    }

    private static LogEvent Event(string source, LogEventPropertyValue identity) => new(DateTimeOffset.UtcNow, LogEventLevel.Fatal,
        new UnreadableCertificateException(), new MessageTemplateParser().Parse(HttpBoundaryCapture.Secret + " {payload}"), new[]
        {
            new LogEventProperty("SourceContext", new ScalarValue(source)), new LogEventProperty("EventId", identity),
            new LogEventProperty("payload", new ScalarValue(new UnreadableCertificateValue()))
        }, ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom());
}
