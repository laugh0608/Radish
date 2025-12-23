using Microsoft.Extensions.Options;
using Radish.Common.OptionTool;
using Serilog;

namespace Radish.Infrastructure.ImageProcessing;

/// <summary>
/// 图片处理器工厂
/// </summary>
/// <remarks>
/// 根据配置选择使用 C# (ImageSharp) 或 Rust 实现
/// </remarks>
public class ImageProcessorFactory
{
    private readonly IOptions<FileStorageOptions> _options;

    public ImageProcessorFactory(IOptions<FileStorageOptions> options)
    {
        _options = options;
    }

    /// <summary>
    /// 创建图片处理器实例
    /// </summary>
    /// <returns>图片处理器实例</returns>
    public IImageProcessor Create()
    {
        var useRust = _options.Value.ImageProcessing.UseRustExtension;

        if (useRust)
        {
            // 检查 Rust 库是否可用
            if (RustImageProcessor.IsRustLibraryAvailable())
            {
                Log.Information("Using Rust image processor (high performance mode)");
                return new RustImageProcessor(_options);
            }
            else
            {
                Log.Warning("Rust library not available, falling back to C# image processor");
                Log.Warning("To use Rust extensions, build the library: cd Radish.Core/radish-lib && ./build.sh");
                return new CSharpImageProcessor(_options);
            }
        }

        Log.Information("Using C# image processor (ImageSharp)");
        return new CSharpImageProcessor(_options);
    }

    /// <summary>
    /// 获取当前使用的处理器类型
    /// </summary>
    public string GetProcessorType()
    {
        var useRust = _options.Value.ImageProcessing.UseRustExtension;

        if (useRust && RustImageProcessor.IsRustLibraryAvailable())
        {
            return "Rust";
        }

        return "CSharp";
    }

    /// <summary>
    /// 检查 Rust 扩展是否可用
    /// </summary>
    public static bool IsRustExtensionAvailable()
    {
        return RustImageProcessor.IsRustLibraryAvailable();
    }
}
