using System.Globalization;
using System.Text.Json;

namespace Radish.Common.LogTool;

/// <summary>程序集诊断独立于 App、DI 和主日志器；只输出固定事件与异常类型分类。</summary>
public static class AssemblyDiagnosticReporter
{
    private static readonly AsyncLocal<DiagnosticScope?> Current = new();
    private static readonly AsyncLocal<bool> Reporting = new();
    private static readonly object OutputGate = new();
    private static readonly DiagnosticScope Unbound = new("runtime", null, null);

    /// <summary>在宿主委托执行前绑定来源，不读取配置或创建日志输出；不拥有 writer。</summary>
    public static IDisposable BeginScope(string service, TextWriter? error = null)
    {
        var scope = new DiagnosticScope(service is "api" or "auth" or "gateway" or "dbmigrate" ? service : "runtime",
            error, Current.Value);
        Current.Value = scope;
        return scope;
    }

    public static void AssemblyLoadFailed(Exception exception) => Report("runtime.assembly_load_failed", exception);

    public static void AssemblyTypesFailed(Exception exception) => Report("runtime.assembly_types_failed", exception);

    private static void Report(string eventCode, Exception exception)
    {
        // ExecutionContext 流入的同步 / 异步 writer 回调也不能递归进入本通道。
        if (Reporting.Value) return;
        Reporting.Value = true;
        try
        {
            lock (OutputGate)
            {
                var scope = FindActive(Current.Value) ?? Unbound;
                scope.Write(JsonSerializer.SerializeToElement(new
                {
                    eventCode, level = "Error", sourceCategory = "infrastructure",
                    properties = new { failureKind = RuntimeFailureSummary.Classify(exception) }
                }));
            }
        }
        catch (Exception)
        {
            // 分类、策略初始化或序列化也可能失败。禁止回显或让诊断毒化 App 静态初始化。
            // writer 失败由 RuntimeLogOutput 的有限应急通道处理；此处不递归报告。
        }
        finally
        {
            Reporting.Value = false;
        }
    }

    private static DiagnosticScope? FindActive(DiagnosticScope? scope)
    {
        while (scope is { IsDisposed: true }) scope = scope.Previous;
        return scope;
    }

    private sealed class DiagnosticScope(string service, TextWriter? error, DiagnosticScope? previous) : IDisposable
    {
        private int _disposed;
        private TextWriter? _writer;
        private RuntimeLogOutput? _output;
        public DiagnosticScope? Previous { get; } = previous;
        public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

        // 调用方持有 OutputGate；不同来源共享同一介质时，完整 JSON 行也保持串行。
        public void Write(JsonElement input)
        {
            var writer = error ?? Console.Error;
            if (_output == null || !ReferenceEquals(_writer, writer))
            {
                var source = new RuntimeLogSource("bootstrap", service,
                    "process-" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture), "unversioned");
                var output = new RuntimeLogOutput(new RuntimeLogPolicy(source, minimumLevel: "Error"), source, writer, writer);
                _output = output;
                _writer = writer;
            }
            _output.Write(input);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            // 外层先释放不清除仍存活的内层；继承旧上下文的任务在报告时跳过已释放作用域。
            if (ReferenceEquals(Current.Value, this)) Current.Value = FindActive(Previous);
        }
    }
}
