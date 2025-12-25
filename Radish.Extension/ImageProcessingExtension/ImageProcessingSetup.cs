using Microsoft.Extensions.DependencyInjection;
using Radish.Infrastructure.ImageProcessing;
using Serilog;

namespace Radish.Extension.ImageProcessingExtension;

/// <summary>
/// 图片处理服务注册扩展
/// </summary>
public static class ImageProcessingSetup
{
    /// <summary>
    /// 注册图片处理服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddImageProcessingSetup(this IServiceCollection services)
    {
        // 注册 ImageProcessorFactory
        services.AddSingleton<ImageProcessorFactory>();

        // 注册 IImageProcessor（使用工厂创建）
        services.AddScoped<IImageProcessor>(serviceProvider =>
        {
            var factory = serviceProvider.GetRequiredService<ImageProcessorFactory>();
            var processor = factory.Create();

            // 记录使用的处理器类型
            var processorType = factory.GetProcessorType();
            Log.Information("Image processor initialized: {ProcessorType}", processorType);

            return processor;
        });

        Log.Information("Image processing services registered");

        return services;
    }
}
