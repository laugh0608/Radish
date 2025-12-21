using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Radish.Common.OptionTool;
using Radish.Infrastructure.ImageProcessing;
using Shouldly;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Radish.Api.Tests;

/// <summary>
/// 图片处理器测试
/// </summary>
public class ImageProcessorTest
{
    private readonly IImageProcessor _imageProcessor;
    private readonly string _testOutputPath;

    public ImageProcessorTest()
    {
        // 配置图片处理选项
        var fileStorageOptions = new FileStorageOptions
        {
            ImageProcessing = new ImageProcessingOptions
            {
                ThumbnailSize = new ImageProcessing.ImageSize
                {
                    Width = 150,
                    Height = 150
                },
                CompressQuality = 85
            }
        };

        var options = Options.Create(fileStorageOptions);
        _imageProcessor = new CSharpImageProcessor(options);

        // 创建测试输出目录
        _testOutputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestOutput");
        if (!Directory.Exists(_testOutputPath))
        {
            Directory.CreateDirectory(_testOutputPath);
        }
    }

    /// <summary>
    /// 创建测试图片
    /// </summary>
    private Stream CreateTestImage(int width = 800, int height = 600)
    {
        var image = new Image<Rgba32>(width, height);

        // 填充渐变色背景
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var r = (byte)(x * 255 / width);
                var g = (byte)(y * 255 / height);
                var b = (byte)((x + y) * 255 / (width + height));
                image[x, y] = new Rgba32(r, g, b);
            }
        }

        var stream = new MemoryStream();
        image.SaveAsJpeg(stream);
        stream.Position = 0;
        return stream;
    }

    [Fact(DisplayName = "测试添加文字水印")]
    public async Task AddTextWatermark_ShouldSucceed()
    {
        // Arrange
        var sourceStream = CreateTestImage(800, 600);
        var outputPath = Path.Combine(_testOutputPath, "watermark_test.jpg");

        var watermarkOptions = new WatermarkOptions
        {
            Type = WatermarkType.Text,
            Text = "Radish Test",
            FontSize = 24,
            Opacity = 0.5f,
            Position = WatermarkPosition.BottomRight,
            Color = "#FFFFFF",
            Padding = 10
        };

        // Act
        var result = await _imageProcessor.AddWatermarkAsync(sourceStream, outputPath, watermarkOptions);

        // Assert
        result.ShouldNotBeNull();
        result.Success.ShouldBeTrue($"水印添加失败：{result.ErrorMessage}");
        File.Exists(outputPath).ShouldBeTrue("输出文件不存在");

        var fileInfo = new FileInfo(outputPath);
        fileInfo.Length.ShouldBeGreaterThan(0, "输出文件大小为0");

        // 验证输出文件是有效的图片
        using var outputImage = await Image.LoadAsync(outputPath);
        outputImage.Width.ShouldBe(800);
        outputImage.Height.ShouldBe(600);

        // 清理
        sourceStream.Dispose();
    }

    [Fact(DisplayName = "测试生成缩略图")]
    public async Task GenerateThumbnail_ShouldSucceed()
    {
        // Arrange
        var sourceStream = CreateTestImage(800, 600);
        var outputPath = Path.Combine(_testOutputPath, "thumbnail_test.jpg");

        // Act
        var result = await _imageProcessor.GenerateThumbnailAsync(
            sourceStream,
            outputPath,
            150,
            150,
            85
        );

        // Assert
        result.ShouldNotBeNull();
        result.Success.ShouldBeTrue($"缩略图生成失败：{result.ErrorMessage}");
        File.Exists(outputPath).ShouldBeTrue("输出文件不存在");

        // 验证缩略图尺寸
        using var outputImage = await Image.LoadAsync(outputPath);
        outputImage.Width.ShouldBeLessThanOrEqualTo(150);
        outputImage.Height.ShouldBeLessThanOrEqualTo(150);

        // 清理
        sourceStream.Dispose();
    }

    [Fact(DisplayName = "测试生成多尺寸图片")]
    public async Task GenerateMultipleSizes_ShouldSucceed()
    {
        // Arrange
        var sourceStream = CreateTestImage(1920, 1080);
        var baseOutputPath = Path.Combine(_testOutputPath, "multi_size_test.jpg");

        var sizes = new List<ImageSize>
        {
            new() { Name = "small", Width = 400, Height = 300, KeepAspectRatio = true, Quality = 85 },
            new() { Name = "medium", Width = 800, Height = 600, KeepAspectRatio = true, Quality = 85 },
            new() { Name = "large", Width = 1200, Height = 900, KeepAspectRatio = true, Quality = 85 }
        };

        // Act
        var results = await _imageProcessor.GenerateMultipleSizesAsync(
            sourceStream,
            baseOutputPath,
            sizes
        );

        // Assert
        results.ShouldNotBeNull();
        results.Count.ShouldBe(3);

        foreach (var result in results)
        {
            result.Success.ShouldBeTrue($"生成 {result.OutputPath} 失败：{result.ErrorMessage}");
            File.Exists(result.OutputPath).ShouldBeTrue($"文件不存在：{result.OutputPath}");
        }

        // 验证文件尺寸
        var smallPath = Path.Combine(_testOutputPath, "multi_size_test_small.jpg");
        using var smallImage = await Image.LoadAsync(smallPath);
        smallImage.Width.ShouldBeLessThanOrEqualTo(400);

        var mediumPath = Path.Combine(_testOutputPath, "multi_size_test_medium.jpg");
        using var mediumImage = await Image.LoadAsync(mediumPath);
        mediumImage.Width.ShouldBeLessThanOrEqualTo(800);

        var largePath = Path.Combine(_testOutputPath, "multi_size_test_large.jpg");
        using var largeImage = await Image.LoadAsync(largePath);
        largeImage.Width.ShouldBeLessThanOrEqualTo(1200);

        // 清理
        sourceStream.Dispose();
    }

    [Fact(DisplayName = "测试水印位置 - 左上角")]
    public async Task AddWatermark_TopLeft_ShouldSucceed()
    {
        // Arrange
        var sourceStream = CreateTestImage(800, 600);
        var outputPath = Path.Combine(_testOutputPath, "watermark_topleft.jpg");

        var watermarkOptions = new WatermarkOptions
        {
            Type = WatermarkType.Text,
            Text = "TopLeft",
            FontSize = 20,
            Opacity = 0.7f,
            Position = WatermarkPosition.TopLeft,
            Color = "#FF0000",
            Padding = 10
        };

        // Act
        var result = await _imageProcessor.AddWatermarkAsync(sourceStream, outputPath, watermarkOptions);

        // Assert
        result.Success.ShouldBeTrue();
        File.Exists(outputPath).ShouldBeTrue();

        // 清理
        sourceStream.Dispose();
    }

    [Fact(DisplayName = "测试水印位置 - 居中")]
    public async Task AddWatermark_Center_ShouldSucceed()
    {
        // Arrange
        var sourceStream = CreateTestImage(800, 600);
        var outputPath = Path.Combine(_testOutputPath, "watermark_center.jpg");

        var watermarkOptions = new WatermarkOptions
        {
            Type = WatermarkType.Text,
            Text = "Center Watermark",
            FontSize = 32,
            Opacity = 0.3f,
            Position = WatermarkPosition.Center,
            Color = "#000000",
            Padding = 0
        };

        // Act
        var result = await _imageProcessor.AddWatermarkAsync(sourceStream, outputPath, watermarkOptions);

        // Assert
        result.Success.ShouldBeTrue();
        File.Exists(outputPath).ShouldBeTrue();

        // 清理
        sourceStream.Dispose();
    }

    [Fact(DisplayName = "测试移除 EXIF 信息")]
    public async Task RemoveExif_ShouldSucceed()
    {
        // Arrange
        var sourceStream = CreateTestImage(800, 600);
        var outputPath = Path.Combine(_testOutputPath, "no_exif_test.jpg");

        // Act
        var removed = await _imageProcessor.RemoveExifAsync(sourceStream, outputPath);

        // Assert
        removed.ShouldBeTrue();
        File.Exists(outputPath).ShouldBeTrue();

        // 验证 EXIF 已移除
        using var outputImage = await Image.LoadAsync(outputPath);
        outputImage.Metadata.ExifProfile.ShouldBeNull();

        // 清理
        sourceStream.Dispose();
    }
}
