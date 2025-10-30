using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Radish.Data;
using Serilog;
using Volo.Abp;
using Volo.Abp.Data;

namespace Radish.DbMigrator;

public class DbMigratorHostedService : IHostedService
{
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly IConfiguration _configuration;

    public DbMigratorHostedService(IHostApplicationLifetime hostApplicationLifetime, IConfiguration configuration)
    {
        _hostApplicationLifetime = hostApplicationLifetime;
        _configuration = configuration;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // 在真正初始化 ABP 之前强制校验：连接串必须来自 .env
        var defaultConn = _configuration.GetConnectionString("Default");
        var onlyFromEnv = string.Equals(_configuration["Radish:EnvOnly:ConnectionStringsFromEnv"], "true", StringComparison.OrdinalIgnoreCase);
        if (!onlyFromEnv || string.IsNullOrWhiteSpace(defaultConn))
        {
            const string hint =
                "未找到有效的 ConnectionStrings:Default。请在 src/Radish.DbMigrator 目录配置 .env 中设置：\n" +
                "ConnectionStrings__Default 与 ConnectionStrings__Chrelyonly。";
            throw new InvalidOperationException(hint);
        }

        using (var application = await AbpApplicationFactory.CreateAsync<RadishDbMigratorModule>(options =>
        {
           options.Services.ReplaceConfiguration(_configuration);
           options.UseAutofac();
           options.Services.AddLogging(c => c.AddSerilog());
           options.AddDataMigrationEnvironment();
        }))
        {
            await application.InitializeAsync();

            await application
                .ServiceProvider
                .GetRequiredService<RadishDbMigrationService>()
                .MigrateAsync();

            await application.ShutdownAsync();

            _hostApplicationLifetime.StopApplication();
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
