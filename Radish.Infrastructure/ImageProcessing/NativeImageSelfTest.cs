using Microsoft.Extensions.Options;
using Radish.Common.OptionTool;

namespace Radish.Infrastructure.ImageProcessing;

/// <summary>镜像与发布物自检：不启动宿主、不连接数据库、不监听端口。</summary>
public static class NativeImageSelfTest
{
    public static async Task RunAsync(CancellationToken cancellationToken = default)
    {
        RustImageProcessor.EnsureAvailable();
        var directory = Path.Combine(Path.GetTempPath(), $"radish-native-check-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var options = new FileStorageOptions();
            options.Watermark.Text.Font.UseRelativeSize = false;
            var processor = new RustImageProcessor(Options.Create(options));
            // Fixed 1 × 1 transparent PNG; no external fixture or network needed.
            using var source = new MemoryStream(Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="));
            var resized = await processor.ResizeAsync(source, Path.Combine(directory, "source.png"), 256, 128,
                keepAspectRatio: false, cancellationToken: cancellationToken);
            if (!resized.Success) throw new InvalidOperationException("Native resize self-test failed");
            await using var image = File.OpenRead(resized.OutputPath);
            var result = await processor.AddWatermarkAsync(image, Path.Combine(directory, "result.webp"),
                new WatermarkOptions { Text = "Radish 图片", FontSize = 18, Padding = 4 },
                cancellationToken: cancellationToken);
            if (!result.Success) throw new InvalidOperationException("Native watermark self-test failed");
            await using var output = File.OpenRead(result.OutputPath);
            var info = await processor.GetImageInfoAsync(output, cancellationToken);
            if (info is not { Format: "WEBP", Width: 256, Height: 128 })
                throw new InvalidOperationException("Native output self-test failed");
        }
        finally { Directory.Delete(directory, true); }
    }
}
