using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace Radish.DbMigrator;

class Program
{
    static async Task Main(string[] args)
    {
        // 0) 在 Host 构建前仅预加载 .env（确保环境变量提供器能读取到）。
        // 支持在常见目录（项目根/可执行目录/当前工作目录等）查找 .env。
        try
        {
            var root = Directory.GetCurrentDirectory();
            DotEnv.Preload(root);
        }
        catch { /* ignore */ }

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Volo.Abp", LogEventLevel.Warning)
#if DEBUG
                .MinimumLevel.Override("Radish", LogEventLevel.Debug)
#else
                .MinimumLevel.Override("Radish", LogEventLevel.Information)
#endif
                .Enrich.FromLogContext()
            .WriteTo.Async(c => c.File("Logs/logs.txt"))
            .WriteTo.Async(c => c.Console())
            .CreateLogger();

        await CreateHostBuilder(args).RunConsoleAsync();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .AddAppSettingsSecretsJson()
            .ConfigureAppConfiguration((hostingContext, config) =>
            {
                var env = hostingContext.HostingEnvironment;
                var root = env.ContentRootPath;

                // 仅从 .env 加载（放在后面以覆盖 appsettings/secrets），忽略空值。
                // 为兼容从 bin 目录启动，支持在多个候选根目录查找 .env。
                var roots = new List<string>();

                void add(string? r)
                {
                    if (string.IsNullOrWhiteSpace(r)) return;
                    try { r = Path.GetFullPath(r); } catch { return; }
                    if (!roots.Exists(x => string.Equals(x, r, StringComparison.OrdinalIgnoreCase))) roots.Add(r);
                }

                add(root);
                add(AppContext.BaseDirectory);
                add(Directory.GetCurrentDirectory());
                try { add(Path.Combine(AppContext.BaseDirectory!, "..", "..", "..")); } catch { }
                try { add(Path.Combine(root!, "..", "..", "..")); } catch { }

                var fromEnvDefault = false;
                var fromEnvChrelyonly = false;
                foreach (var r in roots)
                {
                    var envFile = Path.Combine(r, ".env");
                    var data = DotEnv.Read(envFile);
                    if (data is not null && data.Count > 0)
                    {
                        if (!fromEnvDefault && data.TryGetValue("ConnectionStrings:Default", out var v1) && !string.IsNullOrWhiteSpace(v1))
                        {
                            fromEnvDefault = true;
                        }
                        if (!fromEnvChrelyonly && data.TryGetValue("ConnectionStrings:Chrelyonly", out var v2) && !string.IsNullOrWhiteSpace(v2))
                        {
                            fromEnvChrelyonly = true;
                        }
                        config.AddInMemoryCollection(data);
                    }
                }

                // 标记是否已从 .env 明确提供连接字符串，用于运行时强制校验
                config.AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Radish:EnvOnly:ConnectionStringsFromEnv"] = fromEnvDefault ? "true" : "false",
                    ["Radish:EnvOnly:ChrelyonlyFromEnv"] = fromEnvChrelyonly ? "true" : "false",
                });
            })
            .ConfigureLogging((context, logging) => logging.ClearProviders())
            .ConfigureServices((hostContext, services) =>
            {
                services.AddHostedService<DbMigratorHostedService>();
            });
}

internal static class DotEnv
{
    public static void Preload(string root)
    {
        // 仅名为 .env，但在多目录候选中查找（便于从 bin 启动时仍能读取到项目根的 .env）。
        foreach (var candidate in EnumerateCandidateFiles(root))
        {
            Read(candidate);
        }
    }

    private static IEnumerable<string> EnumerateCandidateFiles(string root)
    {
        // 仅保留 .env 名称
        var names = new[] { ".env" };
        var roots = new List<string>();

        void add(string? r)
        {
            if (string.IsNullOrWhiteSpace(r)) return;
            try { r = Path.GetFullPath(r); } catch { return; }
            if (!roots.Exists(x => string.Equals(x, r, StringComparison.OrdinalIgnoreCase))) roots.Add(r);
        }

        add(root);
        add(AppContext.BaseDirectory);
        add(Directory.GetCurrentDirectory());
        try { add(Path.Combine(AppContext.BaseDirectory!, "..", "..", "..")); } catch { }
        try { add(Path.Combine(root!, "..", "..", "..")); } catch { }

        foreach (var r in roots)
        foreach (var n in names)
            yield return Path.Combine(r, n);
    }

    public static IDictionary<string, string> Read(string path)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (!File.Exists(path))
            {
                return dict;
            }

            foreach (var raw in File.ReadAllLines(path))
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                var line = raw.Trim();
                if (line.StartsWith("#")) continue;
                var idx = line.IndexOf('=');
                if (idx <= 0) continue;
                var key = line.Substring(0, idx).Trim();
                var value = line.Substring(idx + 1).Trim();
                if ((value.StartsWith("\"") && value.EndsWith("\"")) || (value.StartsWith("'") && value.EndsWith("'")))
                {
                    value = value.Substring(1, value.Length - 2);
                }

                if (string.IsNullOrEmpty(value))
                {
                    // 空值不写入，避免覆盖 appsettings 的有效默认值
                    continue;
                }

                var normalizedKey = key.Replace("__", ":");
                // 写入进程级环境变量，便于其他读取路径（Host 默认包含 AddEnvironmentVariables）
                Environment.SetEnvironmentVariable(key, value);
                // 同时注入到 IConfiguration（后加入的提供程序将覆盖之前的值）
                dict[normalizedKey] = value;
            }
        }
        catch
        {
            // 忽略 .env 解析错误
        }

        return dict;
    }
}
