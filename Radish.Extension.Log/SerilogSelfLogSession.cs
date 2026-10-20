using System.Text.Json;
using Radish.Common.LogTool;
using Serilog.Debugging;

namespace Radish.Extension.Log;

/// <summary>SelfLog 没有结构化等级或调用方身份；仅向当前会话的独立应急通道报告固定摘要。</summary>
internal sealed class SerilogSelfLogSession : IDisposable
{
    private const long MaxCount = 9_007_199_254_740_991L;
    private static readonly object RegistrationGate = new();
    private static readonly List<SerilogSelfLogSession> Sessions = [];
    private readonly object _gate = new();
    private readonly RuntimeLogPolicy _policy;
    private readonly TextWriter _output;
    private readonly TimeProvider _clock;
    private long? _lastReport;
    private long _pending;
    private bool _writing;
    private bool _disposed;

    internal SerilogSelfLogSession(RuntimeLogSource source, string mode, TextWriter output, TimeProvider? clock = null)
    {
        _clock = clock ?? TimeProvider.System;
        _policy = new RuntimeLogPolicy(source, mode, "Warning", clock: _clock);
        _output = output;
        lock (RegistrationGate)
        {
            Sessions.Add(this);
            // 原文已由 Serilog 生成；这里不解析、复制或转发，也不再调用任何 logger。
            SelfLog.Enable(static _ => ReportCurrent());
        }
    }

    private static void ReportCurrent()
    {
        SerilogSelfLogSession? current;
        lock (RegistrationGate) current = Sessions.LastOrDefault();
        // 不持有全局注册锁执行外部 writer，避免与并发释放形成锁顺序反转。
        current?.Report();
    }

    private void Report()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _pending = Math.Min(_pending + 1, MaxCount);
            if (_writing) return;
            var now = _clock.GetTimestamp();
            if (_lastReport is { } previous && _clock.GetElapsedTime(previous, now) < TimeSpan.FromMinutes(1)) return;
            _lastReport = now;
            var count = _pending;
            _writing = true;
            try
            {
                var input = JsonSerializer.SerializeToElement(new
                {
                    eventCode = "pipeline.self_diagnostic", level = "Warning", sourceCategory = "pipeline",
                    properties = new { count }
                });
                _output.WriteLine(_policy.Create(input)!.ToJsonLine());
                _output.Flush();
                // writer 若重入 SelfLog，新增加的计数留给下一个时间窗口。
                _pending -= count;
            }
            catch (Exception)
            {
                // 输出可能部分成功；保留计数供下一次诊断触发重试，不递归、不替代业务异常。
            }
            finally
            {
                _writing = false;
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
        }
        lock (RegistrationGate)
        {
            Sessions.Remove(this);
            if (Sessions.Count == 0) SelfLog.Disable();
        }
    }
}
