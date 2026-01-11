using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Radish.Extension.ExperienceExtension;

/// <summary>
/// 经验值计算扩展注册
/// </summary>
public static class ExperienceExtensionSetup
{
    /// <summary>
    /// 注册经验值计算服务
    /// </summary>
    public static void AddExperienceCalculator(this IServiceCollection services, IConfiguration configuration)
    {
        // 绑定配置
        services.Configure<ExperienceCalculatorOptions>(
            configuration.GetSection("ExperienceCalculator")
        );

        // 注册计算器服务（单例）
        services.AddSingleton<IExperienceCalculator, ExperienceCalculator>();

        Log.Information("经验值计算器扩展已注册");
    }
}
