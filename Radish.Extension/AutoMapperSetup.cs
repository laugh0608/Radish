using System;
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

            var licenseKey = AppSettings.App(new string[] {"AutoMapper", "LicenseKey"}).ObjToString();
            // var licenseKey = configuration["AutoMapper:LicenseKey"];
            if (!string.IsNullOrWhiteSpace(licenseKey))
            {
                expression.LicenseKey = licenseKey;
            }

            expression.ConstructServicesUsing(type =>
            {
                var resolved = provider.GetService(type);
                return resolved ?? Activator.CreateInstance(type)!;
            });

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
