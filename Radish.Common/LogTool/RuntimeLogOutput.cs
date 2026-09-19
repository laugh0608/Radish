using System.Text.Json;

namespace Radish.Common.LogTool;

/// <summary>生成一次规范事件后输出；失败只能进入有限的安全应急通道，不回显原始输入。</summary>
public sealed class RuntimeLogOutput
{
    private readonly RuntimeLogPolicy _policy;
    private readonly RuntimeLogPolicy _emergencyPolicy;
    private readonly TextWriter _output;
    private readonly TextWriter _emergency;
    private readonly TimeProvider _clock;
    private readonly object _gate = new();
    private long? _lastEmergency;
    private long _failures;
    private long _pendingFailures;

    public long FailureCount => Interlocked.Read(ref _failures);

    public RuntimeLogOutput(RuntimeLogPolicy policy, RuntimeLogSource source, TextWriter output,
        TextWriter emergency, string mode = "Production", TimeProvider? clock = null)
    {
        _policy = policy;
        _clock = clock ?? TimeProvider.System;
        _emergencyPolicy = new RuntimeLogPolicy(source, mode, "Error", clock: _clock);
        _output = output;
        _emergency = emergency;
    }

    public void Write(JsonElement input)
    {
        try
        {
            var value = _policy.Create(input);
            if (value == null) return;
            var line = value.ToJsonLine();
            lock (_gate)
            {
                _output.WriteLine(line);
                _output.Flush();
            }
        }
        catch (Exception)
        {
            ReportFailure();
        }
    }

    // 同时接收 provider 自诊断；不得将 SelfLog 的原文传入这里。
    public void ReportFailure()
    {
        lock (_gate)
        {
            Interlocked.Increment(ref _failures);
            _pendingFailures++;
            var now = _clock.GetTimestamp();
            if (_lastEmergency is { } previous && _clock.GetElapsedTime(previous, now) < TimeSpan.FromMinutes(1)) return;
            _lastEmergency = now;
            try
            {
                var input = JsonSerializer.SerializeToElement(new
                {
                    eventCode = "pipeline.output_failed", level = "Error", sourceCategory = "pipeline",
                    properties = new { count = Math.Min(_pendingFailures, 9_007_199_254_740_991L) }
                });
                _emergency.WriteLine(_emergencyPolicy.Create(input)!.ToJsonLine());
                _emergency.Flush();
                _pendingFailures = 0;
            }
            catch (Exception)
            {
                // 应急介质也可能失效。保留计数，禁止递归或让日志失败替代业务结果。
            }
        }
    }
}
