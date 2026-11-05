using Serilog.Events;

namespace Radish.Common.LogTool;

/// <summary>接收链只通过独立诊断报告故障，禁止把接收过程重新送回主日志链。</summary>
public sealed class RuntimeLogIngestScope : IDisposable
{
    private static readonly AsyncLocal<RuntimeLogIngestScope?> Current = new();
    private readonly RuntimeLogIngestScope? _previous = Current.Value;
    private bool _disposed;
    public RuntimeLogIngestScope() => Current.Value = this;

    public static bool Suppress(LogEvent logEvent)
    {
        if (Current.Value is { _disposed: false }) return true;
        // Hosting 起始事件发生在用户 middleware 之前，结束事件发生在 scope 释放之后。
        return logEvent.Properties.TryGetValue("SourceContext", out var source) &&
            source is ScalarValue { Value: "Microsoft.AspNetCore.Hosting.Diagnostics" } &&
            logEvent.Properties.TryGetValue("EventId", out var eventId) && eventId is StructureValue identity &&
            identity.Properties.Any(p => p.Name == "Id" && p.Value is ScalarValue { Value: int id } && id is 1 or 2 or 16) &&
            new[] { "Path", "RequestPath" }.Any(key => logEvent.Properties.TryGetValue(key, out var path) &&
                path is ScalarValue { Value: string value } && IsInternalPath(value));
    }

    public static bool IsInternalPath(string value) => value.Equals("/internal", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("/internal/", StringComparison.OrdinalIgnoreCase);

    public void Dispose()
    {
        _disposed = true;
        if (ReferenceEquals(Current.Value, this)) Current.Value = _previous;
    }
}
