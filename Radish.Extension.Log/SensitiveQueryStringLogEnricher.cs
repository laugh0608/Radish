using System.Text.RegularExpressions;
using Serilog.Core;
using Serilog.Events;

namespace Radish.Extension.Log;

/// <summary>
/// 对结构化日志属性中的查询参数凭据统一脱敏，避免 SignalR 与 OAuth 请求泄露令牌。
/// </summary>
public sealed partial class SensitiveQueryStringLogEnricher : ILogEventEnricher
{
    private const string RedactedValue = "[REDACTED]";

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        foreach (var property in logEvent.Properties.ToArray())
        {
            if (property.Value is not ScalarValue { Value: string value })
            {
                continue;
            }

            var sanitizedValue = SensitiveQueryParameterRegex().Replace(
                value,
                match => $"{match.Groups["prefix"].Value}{match.Groups["key"].Value}={RedactedValue}");

            if (!string.Equals(value, sanitizedValue, StringComparison.Ordinal))
            {
                logEvent.AddOrUpdateProperty(new LogEventProperty(property.Key, new ScalarValue(sanitizedValue)));
            }
        }
    }

    [GeneratedRegex(
        "(?<prefix>[?&])(?<key>access_token|refresh_token|id_token|client_secret|code)=[^&#\\s]*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveQueryParameterRegex();
}
