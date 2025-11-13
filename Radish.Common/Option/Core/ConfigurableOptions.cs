using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Radish.Common.Core;

namespace Radish.Common.Option.Core;

/// <summary>
/// 提供注册 <see cref="IConfigurableOptions" /> 实现的统一入口，避免重复的配置绑定代码。
/// </summary>
public static class ConfigurableOptions
{
    // /// <summary>
    // /// 由 <see cref="ConfigureApplication" /> 设置的全局配置引用，用于读取具体的配置节。
    // /// </summary>
    // internal static IConfiguration Configuration;
    //
    // /// <summary>
    // /// 缓存应用启动时构建好的 <see cref="IConfiguration" />，供后续选项注册使用。
    // /// </summary>
    // /// <param name="configuration">程序初始化阶段注入的配置对象</param>
    // public static void ConfigureApplication(this IConfiguration configuration)
    // {
    //     Configuration = configuration;
    // }

    /// <summary>
    /// 通过配置节名称绑定 `TOptions`，便于在其它层中使用 `IOptions&lt;TOptions&gt;` 获取强类型配置。
    /// </summary>
    /// <typeparam name="TOptions">实现 <see cref="IConfigurableOptions" /> 的选项类型</typeparam>
    /// <param name="services">应用的服务集合</param>
    /// <returns>原始服务集合，便于链式调用</returns>
    public static IServiceCollection AddConfigurableOptions<TOptions>(this IServiceCollection services)
        where TOptions : class, IConfigurableOptions
    {
        Type optionsType = typeof(TOptions);
        string path = GetConfigurationPath(optionsType);
        services.Configure<TOptions>(App.Configuration.GetSection(path));

        return services;
    }

    /// <summary>
    /// 通过运行时类型注册配置选项，适合配合反射扫描进行批量注册。
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="type">实现 <see cref="IConfigurableOptions" /> 的选项类型</param>
    /// <returns>原始服务集合，便于链式调用</returns>
    public static IServiceCollection AddConfigurableOptions(this IServiceCollection services, Type type)
    {
        string path = GetConfigurationPath(type);
        var config = App.Configuration.GetSection(path);

        Type iOptionsChangeTokenSource = typeof(IOptionsChangeTokenSource<>);
        Type iConfigureOptions = typeof(IConfigureOptions<>);
        Type configurationChangeTokenSource = typeof(ConfigurationChangeTokenSource<>);
        Type namedConfigureFromConfigurationOptions = typeof(NamedConfigureFromConfigurationOptions<>);
        iOptionsChangeTokenSource = iOptionsChangeTokenSource.MakeGenericType(type);
        iConfigureOptions = iConfigureOptions.MakeGenericType(type);
        configurationChangeTokenSource = configurationChangeTokenSource.MakeGenericType(type);
        namedConfigureFromConfigurationOptions = namedConfigureFromConfigurationOptions.MakeGenericType(type);

        services.AddOptions();
        services.AddSingleton(iOptionsChangeTokenSource,
            Activator.CreateInstance(configurationChangeTokenSource,
                Options.DefaultName, config) ??
            throw new InvalidOperationException());
        return services.AddSingleton(iConfigureOptions,
            Activator.CreateInstance(namedConfigureFromConfigurationOptions,
                Options.DefaultName, config) ??
            throw new InvalidOperationException());
    }

    /// <summary>获取配置路径</summary>
    /// <param name="optionsType">选项类型</param>
    /// <returns>去掉 Option/Options 后缀后的配置节名称</returns>
    public static string GetConfigurationPath(Type optionsType)
    {
        // 自动剥离常见的后缀，保持配置节名称更简洁
        var endPath = new[] { "Option", "Options" };
        var configurationPath = optionsType.Name;
        foreach (var s in endPath)
        {
            if (configurationPath.EndsWith(s))
            {
                return configurationPath[..^s.Length];
            }
        }

        return configurationPath;
    }
}