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
        // 在构建 Host 之前预加载 .env，确保环境变量提供器可用
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
                // 加载 .env 配置：按优先级顺序追加（后者覆盖前者），放在后面以便覆盖 appsettings/secrets
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    var env = hostingContext.HostingEnvironment;
                    var root = env.ContentRootPath;
                    var files = new[]
                    {
                        Path.Combine(root, ".env"),
                        Path.Combine(root, ".env.product"),
                        Path.Combine(root, ".env.development"),
                        Path.Combine(root, ".env.local"),
                    };
                    foreach (var f in files)
                    {
                        var data = DotEnv.Read(f);
                        if (data is { Count: > 0 })
                        {
                            config.AddInMemoryCollection(data);
                        }
                    }
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
            // 在模块初始化前做关键配置校验：连接字符串必须来自环境变量/.env（不能是占位）
            var conn = builder.Configuration.GetConnectionString("Default");
            if (string.IsNullOrWhiteSpace(conn) || conn.Contains("xxxx", StringComparison.OrdinalIgnoreCase))
            {
                const string hint =
                    "未找到有效的 ConnectionStrings:Default。请在 src/Radish.HttpApi.Host 目录的 .env(.development/.product/.local) 中设置\n" +
                    "ConnectionStrings__Default 与 ConnectionStrings__Chrelyonly，或通过系统环境变量覆盖。";
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
        var names = new[] { ".env", ".env.product", ".env.development", ".env.local" };
        var roots = new List<string>();

        void add(string? r)
        {
            if (string.IsNullOrWhiteSpace(r)) return;
            try { r = Path.GetFullPath(r); } catch { return; }
            if (!roots.Contains(r, StringComparer.OrdinalIgnoreCase)) roots.Add(r);
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
