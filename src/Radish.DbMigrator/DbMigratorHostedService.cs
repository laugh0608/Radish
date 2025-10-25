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
        // 在真正初始化 ABP 之前先校验连接串是否已通过 .env/环境变量覆盖
        var defaultConn = _configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(defaultConn) || defaultConn.Contains("xxxx", StringComparison.OrdinalIgnoreCase))
        {
            const string hint =
                "未找到有效的 ConnectionStrings:Default。请在 src/Radish.DbMigrator 目录配置 .env(.development/.product/.local) 中的\n" +
                "ConnectionStrings__Default 与 ConnectionStrings__Chrelyonly，或通过系统环境变量覆盖。";
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
