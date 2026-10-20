using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Radish.Infrastructure.ImageProcessing;

namespace Radish.Extension.ImageProcessingExtension;

public static class ImageProcessorServiceCollectionExtensions
{
    public static IServiceCollection AddImageProcessingSetup(this IServiceCollection services, IConfiguration configuration)
    {
        if (configuration["FileStorage:ImageProcessing:UseRustExtension"] != null)
            throw new InvalidOperationException("Remove the retired FileStorage:ImageProcessing:UseRustExtension setting; native image processing is required.");
        RustImageProcessor.EnsureAvailable();
        services.AddScoped<IImageProcessor, RustImageProcessor>();
        return services;
    }
}
