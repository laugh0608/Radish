using Microsoft.Extensions.Options;
using Radish.Common.OptionTool;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.Fonts;

namespace Radish.Infrastructure.ImageProcessing;

/// <summary>
/// 基于 ImageSharp 的图片处理实现
/// </summary>
public class CSharpImageProcessor : IImageProcessor
{
    private readonly ImageProcessingOptions _options;

    public CSharpImageProcessor(IOptions<FileStorageOptions> fileStorageOptions)
    {
        _options = fileStorageOptions.Value.ImageProcessing;
    }
    #region GenerateThumbnail

    /// <summary>
    /// 生成缩略图
    /// </summary>
    public async Task<ImageProcessResult> GenerateThumbnailAsync(
        Stream sourceStream,
        string outputPath,
        int width = 0,
        int height = 0,
        int quality = 0)
    {
        try
        {
            // 使用配置的默认值
            if (width <= 0) width = _options.ThumbnailSize.Width;
            if (height <= 0) height = _options.ThumbnailSize.Height;
            if (quality <= 0) quality = _options.CompressQuality;

            sourceStream.Position = 0;

            using var image = await Image.LoadAsync(sourceStream);

            // 计算缩放后的尺寸（保持宽高比）
            var resizeOptions = new ResizeOptions
            {
                Size = new Size(width, height),
                Mode = ResizeMode.Max // 保持宽高比，不超过目标尺寸
            };

            image.Mutate(x => x.Resize(resizeOptions));

            // 确保输出目录存在
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 保存图片
            var encoder = GetEncoder(outputPath, quality);
            await image.SaveAsync(outputPath, encoder);

            var fileInfo = new FileInfo(outputPath);

            return ImageProcessResult.Ok(
                outputPath: outputPath,
                fileSize: fileInfo.Length,
                width: image.Width,
                height: image.Height
            );
        }
        catch (Exception ex)
        {
            return ImageProcessResult.Fail($"生成缩略图失败：{ex.Message}");
        }
    }

    #endregion

    #region Resize

    /// <summary>
    /// 调整图片大小
    /// </summary>
    public async Task<ImageProcessResult> ResizeAsync(
        Stream sourceStream,
        string outputPath,
        int width,
        int height,
        bool keepAspectRatio = true,
        int quality = 85)
    {
        try
        {
            sourceStream.Position = 0;

            using var image = await Image.LoadAsync(sourceStream);

            // 配置缩放选项
            var resizeOptions = new ResizeOptions
            {
                Size = new Size(width, height),
                Mode = keepAspectRatio ? ResizeMode.Max : ResizeMode.Stretch
            };

            // 如果宽度或高度为 0，按比例计算
            if (width == 0 || height == 0)
            {
                var aspectRatio = (double)image.Width / image.Height;
                if (width == 0)
                {
                    width = (int)(height * aspectRatio);
                }
                else
                {
                    height = (int)(width / aspectRatio);
                }
                resizeOptions.Size = new Size(width, height);
            }

            image.Mutate(x => x.Resize(resizeOptions));

            // 确保输出目录存在
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 保存图片
            var encoder = GetEncoder(outputPath, quality);
            await image.SaveAsync(outputPath, encoder);

            var fileInfo = new FileInfo(outputPath);

            return ImageProcessResult.Ok(
                outputPath: outputPath,
                fileSize: fileInfo.Length,
                width: image.Width,
                height: image.Height
            );
        }
        catch (Exception ex)
        {
            return ImageProcessResult.Fail($"调整图片大小失败：{ex.Message}");
        }
    }

    #endregion

    #region Compress

    /// <summary>
    /// 压缩图片
    /// </summary>
    public async Task<ImageProcessResult> CompressAsync(
        Stream sourceStream,
        string outputPath,
        int quality = 0)
    {
        try
        {
            // 使用配置的默认值
            if (quality <= 0) quality = _options.CompressQuality;

            sourceStream.Position = 0;

            using var image = await Image.LoadAsync(sourceStream);

            // 确保输出目录存在
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 使用指定质量保存
            var encoder = GetEncoder(outputPath, quality);
            await image.SaveAsync(outputPath, encoder);

            var fileInfo = new FileInfo(outputPath);

            return ImageProcessResult.Ok(
                outputPath: outputPath,
                fileSize: fileInfo.Length,
                width: image.Width,
                height: image.Height
            );
        }
        catch (Exception ex)
        {
            return ImageProcessResult.Fail($"压缩图片失败：{ex.Message}");
        }
    }

    #endregion

    #region AddWatermark

    /// <summary>
    /// 添加水印
    /// </summary>
    public async Task<ImageProcessResult> AddWatermarkAsync(
        Stream sourceStream,
        string outputPath,
        WatermarkOptions options)
    {
        try
        {
            sourceStream.Position = 0;

            using var image = await Image.LoadAsync(sourceStream);

            // 根据水印类型处理
            if (options.Type == WatermarkType.Text)
            {
                // 文字水印
                await AddTextWatermarkAsync(image, options);
            }
            else if (options.Type == WatermarkType.Image && !string.IsNullOrEmpty(options.ImagePath))
            {
                // 图片水印
                await AddImageWatermarkAsync(image, options);
            }

            // 确保输出目录存在
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 保存图片
            var encoder = GetEncoder(outputPath, 85);
            await image.SaveAsync(outputPath, encoder);

            var fileInfo = new FileInfo(outputPath);

            return ImageProcessResult.Ok(
                outputPath: outputPath,
                fileSize: fileInfo.Length,
                width: image.Width,
                height: image.Height
            );
        }
        catch (Exception ex)
        {
            return ImageProcessResult.Fail($"添加水印失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 添加文字水印
    /// </summary>
    private async Task AddTextWatermarkAsync(Image image, WatermarkOptions options)
    {
        // 创建字体
        var fontCollection = new FontCollection();
        // 使用系统默认字体（简化处理，生产环境可能需要指定字体文件）
        if (!SystemFonts.Families.Any())
        {
            throw new Exception("未找到可用字体");
        }
        var fontFamily = SystemFonts.Families.First();
        var font = fontFamily.CreateFont(options.FontSize, FontStyle.Regular);

        // 计算水印位置
        var textOptions = new RichTextOptions(font)
        {
            Origin = CalculateWatermarkPosition(image, options.Text, font, options.Position, options.Padding)
        };

        // 添加水印
        image.Mutate(x => x.DrawText(
            textOptions,
            options.Text,
            Color.ParseHex(options.Color).WithAlpha(options.Opacity)
        ));

        await Task.CompletedTask;
    }

    /// <summary>
    /// 添加图片水印
    /// </summary>
    private async Task AddImageWatermarkAsync(Image image, WatermarkOptions options)
    {
        if (string.IsNullOrEmpty(options.ImagePath) || !File.Exists(options.ImagePath))
        {
            throw new Exception($"水印图片不存在：{options.ImagePath}");
        }

        using var watermarkImage = await Image.LoadAsync(options.ImagePath);

        // 计算水印图片的目标尺寸（相对于原图宽度）
        var targetWidth = (int)(image.Width * options.Scale);
        var aspectRatio = (float)watermarkImage.Height / watermarkImage.Width;
        var targetHeight = (int)(targetWidth * aspectRatio);

        // 缩放水印图片
        watermarkImage.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new Size(targetWidth, targetHeight),
            Mode = ResizeMode.Max
        }));

        // 设置水印透明度
        watermarkImage.Mutate(x => x.Opacity(options.Opacity));

        // 计算水印位置
        var position = CalculateImageWatermarkPosition(
            image,
            watermarkImage,
            options.Position,
            options.Padding
        );

        // 添加水印
        image.Mutate(x => x.DrawImage(watermarkImage, position, 1f));
    }

    #endregion

    #region RemoveExif

    /// <summary>
    /// 移除 EXIF 信息
    /// </summary>
    public async Task<bool> RemoveExifAsync(Stream sourceStream, string outputPath)
    {
        try
        {
            sourceStream.Position = 0;

            using var image = await Image.LoadAsync(sourceStream);

            // 移除 EXIF 信息
            image.Metadata.ExifProfile = null;

            // 确保输出目录存在
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 保存图片
            await image.SaveAsync(outputPath);

            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region GenerateMultipleSizes

    /// <summary>
    /// 生成多个尺寸的图片
    /// </summary>
    public async Task<List<ImageProcessResult>> GenerateMultipleSizesAsync(
        Stream sourceStream,
        string baseOutputPath,
        List<ImageSize> sizes)
    {
        var results = new List<ImageProcessResult>();

        foreach (var size in sizes)
        {
            try
            {
                // 为每个尺寸生成输出路径
                // baseOutputPath 应该包含扩展名，例如：/path/to/file.jpg
                var directory = Path.GetDirectoryName(baseOutputPath);
                var fileNameWithoutExt = Path.GetFileNameWithoutExtension(baseOutputPath);
                var extension = Path.GetExtension(baseOutputPath);

                var outputPath = Path.Combine(directory ?? "", $"{fileNameWithoutExt}_{size.Name}{extension}");

                // 重置流位置
                sourceStream.Position = 0;

                var result = await ResizeAsync(
                    sourceStream,
                    outputPath,
                    size.Width,
                    size.Height,
                    size.KeepAspectRatio,
                    size.Quality
                );

                results.Add(result);
            }
            catch (Exception ex)
            {
                results.Add(ImageProcessResult.Fail($"生成 {size.Name} 尺寸失败：{ex.Message}"));
            }
        }

        return results;
    }

    #endregion

    #region GetImageInfo

    /// <summary>
    /// 获取图片信息
    /// </summary>
    public async Task<ImageInfo?> GetImageInfoAsync(Stream stream)
    {
        try
        {
            stream.Position = 0;

            var imageInfo = await Image.IdentifyAsync(stream);
            if (imageInfo == null)
            {
                return null;
            }

            var fileSize = stream.Length;

            return new ImageInfo
            {
                Width = imageInfo.Width,
                Height = imageInfo.Height,
                Format = imageInfo.Metadata.DecodedImageFormat?.Name ?? "Unknown",
                FileSize = fileSize,
                HasExif = imageInfo.Metadata.ExifProfile != null,
                BitsPerPixel = imageInfo.PixelType?.BitsPerPixel
            };
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region IsValidImage

    /// <summary>
    /// 验证图片是否有效
    /// </summary>
    public async Task<bool> IsValidImageAsync(Stream stream)
    {
        try
        {
            stream.Position = 0;
            var imageInfo = await Image.IdentifyAsync(stream);
            return imageInfo != null;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region Private Helper Methods

    /// <summary>
    /// 根据文件扩展名获取编码器
    /// </summary>
    private SixLabors.ImageSharp.Formats.IImageEncoder GetEncoder(string filePath, int quality)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        return extension switch
        {
            ".jpg" or ".jpeg" => new JpegEncoder { Quality = quality },
            ".png" => new PngEncoder(),
            ".webp" => new WebpEncoder { Quality = quality },
            _ => new JpegEncoder { Quality = quality }
        };
    }

    /// <summary>
    /// 解析颜色
    /// </summary>
    private Color ParseColor(string colorHex)
    {
        try
        {
            return Color.ParseHex(colorHex);
        }
        catch
        {
            return Color.White; // 默认白色
        }
    }

    /// <summary>
    /// 计算水印位置
    /// </summary>
    private PointF CalculateWatermarkPosition(
        Image image,
        string text,
        Font font,
        WatermarkPosition position,
        int padding)
    {
        // 测量文字尺寸
        var textSize = TextMeasurer.MeasureSize(text, new RichTextOptions(font));

        return position switch
        {
            WatermarkPosition.TopLeft => new PointF(padding, padding),
            WatermarkPosition.TopRight => new PointF(image.Width - textSize.Width - padding, padding),
            WatermarkPosition.BottomLeft => new PointF(padding, image.Height - textSize.Height - padding),
            WatermarkPosition.BottomRight => new PointF(
                image.Width - textSize.Width - padding,
                image.Height - textSize.Height - padding
            ),
            WatermarkPosition.Center => new PointF(
                (image.Width - textSize.Width) / 2,
                (image.Height - textSize.Height) / 2
            ),
            _ => new PointF(padding, padding)
        };
    }

    /// <summary>
    /// 计算图片水印位置
    /// </summary>
    private Point CalculateImageWatermarkPosition(
        Image image,
        Image watermarkImage,
        WatermarkPosition position,
        int padding)
    {
        return position switch
        {
            WatermarkPosition.TopLeft => new Point(padding, padding),
            WatermarkPosition.TopRight => new Point(image.Width - watermarkImage.Width - padding, padding),
            WatermarkPosition.BottomLeft => new Point(padding, image.Height - watermarkImage.Height - padding),
            WatermarkPosition.BottomRight => new Point(
                image.Width - watermarkImage.Width - padding,
                image.Height - watermarkImage.Height - padding
            ),
            WatermarkPosition.Center => new Point(
                (image.Width - watermarkImage.Width) / 2,
                (image.Height - watermarkImage.Height) / 2
            ),
            _ => new Point(padding, padding)
        };
    }

    #endregion
}
