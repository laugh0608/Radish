using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

namespace Radish.Extension.Log;

/// <summary>按精确生成身份重建商城、库存与支付口令的运行事件。</summary>
internal sealed class ShopLogSink(ILogEventSink next) : ILogEventSink
{
    private const string Product = "Radish.Service.ProductService";
    private const string Order = "Radish.Service.OrderService";
    private const string Inventory = "Radish.Service.UserInventoryService";
    private const string Payment = "Radish.Service.PaymentPasswordService";
    private const string Unknown = "runtime.unclassified";

    private static readonly Dictionary<string, MessageTemplate> Templates = new(StringComparer.Ordinal)
    {
        ["product.configuration_rejected"] = new MessageTemplateParser().Parse("Product configuration rejected purchase."),
        ["product.stock_retrying"] = new MessageTemplateParser().Parse("Stock update will retry after a conflict."),
        ["order.purchase_failed"] = new MessageTemplateParser().Parse("Purchase stage consumed a failure."),
        ["order.purchase_interrupted"] = new MessageTemplateParser().Parse("Purchase interrupted before returning a result."),
        ["inventory.rename_rejected"] = new MessageTemplateParser().Parse("Inventory rename effect rejected."),
        ["payment.locks_cleared"] = new MessageTemplateParser().Parse("Expired payment locks cleared."),
        ["payment.suggestions_failed"] = new MessageTemplateParser().Parse("Payment security suggestions consumed a failure."),
        [Unknown] = new MessageTemplateParser().Parse("Unrecognized shop, inventory or payment event.")
    };

    public void Emit(LogEvent value)
    {
        if (Scalar(value, "SourceContext") is not { Value: string source } ||
            source is not (Product or Order or Inventory or Payment))
        {
            next.Emit(value);
            return;
        }

        var stage = Scalar(value, "purchaseStage");
        var code = (source, Scalar(value, "EventCode")?.Value, value.Level) switch
        {
            (Product, "product.configuration_rejected", LogEventLevel.Warning) => "product.configuration_rejected",
            (Product, "product.stock_retrying", LogEventLevel.Warning) => "product.stock_retrying",
            (Order, "order.purchase_failed", LogEventLevel.Error) when stage?.Value is "payment" or "fulfillment" => "order.purchase_failed",
            (Order, "order.purchase_interrupted", LogEventLevel.Error) => "order.purchase_interrupted",
            (Inventory, "inventory.rename_rejected", LogEventLevel.Warning) => "inventory.rename_rejected",
            (Payment, _, LogEventLevel.Information) when Identity(value) == (1, "PaymentLocksCleared") => "payment.locks_cleared",
            (Payment, _, LogEventLevel.Error) when Identity(value) == (2, "PaymentSuggestionsFailed") => "payment.suggestions_failed",
            _ => Unknown
        };
        var properties = new List<LogEventProperty>
        {
            new("SourceContext", new ScalarValue(source)),
            new("EventCode", new ScalarValue(code)),
            new("SourceCategory", new ScalarValue("application")),
            new("Diagnostic", new ScalarValue(code == Unknown && value.Level < LogEventLevel.Warning))
        };

        if (code == "order.purchase_failed") properties.Add(new("purchaseStage", stage!));
        if (code == "product.stock_retrying")
        {
            if (Scalar(value, "attempt") is { Value: int and >= 0 and <= 1_000_000 } attempt)
                properties.Add(new("attempt", attempt));
            if (Scalar(value, "delayMs") is { Value: int and >= 0 and <= 60_000 } delay)
                properties.Add(new("delayMs", delay));
        }
        if (code == "payment.locks_cleared" && Scalar(value, "processedCount") is { Value: int and > 0 } count)
            properties.Add(new("processedCount", count));
        if (code is "order.purchase_failed" or "order.purchase_interrupted" or "inventory.rename_rejected" or
            "payment.suggestions_failed" && Scalar(value, "failureKind") is
            { Value: "aggregate" or "database" or "cancelled" or "timeout" or "io" or "access-denied" or "argument" or "invalid-operation" or "other" } failure)
            properties.Add(new("failureKind", failure));

        var activity = Activity.Current is { IdFormat: ActivityIdFormat.W3C } current ? current : null;
        next.Emit(new LogEvent(value.Timestamp, value.Level, null, Templates[code], properties,
            value.TraceId ?? activity?.TraceId ?? default, value.SpanId ?? activity?.SpanId ?? default));
    }

    private static ScalarValue? Scalar(LogEvent value, string key) => value.Properties.TryGetValue(key, out var property)
        ? property as ScalarValue : null;

    private static (int?, string?) Identity(LogEvent value)
    {
        if (!value.Properties.TryGetValue("EventId", out var identity) || identity is not StructureValue details)
            return (null, null);
        var ids = details.Properties.Where(p => p.Name == "Id").ToArray();
        var names = details.Properties.Where(p => p.Name == "Name").ToArray();
        return ids.Length == 1 && ids[0].Value is ScalarValue { Value: int id } &&
            names.Length == 1 && names[0].Value is ScalarValue { Value: string name } ? (id, name) : (null, null);
    }
}
