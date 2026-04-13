using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Radish.Common.HealthTool;

public static class StructuredHealthCheckResponseWriter
{
    public static Task WriteJsonAsync(
        HttpContext context,
        HealthReport report,
        IReadOnlyDictionary<string, string[]>? healthCheckTags = null)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = new
        {
            status = report.Status.ToString(),
            generatedAtUtc = DateTimeOffset.UtcNow,
            totalDuration = report.TotalDuration,
            totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 2),
            entries = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = string.IsNullOrWhiteSpace(entry.Value.Description) ? null : entry.Value.Description,
                duration = entry.Value.Duration,
                durationMs = Math.Round(entry.Value.Duration.TotalMilliseconds, 2),
                tags = healthCheckTags != null && healthCheckTags.TryGetValue(entry.Key, out var tags) ? tags : [],
                exception = entry.Value.Exception?.Message
            })
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        }));
    }
}
