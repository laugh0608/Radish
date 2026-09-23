using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Radish.Common.LogTool;
using Serilog.Events;

namespace Radish.Extension.Log;

/// <summary>覆盖配置加载前、宿主构建与运行失败；不让 CLR 再向 stderr 输出异常原文。</summary>
public static class RuntimeProcess
{
    private static readonly AsyncLocal<bool> ActiveBoundary = new();

    // .NET Host 会先记录 EventId=11 再抛出。仅在本进程边界接管时移交记录责任。
    public static bool OwnsStartupFailure(LogEvent value) => ActiveBoundary.Value &&
        value.Properties.TryGetValue("SourceContext", out var source) &&
        source is ScalarValue { Value: "Microsoft.Extensions.Hosting.Internal.Host" } &&
        value.Properties.TryGetValue("EventId", out var eventId) &&
        eventId is StructureValue details && details.Properties.Any(property =>
            property.Name == "Id" && property.Value is ScalarValue { Value: 11 });

    public static async Task<int> RunAsync(string service, Func<Task> run, TextWriter? error = null)
    {
        var previous = ActiveBoundary.Value;
        ActiveBoundary.Value = true;
        try
        {
            await run();
            return 0;
        }
        catch (HostAbortedException)
        {
            // WebApplicationFactory / EF tooling 的正常宿主中止信号必须交还工具。
            throw;
        }
        catch (Exception exception)
        {
            // 此时配置或 session 可能尚未建立 / 已释放，不能依赖全局 logger 或读取失败的配置。
            var source = new RuntimeLogSource("bootstrap", service, Environment.MachineName, "unversioned");
            var output = new RuntimeLogOutput(new RuntimeLogPolicy(source), source,
                error ?? Console.Error, error ?? Console.Error);
            output.Write(JsonSerializer.SerializeToElement(new
            {
                eventCode = "runtime.failed", level = "Fatal", sourceCategory = "lifecycle",
                properties = new { failureKind = RuntimeFailureSummary.Classify(exception) }
            }));
            return 1;
        }
        finally
        {
            ActiveBoundary.Value = previous;
        }
    }
}
