using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Radish.Api.Tests.ErrorHandling;
using Radish.Common.OptionTool;
using Radish.Extension.Log;
using Radish.Service;
using Serilog;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;
using static Radish.Api.Tests.ShopOutputAssertions;

namespace Radish.Api.Tests;

[Collection("Runtime logging global state")]
public sealed class ShopOutputLoggingTests
{
    public static IEnumerable<object[]> OutputScopes()
    {
        foreach (var route in HangfireOutputLoggingTests.OutputRoutes())
        foreach (var mel in new[] { false, true })
            yield return route.Concat(new object[] { mel }).ToArray();
    }

    private static Expected[] RegisteredEvents() =>
    [
        new(typeof(ProductService), "product.configuration_rejected", LogEventLevel.Warning, null),
        new(typeof(ProductService), "product.stock_retrying", LogEventLevel.Warning, null, ("attempt", 1), ("delayMs", 50)),
        new(typeof(OrderService), "order.purchase_failed", LogEventLevel.Error, "io", ("purchaseStage", "payment")),
        new(typeof(OrderService), "order.purchase_failed", LogEventLevel.Error, "io", ("purchaseStage", "fulfillment")),
        new(typeof(OrderService), "order.purchase_interrupted", LogEventLevel.Error, "io"),
        new(typeof(UserInventoryService), "inventory.rename_rejected", LogEventLevel.Warning, "invalid-operation"),
        new(typeof(PaymentPasswordService), "payment.locks_cleared", LogEventLevel.Information, null, ("processedCount", 3)),
        new(typeof(PaymentPasswordService), "payment.suggestions_failed", LogEventLevel.Error, "io")
    ];

    [Theory]
    [MemberData(nameof(OutputScopes))]
    public void RegisteredEvents_ShouldRebuildAllFieldsAfterSerilogOrMelScopes(bool candidate, string environment, bool audit, bool mel)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, audit: audit);
        using var scope = Pollute(capture, mel);
        using var activity = new Activity("shop-output-fields").SetIdFormat(ActivityIdFormat.W3C).Start();
        foreach (var item in RegisteredEvents()) capture.Logger.Write(Raw(item));
        Check(capture, RegisteredEvents());
    }

    [Theory]
    [MemberData(nameof(OutputScopes))]
    public void UnknownIdentityAndInvalidStage_ShouldKeepSeverityWithoutBorrowingScopeIdentity(bool candidate, string environment, bool audit, bool mel)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, audit: audit);
        using var scope = Pollute(capture, mel);
        foreach (var item in RegisteredEvents())
        {
            var key = item.Source == typeof(PaymentPasswordService) ? "EventId" : "EventCode";
            var missing = Raw(item);
            missing.RemovePropertyIfPresent(key);
            capture.Logger.Write(missing);
            AssertUnknown(capture, item.Level);
            var unknown = Raw(item);
            unknown.AddOrUpdateProperty(new(key, new ScalarValue(Secret)));
            capture.Logger.Write(unknown);
            AssertUnknown(capture, item.Level);
            var otherSource = Raw(item);
            otherSource.AddOrUpdateProperty(new("SourceContext", new ScalarValue(
                item.Source == typeof(ProductService) ? typeof(OrderService).FullName : typeof(ProductService).FullName)));
            capture.Logger.Write(otherSource);
            AssertUnknown(capture, item.Level);
            var wrongLevel = Raw(item);
            capture.Logger.Write(new LogEvent(wrongLevel.Timestamp, LogEventLevel.Debug, wrongLevel.Exception,
                wrongLevel.MessageTemplate, wrongLevel.Properties.Select(p => new LogEventProperty(p.Key, p.Value))));
            AssertUnknown(capture, LogEventLevel.Debug);
            if (item.Code == "order.purchase_failed")
            {
                foreach (var stage in new object?[] { null, Secret, 1 })
                {
                    var invalid = Raw(item);
                    invalid.AddOrUpdateProperty(new("purchaseStage", new ScalarValue(stage)));
                    capture.Logger.Write(invalid);
                    AssertUnknown(capture, item.Level);
                }
            }
            if (item.Source == typeof(PaymentPasswordService))
            {
                foreach (var invalid in new[]
                {
                    EventIdentity(1, "PaymentSuggestionsFailed"), EventIdentity(2, "PaymentLocksCleared"),
                    EventIdentity(3, "PaymentLocksCleared"), EventIdentity("1", "PaymentLocksCleared"),
                    new StructureValue([new("Id", new ScalarValue(1))]),
                    new StructureValue([new("Id", new ScalarValue(1)), new("Id", new ScalarValue(1)), new("Name", new ScalarValue("PaymentLocksCleared"))])
                })
                {
                    var value = Raw(item);
                    value.AddOrUpdateProperty(new("EventId", invalid));
                    // 合法的外层事件码也不能补齐缺失或非法的命名 EventId。
                    value.AddOrUpdateProperty(new("EventCode", new ScalarValue(item.Code)));
                    capture.Logger.Write(value);
                    AssertUnknown(capture, item.Level);
                }
            }
        }
    }

    [Fact]
    public void Adapter_ShouldPreserveTimestampTypedCorrelationAndRejectInvalidFieldTypes()
    {
        using var output = new HttpBoundaryTestSink();
        var adapter = new ShopLogSink(output);
        using var activity = new Activity("shop-current-trace").SetIdFormat(ActivityIdFormat.W3C).Start();
        var trace = ActivityTraceId.CreateRandom();
        var span = ActivitySpanId.CreateRandom();
        var timestamp = DateTimeOffset.UtcNow.AddDays(-1);
        foreach (var item in RegisteredEvents())
        {
            var raw = Raw(item);
            raw.AddOrUpdateProperty(new("failureKind", new ScalarValue(Secret)));
            foreach (var (key, _) in item.Fields.Where(p => p.Value is int)) raw.AddOrUpdateProperty(new(key, new ScalarValue(-1)));
            adapter.Emit(new LogEvent(timestamp, item.Level, raw.Exception, raw.MessageTemplate,
                raw.Properties.Select(p => new LogEventProperty(p.Key, p.Value)), trace, span));
            var result = Assert.Single(output.Events);
            Assert.Equal(timestamp, result.Timestamp);
            Assert.Equal(trace, result.TraceId);
            Assert.Equal(span, result.SpanId);
            Assert.Equal(item.Level, result.Level);
            Assert.Equal(item.Code, ReadScalar(result, "EventCode"));
            Assert.Null(result.Exception);
            Assert.Equal(Identity.Concat(item.Fields.Where(p => p.Value is not int).Select(p => p.Key)).Order(), result.Properties.Keys.Order());
            output.Events.Clear();
        }
        foreach (var item in RegisteredEvents().Where(e => e.Fields.Any(f => f.Value is int)))
        foreach (var invalid in new object[] { -1, 1L, 1.0, double.NaN, "1" })
        {
            var raw = Raw(item);
            foreach (var (key, _) in item.Fields) raw.AddOrUpdateProperty(new(key, new ScalarValue(invalid)));
            adapter.Emit(raw);
            Assert.Equal(Identity.Order(), Assert.Single(output.Events).Properties.Keys.Order());
            output.Events.Clear();
        }
        var retry = Raw(RegisteredEvents()[1]);
        retry.AddOrUpdateProperty(new("attempt", new ScalarValue(1_000_001)));
        retry.AddOrUpdateProperty(new("delayMs", new ScalarValue(60_001)));
        adapter.Emit(retry);
        Assert.Equal(Identity.Order(), Assert.Single(output.Events).Properties.Keys.Order());
        output.Events.Clear();
        retry.AddOrUpdateProperty(new("attempt", new ScalarValue(1_000_000)));
        retry.AddOrUpdateProperty(new("delayMs", new ScalarValue(60_000)));
        adapter.Emit(retry);
        Assert.Equal(1_000_000, ReadScalar(Assert.Single(output.Events), "attempt"));
        Assert.Equal(60_000, ReadScalar(Assert.Single(output.Events), "delayMs"));
        output.Events.Clear();
        var clearing = Raw(RegisteredEvents()[6]);
        clearing.AddOrUpdateProperty(new("processedCount", new ScalarValue(0)));
        adapter.Emit(clearing);
        Assert.Equal(Identity.Order(), Assert.Single(output.Events).Properties.Keys.Order());
        output.Events.Clear();
        var adjacent = Raw(RegisteredEvents()[0]);
        adjacent.AddOrUpdateProperty(new("SourceContext", new ScalarValue(typeof(ProductService).FullName + ".Adjacent")));
        adapter.Emit(adjacent);
        Assert.Same(adjacent, Assert.Single(output.Events));
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public void CodeConsole_ShouldRenderOnlyFixedTemplates(string environment)
    {
        var previous = Console.Out;
        using var output = new StringWriter();
        using var services = new ServiceCollection().BuildServiceProvider();
        try
        {
            Console.SetOut(output);
            var config = new LoggerConfiguration();
            SerilogSetup.ConfigureLegacy(config, new ConfigurationBuilder().Build(), services, new SerilogOptions
            { Console = new() { Enable = true }, File = new() { Enable = false }, Database = new() { Enable = false } }, environment);
            using var logger = config.CreateLogger();
            foreach (var item in RegisteredEvents()) logger.Write(Raw(item));
            var text = output.ToString();
            Assert.Equal(8, text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
            Assert.DoesNotContain(Secret, text);
            Assert.DoesNotContain("Unrecognized", text);
            Assert.Contains("Stock update will retry after a conflict.", text);
            Assert.Contains("Purchase stage consumed a failure.", text);
            Assert.Contains("Inventory rename effect rejected.", text);
            Assert.Contains("Expired payment locks cleared.", text);
            Assert.Contains("Payment security suggestions consumed a failure.", text);
        }
        finally { Console.SetOut(previous); }
    }

    private static LogEvent Raw(Expected item)
    {
        var properties = new Dictionary<string, LogEventPropertyValue>
        {
            ["SourceContext"] = new ScalarValue(item.Source.FullName),
            ["EventCode"] = new ScalarValue(item.Source == typeof(PaymentPasswordService) ? Secret : item.Code),
            ["SourceCategory"] = new ScalarValue("job"), ["Diagnostic"] = new ScalarValue(true),
            ["privatePayload"] = new ScalarValue(Secret), ["failureKind"] = new ScalarValue(item.Failure ?? "database"),
            ["operationId"] = new ScalarValue("01234567-89ab-4cde-8f01-23456789abcd"),
            ["traceId"] = new ScalarValue("11111111111111111111111111111111"), ["spanId"] = new ScalarValue("1111111111111111"),
            ["attempt"] = new ScalarValue(999), ["delayMs"] = new ScalarValue(999), ["processedCount"] = new ScalarValue(999),
            ["purchaseStage"] = new ScalarValue("fulfillment"), ["statusCode"] = new ScalarValue(503)
        };
        foreach (var (key, value) in item.Fields) properties[key] = new ScalarValue(value);
        if (item.Source == typeof(PaymentPasswordService)) properties["EventId"] = item.Code == "payment.locks_cleared"
            ? EventIdentity(1, "PaymentLocksCleared") : EventIdentity(2, "PaymentSuggestionsFailed");
        return new LogEvent(DateTimeOffset.UtcNow.AddDays(-1), item.Level, new IOException(Secret), new MessageTemplateParser().Parse(Secret),
            properties.Select(p => new LogEventProperty(p.Key, p.Value)), Activity.Current?.TraceId ?? default, Activity.Current?.SpanId ?? default);
    }

    private static StructureValue EventIdentity(object id, string name) => new(
        [new("Id", new ScalarValue(id)), new("Name", new ScalarValue(name))]);

    private static void AssertUnknown(HttpBoundaryCapture capture, LogEventLevel level)
    {
        if (capture.Candidate && capture.Environment == "Production" && level < LogEventLevel.Warning) Assert.Empty(capture.Events);
        else
        {
            var summary = Assert.Single(capture.Events);
            Assert.Equal("runtime.unclassified", summary.Code);
            Assert.Equal(level < LogEventLevel.Warning, summary.Diagnostic);
            Assert.Null(summary.FailureKind);
            if (capture.Candidate)
            {
                using var json = JsonDocument.Parse(capture.Output.ToString());
                Assert.Empty(json.RootElement.GetProperty("properties").EnumerateObject());
                Assert.False(json.RootElement.TryGetProperty("operationId", out _));
                Assert.DoesNotContain(Secret, capture.Output.ToString());
            }
            else
            {
                var value = Assert.Single(capture.Sink.Events);
                Assert.Equal(level, value.Level);
                Assert.Equal(Identity.Order(), value.Properties.Keys.Order());
                Assert.Null(value.Exception);
                Assert.DoesNotContain(Secret, value.RenderMessage());
            }
        }
        capture.Clear();
    }
}
