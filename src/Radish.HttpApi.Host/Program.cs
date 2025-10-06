using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;

namespace Radish;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
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
            await builder.AddApplicationAsync<RadishHttpApiHostModule>();
            
            var app = builder.Build();
            
            // ↓ Scalar 配置 ↓
            if (app.Environment.IsDevelopment())
            {
                // 注入 OpenAPI
                app.MapOpenApi();

                // 注入 Scalar，Api Web UI 管理
                // 文档地址：https://guides.scalar.com/scalar/scalar-api-references/integrations/net-aspnet-core/integration
                // 默认路由地址为：/scalar ，自定义路由地址为：/api-docs
                // 自定义路由方法：app.MapScalarApiReference("/api-docs", options => {});
                app.MapScalarApiReference(options =>
                {
                    options
                        // 多个文档时，这里的标题不起作用，需要单独在 context.Services.AddOpenApi() 中配置
                        .WithTitle("Radish API") // 标题
                        .WithTheme(ScalarTheme.BluePlanet) // 主题
                        // .WithSidebar(false) // 侧边栏
                        .WithDarkMode(false) // 黑暗模式
                        .WithDefaultOpenAllTags(false); // 是否展开所有标签栏
                    // 自定义查找 Open API 文档地址
                    // options.WithOpenApiRoutePattern("/openapi/{documentName}.json");
                    // 设置默认的 Http 客户端
                    options.WithDefaultHttpClient(ScalarTarget.Node, ScalarClient.HttpClient);

                    // 自定义多个版本 API 文档集合，对应 ConfigureScalar 中的文档名称
                    options
                        .AddDocument("v1", "V1", "openapi/v1.json") // 发布 V1
                        .AddDocument("v1beta", "V1Beta", "openapi/v1beta.json", isDefault: true); // 测试 v1beta，默认
                });
            }
            // ↑ Scalar 配置 ↑

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