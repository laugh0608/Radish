using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace Radish;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        // 在构建 Host 之前仅预加载 .env，确保环境变量提供器可用；
        // 支持在项目根/可执行目录/当前工作目录等常见位置搜索 .env。
        try
        {
            DotEnv.Preload(Directory.GetCurrentDirectory());
        }
        catch { /* ignore */ }
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Async(c => c.File("Logs/logs.txt"))
            .WriteTo.Async(c => c.Console())
            .CreateBootstrapLogger();

        try
        {
            Log.Information("Starting Radish.HttpApi.Host.");
            var builder = WebApplication.CreateBuilder(args);

            builder.Host
                .AddAppSettingsSecretsJson()
                // 仅从 .env 加载：在多个候选根目录查找，后加入者覆盖前者，以便覆盖 appsettings/secrets
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    var env = hostingContext.HostingEnvironment;
                    var root = env.ContentRootPath;
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
                        var f = Path.Combine(r, ".env");
                        var data = DotEnv.Read(f);
                        if (data is { Count: > 0 })
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

                    // 写入标记，用于启动时校验连接字符串来源
                    config.AddInMemoryCollection(new Dictionary<string, string>
                    {
                        ["Radish:EnvOnly:ConnectionStringsFromEnv"] = fromEnvDefault ? "true" : "false",
                        ["Radish:EnvOnly:ChrelyonlyFromEnv"] = fromEnvChrelyonly ? "true" : "false",
                    });
                })
                .UseAutofac()
                .UseSerilog((context, services, loggerConfiguration) =>
                {
                    loggerConfiguration
#if DEBUG
                        .MinimumLevel.Debug()
#else
                        .MinimumLevel.Information()
#endif
                        .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                        .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                        .Enrich.FromLogContext()
                        .WriteTo.Async(c => c.File("Logs/logs.txt"))
                        .WriteTo.Async(c => c.Console())
                        .WriteTo.Async(c => c.AbpStudio(services));
                });
            // 在模块初始化前做关键配置校验：连接字符串必须来自 .env
            var conn = builder.Configuration.GetConnectionString("Default");
            var onlyFromEnv = string.Equals(builder.Configuration["Radish:EnvOnly:ConnectionStringsFromEnv"], "true", StringComparison.OrdinalIgnoreCase);
            if (!onlyFromEnv || string.IsNullOrWhiteSpace(conn))
            {
                const string hint =
                    "未找到有效的 ConnectionStrings:Default。请在 src/Radish.HttpApi.Host 目录的 .env 中设置：\n" +
                    "ConnectionStrings__Default 与 ConnectionStrings__Chrelyonly。";
                throw new InvalidOperationException(hint);
            }

            await builder.AddApplicationAsync<RadishHttpApiHostModule>();
            
            var app = builder.Build();

            await app.InitializeApplicationAsync();
            await app.RunAsync();
            return 0;
        }
        catch (Exception ex)
        {
            if (ex is HostAbortedException)
            {
                throw;
            }

            Log.Fatal(ex, "Host terminated unexpectedly!");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}

internal static class DotEnv
{
    public static void Preload(string root)
    {
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
            // if (!roots.Contains(r, StringComparer.OrdinalIgnoreCase)) roots.Add(r);
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

                // 允许通过 .env 覆盖环境变量与配置；空值不写入
                if (!string.IsNullOrEmpty(value))
                {
                    // 兼容 KEY__SUBKEY 与 KEY:SUBKEY 两种写法，配置字典使用冒号分隔
                    var normalizedKey = key.Replace("__", ":");
                    Environment.SetEnvironmentVariable(key, value);
                    dict[normalizedKey] = value;
                }
            }
        }
        catch
        {
            // 忽略 .env 解析错误，保持启动不中断
        }

        return dict;
    }
}
