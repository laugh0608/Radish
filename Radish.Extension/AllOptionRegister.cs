using Microsoft.Extensions.DependencyInjection;
using Radish.Common.OptionTool.Core;

namespace Radish.Extension;

/// <summary>
/// 扫描并注册所有实现 <see cref="IConfigurableOptions" /> 的配置类型，避免手工逐个绑定。
/// </summary>
public static class AllOptionRegister
{
    /// <summary>
    /// 将 <see cref="ConfigurableOptions" /> 所在程序集里所有选项类型批量加入 DI 容器。
    /// </summary>
    /// <param name="services">应用的服务集合</param>
    public static void AddAllOptionRegister(this IServiceCollection services)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));

        foreach (var optionType in typeof(ConfigurableOptions).Assembly.GetTypes().Where(s =>
                     !s.IsInterface && typeof(IConfigurableOptions).IsAssignableFrom(s)))
        {
            services.AddConfigurableOptions(optionType);
        }
    }
}
