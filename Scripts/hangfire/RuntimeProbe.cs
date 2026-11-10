using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using Hangfire.Storage.SQLite;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Npgsql;
using Radish.Api.Services;

namespace Radish.HangfireSourceDiagnostics;

public static class RuntimeProbe
{
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Save(string file, object data) => File.WriteAllText(file, System.Text.Json.JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }) + "\n");
    public static async Task<int> Main(string[] args)
    {
        Require(args.Length == 4 && new[] { "sqlite", "pg17", "pg16" }.Contains(args[0]) && new[] { "Debug", "Release" }.Contains(args[2]) && new[] { "original", "source", "marked" }.Contains(args[3]), "Invalid probe case");
        var mode = args[0]; var directory = Path.GetFullPath(args[1]);
        Require(!Directory.Exists(directory), "Probe evidence already exists"); Directory.CreateDirectory(directory);
        var schema = "radish_hf_20261031_c9e4_" + mode + "_" + args[2].ToLowerInvariant() + "_" + args[3];
        GlobalConfiguration.Configuration.SetDataCompatibilityLevel(CompatibilityLevel.Version_180).UseSimpleAssemblyNameTypeSerializer().UseRecommendedSerializerSettings();
        string? postgres = null;
        JobStorage storage;
        if (mode == "sqlite")
        {
            SQLitePCL.Batteries_V2.Init();
            using var sqlite = new SqliteConnection("Data Source=:memory:;Pooling=False"); sqlite.Open();
            using var query = sqlite.CreateCommand(); query.CommandText = "SELECT sqlite_version()";
            Require((string?)query.ExecuteScalar() == "3.53.3", "SQLite version drift");
            storage = new SQLiteStorage(Path.Combine(directory, "hangfire.db"), new SQLiteStorageOptions { QueuePollInterval = TimeSpan.FromMilliseconds(100) });
        }
        else
        {
            Require(Regex.IsMatch(schema, "^radish_hf_20261031_c9e4_pg(17|16)_(debug|release)_(original|source|marked)$"), "Unowned schema");
            postgres = Environment.GetEnvironmentVariable("RADISH_TEST_POSTGRES_CONNECTION_STRING") ?? "";
            var connection = new NpgsqlConnectionStringBuilder(postgres);
            Require(connection.Host == "127.0.0.1" && connection.Port == 15432 && connection.Database == "radish_hf_source" && connection.Username == "radish_hf_source", "Non-test endpoint rejected");
            GlobalConfiguration.Configuration.UsePostgreSqlStorage(options => options.UseNpgsqlConnection(postgres), new PostgreSqlStorageOptions { SchemaName = schema, QueuePollInterval = TimeSpan.FromMilliseconds(100) });
            storage = JobStorage.Current;
        }
        try
        {
            JobStorage.Current = storage;
            await Run(storage, directory, args[3] != "original");
            if (mode == "sqlite")
            {
                var modules = LoadedSqliteModules();Require(modules.Length == 1, "Expected one SQLite module");
                Save(Path.Combine(directory, "sqlite.json"), new { native = modules[0], sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(modules[0]))), version = SQLitePCL.raw.sqlite3_libversion().utf8_to_string(), sourceId = SQLitePCL.raw.sqlite3_sourceid().utf8_to_string() });
            }
            var assemblies = new[] { typeof(BackgroundJob).Assembly, typeof(HangfireServiceCollectionExtensions).Assembly, typeof(HangfireApplicationBuilderExtensions).Assembly, typeof(PostgreSqlStorage).Assembly, typeof(NpgsqlConnection).Assembly, typeof(JsonConvert).Assembly };
            Save(Path.Combine(directory, "assemblies.json"), assemblies.Select(assembly => new { name = assembly.GetName().Name, version = assembly.GetName().Version?.ToString(), path = assembly.Location, sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(assembly.Location))) }));
        }
        finally
        {
            (storage as IDisposable)?.Dispose();
            SqliteConnection.ClearAllPools();
            if (postgres != null)
            {
                NpgsqlConnection.ClearAllPools();
                await using var connection = new NpgsqlConnection(postgres); await connection.OpenAsync();
                await using var drop = new NpgsqlCommand("DROP SCHEMA IF EXISTS \"" + schema + "\" CASCADE", connection); await drop.ExecuteNonQueryAsync();
                await using var query = new NpgsqlCommand("SELECT COUNT(*) FROM information_schema.schemata WHERE schema_name=@schema", connection);query.Parameters.AddWithValue("schema", schema);
                Require(Convert.ToInt64(await query.ExecuteScalarAsync()) == 0, "Schema remains");
            }
            foreach (var file in Directory.GetFiles(directory).Where(file => !file.EndsWith(".json", StringComparison.Ordinal) && !file.EndsWith(".jsonl", StringComparison.Ordinal))) File.Delete(file);
        }
        return 0;
    }
    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "_dyld_image_count")]
    private static extern uint DyldImageCount();

    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "_dyld_get_image_name")]
    private static extern IntPtr DyldImageName(uint index);

    private static string[] LoadedSqliteModules()
    {
        if (!OperatingSystem.IsMacOS())
        {
            using var process = Process.GetCurrentProcess();
            return process.Modules.Cast<ProcessModule>().Where(module => module.ModuleName.Contains("e_sqlite3", StringComparison.Ordinal))
                .Select(module => module.FileName).ToArray();
        }

        var paths = new List<string>();
        var count = DyldImageCount();
        for (uint index = 0; index < count; index++)
        {
            var file = Marshal.PtrToStringUTF8(DyldImageName(index));
            if (file is not null && Path.GetFileName(file) == "libe_sqlite3.dylib") paths.Add(file);
        }
        return paths.ToArray();
    }

    private static async Task Run(JobStorage storage, string directory, bool adaptedLogging)
    {
        CheckLogging(directory, adaptedLogging);
        using var connection = storage.GetConnection();
            var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Hangfire:Shop:DailyStats:Enable"] = "true"
            }).Build();
            ApiRecurringJobRegistration.Register(configuration, zone);
            var jobs = connection.GetRecurringJobs();
            var expectedIds = new[] { "reliable-outbox-dispatch", "cleanup-chat-reaction-operations", "cleanup-wiki-terminal-draft-payloads",
                "cleanup-notification-inbox", "cleanup-deleted-files", "cleanup-temp-files", "cleanup-recycle-bin", "cleanup-orphan-attachments",
                "cleanup-expired-upload-sessions", "cleanup-expired-access-tokens", "comment-highlight-stat", "retention-reward",
                "forum-post-lottery-auto-draw", "shop-cancel-timeout-orders", "shop-mark-expired-benefits", "shop-daily-stats" };
            var schedules = new[] { "*/1 * * * *", "15 4 * * *", "45 3 * * *", "30 3 * * *", "0 3 * * *", "0 * * * *", "0 4 * * *",
                "0 5 * * *", "*/15 * * * *", "0 7 * * *", "0 1 * * *", "0 2 * * 0", "*/1 * * * *", "*/10 * * * *", "0 0 * * *", "0 1 * * *" };
            var methods = new[] { "DispatchAsync", "ExecuteAsync", "ExecuteAsync", "ExecuteAsync", "CleanupDeletedFilesAsync", "CleanupTempFilesAsync",
                "CleanupRecycleBinAsync", "CleanupOrphanAttachmentsAsync", "CleanupExpiredSessionsAsync", "CleanupExpiredTokensAsync", "ExecuteAsync",
                "ExecuteAsync", "ExecuteAutoDrawAsync", "CancelTimeoutOrdersAsync", "MarkExpiredBenefitsAsync", "GenerateDailyStatsAsync" };
            var arguments = new[] { "[50]", "[500]", "[90,200]", "[200,5000]", "[30]", "[2]", "[90]", "[24]", "[]", "[]", "[null]", "[]", "[20]", "[30]", "[]", "[]" };
            Require(jobs.Count == expectedIds.Length, "Recurring registration count mismatch");
            for (var index = 0; index < expectedIds.Length; index++)
            {
                var job = jobs.Single(item => item.Id == expectedIds[index]);
                Require(job.Cron == schedules[index] && job.TimeZoneId == (index < 4 ? TimeZoneInfo.Utc.Id : zone.Id)
                    && job.Job.Method.Name == methods[index] && System.Text.Json.JsonSerializer.Serialize(job.Job.Args) == arguments[index], "Recurring registration contract mismatch: " + job.Id);
                new RecurringJobManager(storage).RemoveIfExists(job.Id);
            }

        const string queue = "hangfire-source-probe";
        var services = new ServiceCollection(); services.AddLogging();services.AddDataProtection().UseEphemeralDataProtectionProvider();services.AddAntiforgery();
        services.AddHangfire(config => config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180).UseSimpleAssemblyNameTypeSerializer().UseRecommendedSerializerSettings().UseStorage(storage));
        services.AddHangfireServer(options => { options.Queues = new[] { queue }; options.WorkerCount = 1; options.SchedulePollingInterval = TimeSpan.FromMilliseconds(100); options.ShutdownTimeout = TimeSpan.FromSeconds(5); });
        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IBackgroundJobClient>();
        Require(provider.GetRequiredService<IRecurringJobManager>() != null, "Recurring DI missing");
        var marker = Path.Combine(directory, "success.txt");var retryMarker = Path.Combine(directory, "retry.txt");
        var successId = client.Create(Job.FromExpression(() => ProbeJobs.Succeed(marker)), new EnqueuedState(queue));
        var retryId = client.Create(Job.FromExpression(() => ProbeJobs.Retry(retryMarker)), new EnqueuedState(queue));
        var allHosted = provider.GetServices<IHostedService>().ToArray();
        var hosted = allHosted.Where(service => service.GetType().Name == "BackgroundJobServerHostedService" && service.GetType().Assembly == typeof(HangfireServiceCollectionExtensions).Assembly).ToArray();
        Require(hosted.Length == 1, "Expected one Hangfire hosted service");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await hosted[0].StartAsync(timeout.Token);
            var timer = Stopwatch.StartNew();
            while (timer.Elapsed < TimeSpan.FromSeconds(25))
            {
                if (connection.GetStateData(successId)?.Name == "Succeeded" && connection.GetStateData(retryId)?.Name == "Succeeded") break;
                await Task.Delay(50, timeout.Token);
            }
            var success = connection.GetStateData(successId);var retry = connection.GetStateData(retryId);
            Save(Path.Combine(directory, "states.json"), new { success, retry });
            Require(success?.Name == "Succeeded" && retry?.Name == "Succeeded", "Jobs did not succeed");
        }
        finally { await hosted[0].StopAsync(CancellationToken.None); }
        Require(File.ReadAllText(marker) == "success" && File.ReadAllText(retryMarker) == "first\nretry", "Retry markers differ");
        var converter = new StringEnumConverter();
        typeof(StringEnumConverter).GetProperty("CamelCaseText")!.SetValue(converter, true);
        var oldJson = JsonConvert.SerializeObject(Enum.GetValues<ProbeMetric>(), converter);
        var newJson = JsonConvert.SerializeObject(Enum.GetValues<ProbeMetric>(), new StringEnumConverter { NamingStrategy = new CamelCaseNamingStrategy() });
        Require(oldJson == newJson, "CamelCase converter adaptation changed output");
        var app = new ApplicationBuilder(provider); app.UseHangfireDashboard("/hf-probe", new DashboardOptions { IgnoreAntiforgeryToken = true }, storage);
        var pipeline = app.Build();
        var dashboard = await Request(pipeline, provider, "/hf-probe/", false);
        Require(dashboard.status == 200 && dashboard.body.Contains("Hangfire", StringComparison.Ordinal) && dashboard.contentType?.Contains("text/html", StringComparison.Ordinal) == true, "In-memory dashboard failed");
        var stats = await Request(pipeline, provider, "/hf-probe/stats", true);
        Require(stats.status == 200 && stats.contentType?.Contains("application/json", StringComparison.Ordinal) == true, "Dashboard JSON dispatcher failed");
        using var payload = JsonDocument.Parse(stats.body);Require(payload.RootElement.ValueKind == JsonValueKind.Object, "Invalid dashboard JSON");
        Save(Path.Combine(directory, "dashboard.json"), new { dashboard.status, dashboard.contentType, htmlSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(dashboard.body))), stats = stats.body, enumSerialization = newJson });
        Save(Path.Combine(directory, "result.json"), new { registered = jobs.Count, successId, retryId, retryAttempts = 2, hostedServices = hosted.Length, otherHostedServices = allHosted.Except(hosted).Select(service => service.GetType().FullName).ToArray(), dashboard.status, statsStatus = stats.status });
    }
    private static async Task<(int status, string? contentType, string body)> Request(RequestDelegate pipeline, IServiceProvider provider, string path, bool form)
    {
        var context = new DefaultHttpContext { RequestServices = provider };context.Connection.RemoteIpAddress = IPAddress.Loopback;context.Connection.LocalIpAddress = IPAddress.Loopback;
        context.Request.Scheme = "http";context.Request.Host = new HostString("localhost");context.Request.Method = form ? "POST" : "GET";context.Request.Path = path;
        if (form) { context.Request.ContentType = "application/x-www-form-urlencoded";context.Request.Form = new FormCollection(new Dictionary<string, StringValues> { ["metrics[]"] = new StringValues(new[] { "succeeded:count", "failed:count" }) }); }
        using var body = new MemoryStream();context.Response.Body = body;await pipeline(context);
        return (context.Response.StatusCode, context.Response.ContentType, Encoding.UTF8.GetString(body.ToArray()));
    }
    private static void CheckLogging(string directory, bool adapted)
    {
        var type = typeof(HangfireServiceCollectionExtensions).Assembly.GetType("Hangfire.AspNetCore.AspNetCoreLog", throwOnError: true)!;
        var target = new ProbeLogger();
        var adapter = (Hangfire.Logging.ILog)Activator.CreateInstance(type, target)!;
        var evaluated = 0;
        target.Enabled = false;
        var disabled = adapter.Log(Hangfire.Logging.LogLevel.Info, () => { evaluated++; return "disabled"; });
        Require(disabled == !adapted && evaluated == (adapted ? 0 : 1) && target.Messages.Count == (adapted ? 0 : 1), "Disabled logger adaptation contract differs");
        var disabledEvaluations = evaluated;
        target.Enabled = true;
        Require(adapter.Log(Hangfire.Logging.LogLevel.Info, () => { evaluated++; return "enabled"; }), "Enabled logger rejected message");
        Require(evaluated == disabledEvaluations + 1 && target.Messages.Last() == "enabled", "Enabled logging count differs");
        Require(adapter.Log(Hangfire.Logging.LogLevel.Info, null), "Enabled level query failed");
        target.Enabled = false;Require(!adapter.Log(Hangfire.Logging.LogLevel.Info, null), "Disabled level query failed");
        target.Enabled = true;
        var expected = new InvalidOperationException("planned log callback failure");
        try { adapter.Log(Hangfire.Logging.LogLevel.Info, () => throw expected);throw new InvalidOperationException("Callback error was swallowed"); }
        catch (InvalidOperationException exception) when (ReferenceEquals(exception, expected)) { }
        target.Failure = expected;
        try { adapter.Log(Hangfire.Logging.LogLevel.Info, () => "logger failure");throw new InvalidOperationException("Logger error was swallowed"); }
        catch (InvalidOperationException exception) when (ReferenceEquals(exception, expected)) { }
        Save(Path.Combine(directory, "logging.json"), new { adapted, disabledResult = disabled, disabledEvaluations, enabled = true, levelQueries = true, callbackExceptionPropagated = true, loggerExceptionPropagated = true });
    }
}
internal sealed class ProbeLogger : Microsoft.Extensions.Logging.ILogger
{
    public bool Enabled { get; set; }
    public Exception? Failure { get; set; }
    public List<string> Messages { get; } = [];
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => Enabled;
    public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (Failure != null) throw Failure;
        Messages.Add(formatter(state, exception));
    }
}
public enum ProbeMetric { NotReady, XMLStats, [EnumMember(Value = "ExactVALUE")] Custom }
[Queue("hangfire-source-probe")]
public static class ProbeJobs
{
    public static void Succeed(string file) => File.WriteAllText(file, "success");
    [AutomaticRetry(Attempts = 1, DelaysInSeconds = new[] { 0 })]
    public static void Retry(string file)
    {
        if (!File.Exists(file)) { File.WriteAllText(file, "first");throw new InvalidOperationException("planned source probe retry"); }
        File.AppendAllText(file, "\nretry");
    }
}
