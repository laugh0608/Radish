using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Radish.Api.ErrorHandling;
using Radish.Api.Tests.ErrorHandling;
using Radish.Common.OptionTool;
using Radish.Extension.Log;
using Radish.Gateway.PublicHead;
using Radish.Service;
using Serilog;
using Serilog.Context;
using Serilog.Core.Enrichers;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

namespace Radish.Api.Tests;

public sealed partial class PublicMetadataLoggingTests
{
    private const string Unknown = "runtime.unclassified";
    private static readonly string[] IdentityFields = ["SourceContext", "SourceCategory", "EventCode", "Diagnostic"];
    private static readonly DateTimeOffset OriginalTimestamp = new(2026, 11, 12, 0, 0, 0, TimeSpan.Zero);

    private sealed record RegisteredEvent(Type Source, string Code, LogEventLevel Level = LogEventLevel.Warning,
        int Id = 0, string? Name = null, string Field = "failureKind")
    {
        public string Category => Id == 0 ? "application" : "http";
    }

    private static readonly RegisteredEvent[] Catalog =
    [
        new(typeof(PublicHeadSnapshotService), "public_head.cache_read_failed"),
        new(typeof(PublicHeadSnapshotService), "public_head.cache_write_failed"),
        new(typeof(PublicSitemapService), "sitemap.cache_read_failed"),
        new(typeof(PublicSitemapService), "sitemap.cache_write_failed"),
        new(typeof(PublicSitemapService), "sitemap.section_count_failed"),
        new(typeof(PublicSitemapService), "sitemap.generation_failed", LogEventLevel.Error),
        new(typeof(PublicHeadSnapshotClient), "public_head.snapshot_unavailable", Id: 1, Name: "PublicHeadSnapshotUnavailable", Field: "statusCode"),
        new(typeof(PublicHeadSnapshotClient), "public_head.snapshot_request_failed", Id: 2, Name: "PublicHeadSnapshotRequestFailed"),
        new(typeof(PublicHeadSnapshotClient), "public_head.html_unavailable", Id: 3, Name: "PublicHeadHtmlUnavailable", Field: "statusCode"),
        new(typeof(PublicHeadSnapshotClient), "public_head.html_request_failed", Id: 4, Name: "PublicHeadHtmlRequestFailed"),
        new(typeof(PublicHeadSnapshotMiddleware), "public_head.injection_failed", Id: 1, Name: "PublicHeadInjectionFailed")
    ];

    public static IEnumerable<object[]> OutputModes()
    {
        foreach (var route in HangfireOutputLoggingTests.OutputRoutes())
        foreach (var mel in new[] { false, true })
        foreach (var activity in new[] { false, true })
            yield return route.Concat(new object[] { mel, activity }).ToArray();
    }

    [Theory]
    [MemberData(nameof(HangfireOutputLoggingTests.OutputRoutes), MemberType = typeof(HangfireOutputLoggingTests))]
    public void FinalOutput_ShouldRejectMismatchedIdentityAndSeverity(bool candidate, string environment, bool audit)
    {
        using var output = new HttpBoundaryCapture(candidate, environment, audit: audit);
        foreach (var item in Catalog)
        {
            var identityKey = item.Id == 0 ? "EventCode" : "EventId";
            var raw = Raw(item);
            raw.RemovePropertyIfPresent(identityKey);
            output.Logger.Write(raw);
            CheckUnknown(item, item.Level);

            var mismatched = Raw(item);
            mismatched.AddOrUpdateProperty(new("SourceContext", new ScalarValue(
                item.Source == typeof(PublicHeadSnapshotService) ? typeof(PublicSitemapService).FullName
                : typeof(PublicHeadSnapshotService).FullName)));
            output.Logger.Write(mismatched);
            var wrongSource = item.Source == typeof(PublicHeadSnapshotService) ? typeof(PublicSitemapService) : typeof(PublicHeadSnapshotService);
            CheckUnknown(item with { Source = wrongSource, Id = 0 }, item.Level);

            foreach (var wrongLevel in new[] { LogEventLevel.Debug, item.Level == LogEventLevel.Error ? LogEventLevel.Warning : LogEventLevel.Error })
            {
                output.Logger.Write(Raw(item, level: wrongLevel));
                CheckUnknown(item, wrongLevel);
            }

            if (item.Id == 0) continue;
            foreach (var identity in new LogEventPropertyValue[]
            {
                new ScalarValue(item.Id), EventIdentity(item.Id, Secret), EventIdentity(item.Id + 10, item.Name),
                EventIdentity(item.Id.ToString(), item.Name), new StructureValue([new("Id", new ScalarValue(item.Id))]),
                new StructureValue([new("Id", new ScalarValue(item.Id)), new("Id", new ScalarValue(item.Id)), new("Name", new ScalarValue(item.Name))])
            })
            {
                var invalid = Raw(item);
                invalid.AddOrUpdateProperty(new("EventId", identity));
                // 即使 EventCode 与已登记事件匹配，也必须拒绝非法的命名 EventId。
                output.Logger.Write(invalid);
                CheckUnknown(item, item.Level);
            }
        }

        void CheckUnknown(RegisteredEvent item, LogEventLevel level)
        {
            var diagnostic = level < LogEventLevel.Warning;
            if (candidate && environment == "Production" && diagnostic) Assert.Empty(output.Events);
            else Check(output, item.Source, item.Category, Unknown, level, diagnostic);
            output.Clear();
        }
    }

    [Fact]
    public void CandidateWithoutDiagnostics_ShouldKeepRegisteredFailuresAndDropUnknownDebug()
    {
        using var output = new HttpBoundaryCapture(true, "Development", diagnostics: false);
        var item = Catalog[0];
        output.Logger.Write(Raw(item, level: LogEventLevel.Debug));
        Assert.Empty(output.Events);
        output.Logger.Write(Raw(item));
        Check(output, item.Source, item.Category, item.Code, item.Level, false, ("failureKind", "io"));
    }

    [Fact]
    public void Adapter_ShouldRejectInvalidFieldsAndPreserveTypedCorrelationAndTimestamp()
    {
        using var output = new HttpBoundaryTestSink();
        var adapter = new PublicMetadataLogSink(output);
        using var activity = new Activity("public-metadata-current").SetIdFormat(ActivityIdFormat.W3C).Start();
        var trace = ActivityTraceId.CreateRandom();
        var span = ActivitySpanId.CreateRandom();
        foreach (var item in Catalog)
        {
            var invalidValues = item.Field == "failureKind"
                ? new object[] { Secret, 1 }
                : new object[] { 99, 200, 299, 600, 503L, 503.0, "503" };
            if (item.Code == "public_head.snapshot_unavailable") invalidValues = [.. invalidValues, 404];
            foreach (var invalidValue in invalidValues)
            {
                var raw = Raw(item, trace: trace, span: span);
                raw.AddOrUpdateProperty(new(item.Field, new ScalarValue(invalidValue)));
                adapter.Emit(raw);
                var entry = Assert.Single(output.Events);
                Assert.Equal(item.Code, Scalar(entry, "EventCode"));
                Assert.Equal(item.Level, entry.Level);
                Assert.Equal(IdentityFields.Order(), entry.Properties.Keys.Order());
                Assert.Equal(OriginalTimestamp, entry.Timestamp);
                Assert.Equal(trace, entry.TraceId);
                Assert.Equal(span, entry.SpanId);
                Assert.Null(entry.Exception);
                Assert.DoesNotContain(Secret, entry.RenderMessage());
                output.Events.Clear();
            }
        }

        var html404 = Raw(Catalog.Single(item => item.Code == "public_head.html_unavailable"));
        html404.AddOrUpdateProperty(new("statusCode", new ScalarValue(404)));
        adapter.Emit(html404);
        Assert.Equal(404, Scalar(Assert.Single(output.Events), "statusCode"));
        output.Events.Clear();

        html404.AddOrUpdateProperty(new("SourceContext", new ScalarValue(typeof(PublicHeadSnapshotClient).FullName + ".Adjacent")));
        adapter.Emit(html404);
        Assert.Same(html404, Assert.Single(output.Events));
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public void CodeConsole_ShouldRenderOnlyFixedSummaries(string environment)
    {
        var previous = Console.Out;
        using var text = new StringWriter();
        using var services = new ServiceCollection().BuildServiceProvider();
        try
        {
            Console.SetOut(text);
            var configuration = new LoggerConfiguration();
            SerilogSetup.ConfigureLegacy(configuration, new ConfigurationBuilder().Build(), services, new SerilogOptions
            { Console = new() { Enable = true }, File = new() { Enable = false }, Database = new() { Enable = false } }, environment);
            using var logger = configuration.CreateLogger();
            foreach (var item in Catalog) logger.Write(Raw(item));
            Assert.Equal(Catalog.Length, text.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
            Assert.DoesNotContain(Secret, text.ToString());
            Assert.DoesNotContain("Unrecognized", text.ToString());
            Assert.Contains("Public head cache read failed.", text.ToString());
            Assert.Contains("Sitemap generation failed.", text.ToString());
            Assert.Contains("Frontend HTML response is unavailable.", text.ToString());
            Assert.Contains("Public head HTML injection or response failed.", text.ToString());
        }
        finally { Console.SetOut(previous); }
    }

    private sealed class Capture : IDisposable
    {
        private readonly HttpBoundaryCapture _output;
        private readonly ApplicationLogScope _global;
        private readonly IDisposable _scope;
        private readonly Activity? _activity;
        public Serilog.ILogger Logger => _output.Logger;

        public Capture(bool candidate, string environment, bool audit, bool mel, bool activity, string service = "api")
        {
            _output = new HttpBoundaryCapture(candidate, environment, service, audit: audit);
            _global = new ApplicationLogScope(_output.Logger);
            var polluted = new Dictionary<string, object>
            {
                ["SourceContext"] = typeof(ProductService).FullName!,
                ["EventCode"] = "product.configuration_rejected",
                ["EventId"] = new EventId(99, "Neighbor"), ["SourceCategory"] = "job", ["Diagnostic"] = true,
                ["failureKind"] = "database", ["statusCode"] = 599, ["attempt"] = 12,
                ["traceId"] = "11111111111111111111111111111111", ["spanId"] = "2222222222222222",
                ["operationId"] = "01234567-89ab-4cde-8f01-23456789abcd", ["privatePayload"] = Secret
            };
            _scope = mel ? _output.Factory.CreateLogger("audit.scope").BeginScope(polluted)!
                : LogContext.Push(polluted.Select(p => new PropertyEnricher(p.Key, p.Value)).ToArray());
            if (activity) _activity = new Activity("public-metadata-output").SetIdFormat(ActivityIdFormat.W3C).Start();
        }

        public void AssertSingle(string code, object value)
        {
            var item = Catalog.Single(item => item.Code == code);
            Check(_output, item.Source, item.Category, code, item.Level, false, (item.Field, value));
        }

        public void AssertApiFailure(int status, string kind) =>
            Check(_output, typeof(ApiExceptionHandler), "http", "http.failed", LogEventLevel.Error, false,
                ("statusCode", status), ("failureKind", kind));

        public void AssertQuiet()
        {
            Assert.Empty(_output.Events);
            Assert.Equal("", _output.Emergency.ToString());
        }

        public void Clear() => _output.Clear();
        public void Dispose()
        {
            _activity?.Dispose();
            _scope.Dispose();
            _global.Dispose();
            _output.Dispose();
        }
    }

    private static void Check(HttpBoundaryCapture output, Type source, string category, string code, LogEventLevel level,
        bool diagnostic, params (string Key, object Value)[] fields)
    {
        var summary = Assert.Single(output.Events);
        Assert.Equal(code, summary.Code);
        Assert.Equal(diagnostic, summary.Diagnostic);
        Assert.Equal(output.Candidate && level == LogEventLevel.Debug ? "Info" : level.ToString(), summary.Level);
        Assert.Equal("", output.Emergency.ToString());
        var activity = Activity.Current;
        if (output.Candidate)
        {
            using var document = JsonDocument.Parse(output.Output.ToString());
            var entry = document.RootElement;
            var props = entry.GetProperty("properties");
            Assert.Equal(category, entry.GetProperty("sourceCategory").GetString());
            Assert.Equal(activity?.TraceId.ToHexString(), OptionalString(entry, "traceId"));
            Assert.Equal(activity?.SpanId.ToHexString(), OptionalString(entry, "spanId"));
            Assert.Null(OptionalString(entry, "operationId"));
            Assert.Equal(fields.Select(f => f.Key).Order(), props.EnumerateObject().Select(p => p.Name).Order());
            foreach (var (key, value) in fields)
                Assert.Equal(value, value is string ? props.GetProperty(key).GetString() : (object)props.GetProperty(key).GetInt32());
            Assert.DoesNotContain(Secret, output.Output.ToString());
        }
        else
        {
            var entry = Assert.Single(output.Sink.Events);
            Assert.Equal(source.FullName, Scalar(entry, "SourceContext"));
            Assert.Equal(category, Scalar(entry, "SourceCategory"));
            Assert.Equal(activity?.TraceId, entry.TraceId);
            Assert.Equal(activity?.SpanId, entry.SpanId);
            Assert.Equal(IdentityFields.Concat(fields.Select(f => f.Key)).Order(), entry.Properties.Keys.Order());
            foreach (var (key, value) in fields) Assert.Equal(value, Scalar(entry, key));
            Assert.Null(entry.Exception);
            Assert.DoesNotContain(Secret, entry.RenderMessage() + string.Join(" ", entry.Properties.Values));
        }
    }

    private static string? OptionalString(JsonElement item, string key) =>
        item.TryGetProperty(key, out var value) ? value.GetString() : null;

    private static object? Scalar(LogEvent entry, string key) => Assert.IsType<ScalarValue>(entry.Properties[key]).Value;

    private static LogEvent Raw(RegisteredEvent item, LogEventLevel? level = null, ActivityTraceId trace = default, ActivitySpanId span = default)
    {
        var properties = new List<LogEventProperty>
        {
            new("SourceContext", new ScalarValue(item.Source.FullName)),
            new("EventCode", new ScalarValue(item.Code)),
            new("SourceCategory", new ScalarValue("job")), new("Diagnostic", new ScalarValue(true)),
            new("privatePayload", new ScalarValue(Secret)), new("failureKind", new ScalarValue("io")),
            new("statusCode", new ScalarValue(503)), new("operationId", new ScalarValue("01234567-89ab-4cde-8f01-23456789abcd"))
        };
        if (item.Id != 0) properties.Add(new("EventId", EventIdentity(item.Id, item.Name)));
        return new LogEvent(OriginalTimestamp, level ?? item.Level, new IOException(Secret),
            new MessageTemplateParser().Parse(Secret), properties, trace, span);
    }

    private static StructureValue EventIdentity(object id, string? name) =>
        new([new("Id", new ScalarValue(id)), new("Name", new ScalarValue(name))]);
}
