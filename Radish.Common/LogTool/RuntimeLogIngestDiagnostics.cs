using System.Globalization;
using System.Text.Json;

namespace Radish.Common.LogTool;

/// <summary>数据库初始化与入库故障的独立有界 stderr 通道；不调用 Serilog 或数据库。</summary>
public sealed class RuntimeLogIngestDiagnostics(TimeProvider? clock = null, TextWriter? writer = null)
{
    public static RuntimeLogIngestDiagnostics DatabaseSetup { get; } = new();
    private readonly object _gate = new();
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private long _pending;
    private long? _last;
    private bool _writing;

    public void Report(string operation, Exception? exception = null)
    {
        lock (_gate)
        {
            _pending = Math.Min(_pending + 1, 9_007_199_254_740_991L);
            if (_writing) return;
            var now = _clock.GetTimestamp();
            if (_last is { } last && _clock.GetElapsedTime(last, now) < TimeSpan.FromMinutes(1)) return;
            _last = now;
            _writing = true;
            var count = _pending;
            try
            {
                var policy = new RuntimeLogPolicy(new RuntimeLogSource("bootstrap", "runtime",
                    "process-" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture), "unversioned"), clock: _clock);
                var input = JsonSerializer.SerializeToElement(new
                {
                    eventCode = "pipeline.ingest_failed", level = "Warning", sourceCategory = "pipeline",
                    properties = new { count, ingestOperation = operation, failureKind = exception == null ? "other" : RuntimeFailureSummary.Classify(exception) }
                });
                var output = writer ?? Console.Error;
                output.WriteLine(policy.Create(input)!.ToJsonLine());
                output.Flush();
                _pending -= count;
            }
            catch (Exception)
            {
                // 应急输出失败保持计数；不递归、不改变调用方原失败或返回值。
            }
            finally { _writing = false; }
        }
    }
}
