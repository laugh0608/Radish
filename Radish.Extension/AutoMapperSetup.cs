using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Radish.Common;
using IConfiguration = Microsoft.Extensions.Configuration.IConfiguration;

namespace Radish.Extension;

/// <summary>Automapper 启动服务</summary>
public static class AutoMapperSetup
{
    public static IServiceCollection AddAutoMapperSetup(this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton(provider =>
        {
            var expression = new MapperConfigurationExpression();
            AutoMapperConfig.RegisterProfiles(expression);

            // LicenseKey 优先从 AppSettings 统一入口读取，支持 Secret Manager / 环境变量等多种来源
            var licenseKey = AppSettings.App(new string[] {"AutoMapper", "LicenseKey"}).ObjToString();
            // var licenseKey = configuration["AutoMapper:LicenseKey"];
            if (!string.IsNullOrWhiteSpace(licenseKey))
            {
                expression.LicenseKey = licenseKey;
            }

            // 先尝试从容器解析 Profile/Converter 中声明的依赖，缺失时再回退到 Activator
            expression.ConstructServicesUsing(type =>
            {
                var resolved = provider.GetService(type);
                return resolved ?? Activator.CreateInstance(type)!;
            });

            // 传入 ILoggerFactory 以启用 AutoMapper 内部诊断日志
            var loggerFactory = provider.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
            return new MapperConfiguration(expression, loggerFactory);
        });

        services.AddSingleton<IConfigurationProvider>(sp =>
            sp.GetRequiredService<MapperConfiguration>());
        services.AddSingleton<IMapper>(sp =>
        {
            var mapperConfiguration = sp.GetRequiredService<MapperConfiguration>();
            return mapperConfiguration.CreateMapper(sp.GetService);
        });

        return services;
    }
}
