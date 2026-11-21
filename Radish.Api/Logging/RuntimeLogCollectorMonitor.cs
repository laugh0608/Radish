using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Radish.Common.OptionTool;
using Radish.IRepository;
using Radish.Model.DtoModels;

namespace Radish.Api.Logging;

public sealed partial class RuntimeLogCollectorMonitor(Uri endpoint, RuntimeLogIngestOptions ingest,
    IServiceScopeFactory scopes, TimeProvider clock) : BackgroundService
{
    private const int MaxMetricsBytes = 256 * 1024;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var http = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false })
        {
            BaseAddress = endpoint, Timeout = TimeSpan.FromSeconds(3), MaxResponseContentBufferSize = MaxMetricsBytes
        };
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2), clock);
        do
        {
            try
            {
                var metrics = await http.GetStringAsync("/api/v2/metrics/prometheus", stoppingToken);
                using var delivery = JsonDocument.Parse(await http.GetStringAsync("/api/v1/metrics", stoppingToken));
                using var storage = JsonDocument.Parse(await http.GetStringAsync("/api/v1/storage", stoppingToken));
                var output = delivery.RootElement.GetProperty("output");
                var sample = new RuntimeLogPipelineSample(ReadStage(metrics, "chunk_create"), ReadStage(metrics, "chunk_write"),
                    output.GetProperty("runtime_file").GetProperty("proc_records").GetInt64(),
                    output.GetProperty("runtime_http").GetProperty("proc_records").GetInt64(),
                    storage.RootElement.GetProperty("storage_layer").GetProperty("chunks").GetProperty("total_chunks").GetInt32(),
                    clock.GetUtcNow().UtcDateTime,
                    delivery.RootElement.GetProperty("input").GetProperty("guarded_input").GetProperty("records").GetInt64());
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IRuntimeLogConsoleRepository>()
                    .UpdatePipelineAsync(ingest.DeploymentId, sample, stoppingToken);
            }
            catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or
                KeyNotFoundException or JsonException or DbException or FormatException or TimeoutException or OperationCanceledException)
            {
                // 失败时不覆盖最后一份有效采样。查询端据采样时效显示未知。
            }
        } while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken));
    }

    internal static long ReadStage(string metrics, string stage)
    {
        var matches = StorageFailures().Matches(metrics).Where(match =>
            match.Groups["labels"].Value.Split(',').Contains("name=\"guarded_input\"", StringComparer.Ordinal) &&
            match.Groups["labels"].Value.Split(',').Contains("stage=\"" + stage + "\"", StringComparer.Ordinal)).ToArray();
        if (matches.Length != 1) throw new FormatException("Invalid storage metrics.");
        return long.Parse(matches[0].Groups["count"].Value, CultureInfo.InvariantCulture);
    }

    [GeneratedRegex(@"^fluentbit_input_storage_append_failed_records_total\{(?<labels>[^}]+)\}[ \t]+(?<count>\d+)(?:[ \t]+\d+)?\r?$",
        RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex StorageFailures();
}
