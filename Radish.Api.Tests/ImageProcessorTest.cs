using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Radish.Common.OptionTool;
using Radish.Infrastructure.ImageProcessing;
using Xunit;
using WatermarkOptions = Radish.Infrastructure.ImageProcessing.WatermarkOptions;

namespace Radish.Api.Tests;

public sealed class ImageProcessorTest : IDisposable
{
    private readonly IImageProcessor _processor;
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"radish-images-{Guid.NewGuid():N}");

    public ImageProcessorTest()
    {
        Directory.CreateDirectory(_directory);
        var options = new FileStorageOptions();
        options.Watermark.Text.Font.UseRelativeSize = false;
        _processor = new RustImageProcessor(Options.Create(options));
    }
    public void Dispose() => Directory.Delete(_directory, true);
    private string Output(string name) => Path.Combine(_directory, name);

    [Theory]
    [InlineData(".jpg", "JPEG")]
    [InlineData(".png", "PNG")]
    [InlineData(".webp", "WEBP")]
    [InlineData(".gif", "GIF")]
    [InlineData(".bmp", "BMP")]
    [InlineData(".ico", "ICO")]
    public async Task Thumbnail_ShouldUseRealNativeCodec(string extension, string format)
    {
        using var source = ImageFixture.Create();
        source.Position = 5;
        var result = await _processor.GenerateThumbnailAsync(source, Output("thumbnail" + extension), cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.Success, result.ErrorCode);
        Assert.Equal(150, result.Width);
        Assert.Equal(112, result.Height);
        Assert.Equal(format, result.Format);
        Assert.Equal(5, source.Position);
        using var encoded = File.OpenRead(result.OutputPath);
        var info = await _processor.GetImageInfoAsync(encoded, cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(info);
        Assert.Equal(format, info.Format);
        Assert.Equal(result.Width, info.Width);
    }

    [Theory]
    [InlineData(WatermarkPosition.TopLeft)]
    [InlineData(WatermarkPosition.TopRight)]
    [InlineData(WatermarkPosition.BottomLeft)]
    [InlineData(WatermarkPosition.BottomRight)]
    [InlineData(WatermarkPosition.Center)]
    public async Task ChineseWatermark_ShouldKeepDimensions(WatermarkPosition position)
    {
        using var source = ImageFixture.Create();
        var result = await _processor.AddWatermarkAsync(source, Output("text.png"), new WatermarkOptions
        { Text = "萝卜 Radish", FontSize = 24, Position = position, Color = "#FF000080", Opacity = 0.5f }, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.Success, result.ErrorCode);
        Assert.Equal((800, 600), ImageFixture.PngSize(result.OutputPath));
        Assert.False(source.ToArray().SequenceEqual(File.ReadAllBytes(result.OutputPath)));
    }

    [Fact]
    public async Task ImageWatermark_RequiresRealImageAndProducesOutput()
    {
        using var source = ImageFixture.Create();
        using var mark = ImageFixture.Create(32, 32);
        File.WriteAllBytes(Output("mark.png"), mark.ToArray());
        var result = await _processor.AddWatermarkAsync(source, Output("image.png"), new WatermarkOptions
        { Type = WatermarkType.Image, ImagePath = Output("mark.png"), Scale = 0.1f }, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.Success, result.ErrorCode);
        var failure = await _processor.AddWatermarkAsync(source, Output("missing.png"), new WatermarkOptions { Type = WatermarkType.Image }, cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(failure.Success);
        Assert.False(File.Exists(Output("missing.png")));
    }

    [Fact]
    public async Task MultipleSizes_AndMetadataRemoval_ShouldUseNativeOutputs()
    {
        using var source = ImageFixture.Create();
        var results = await _processor.GenerateMultipleSizesAsync(source, Output("base.png"), new List<ImageSize>
        { new() { Name = "small", Width = 100, Height = 100 }, new() { Name = "wide", Width = 400, Height = 0 } }, cancellationToken: TestContext.Current.CancellationToken);
        Assert.All(results, result => Assert.True(result.Success, result.ErrorCode));
        Assert.Equal((100, 75), ImageFixture.PngSize(Output("base_small.png")));
        Assert.Equal((400, 300), ImageFixture.PngSize(Output("base_wide.png")));
        var removed = await _processor.RemoveExifAsync(source, Output("private.png"), cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(removed.Success, removed.ErrorCode);
        using var output = File.OpenRead(removed.OutputPath);
        Assert.False((await _processor.GetImageInfoAsync(output, cancellationToken: TestContext.Current.CancellationToken))!.HasExif);
    }

    [Fact]
    public async Task InvalidContent_Cancellation_AndUnknownOutputMustNotSucceed()
    {
        using var bad = new MemoryStream("PRIVATE_SENTINEL"u8.ToArray());
        Assert.False(await _processor.IsValidImageAsync(bad, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Null(await _processor.GetImageInfoAsync(bad, cancellationToken: TestContext.Current.CancellationToken));
        using var source = ImageFixture.Create();
        var unsupported = await _processor.CompressAsync(source, Output("output.tmp"), cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(unsupported.Success);
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _processor.ResizeAsync(source, Output("cancel.png"), 100, 100, cancellationToken: cancel.Token));
        Assert.False(File.Exists(Output("cancel.png")));
    }

    [Fact]
    public async Task UnicodePath_AndBatchCommitFailure_ShouldPreserveExistingTarget()
    {
        using var source = ImageFixture.Create(32, 24);
        var existing = Output("中文_small.png");
        File.WriteAllBytes(existing, [1, 2, 3]);
        Directory.CreateDirectory(Output("中文_large.png"));
        var result = await _processor.GenerateMultipleSizesAsync(source, Output("中文.png"),
            [new() { Name = "small", Width = 8, Height = 8 }, new() { Name = "large", Width = 16, Height = 16 }],
            TestContext.Current.CancellationToken);
        Assert.All(result, item => Assert.False(item.Success));
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(existing));
        Assert.Empty(Directory.GetDirectories(_directory, ".radish-image-*"));
        var success = await _processor.ResizeAsync(source, Output("恢复.png"), 8, 6,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(success.Success, success.ErrorCode);
    }

    [Theory]
    [InlineData("Regular")]
    [InlineData("Bold")]
    [InlineData("Italic")]
    [InlineData("BoldItalic")]
    public async Task FontStyles_ShouldRenderMultilineChineseAndBidi(string style)
    {
        var options = new FileStorageOptions();
        options.Watermark.Text.Font.UseRelativeSize = false;
        options.Watermark.Text.Font.Style = style;
        var processor = new RustImageProcessor(Options.Create(options));
        using var source = ImageFixture.Create(400, 240);
        var result = await processor.AddWatermarkAsync(source, Output("排版.png"),
            new WatermarkOptions { Text = "萝卜 A\u0301\nالعربية 2026", FontSize = 24 },
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.Success, result.ErrorCode);
    }

    [Fact]
    public async Task RepeatedNativeRequests_ShouldKeepTemporaryOutputsBounded()
    {
        using var source = ImageFixture.Create(256, 192);
        for (var i = 0; i < 20; i++)
            Assert.True((await _processor.CompressAsync(source, Output("warmup.png"),
                cancellationToken: TestContext.Current.CancellationToken)).Success);
        File.Delete(Output("warmup.png"));
        var durations = new List<double>();
        var residentBytes = new List<long>();
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        for (var i = 0; i < 1000; i++)
        {
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            var cancellation = TestContext.Current.CancellationToken;
            var result = (i % 4) switch
            {
                0 => await _processor.ResizeAsync(source, Output("repeat.png"), 128, 96,
                    cancellationToken: cancellation),
                1 => await _processor.CompressAsync(source, Output("repeat.jpg"),
                    cancellationToken: cancellation),
                2 => await _processor.CompressAsync(source, Output("repeat.webp"),
                    cancellationToken: cancellation),
                _ => await _processor.AddWatermarkAsync(source, Output("watermark.png"),
                    new WatermarkOptions { Text = "中文验收", FontSize = 16 },
                    cancellationToken: cancellation)
            };
            Assert.True(result.Success, result.ErrorCode);
            durations.Add(System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            if (i % 100 == 99)
            {
                process.Refresh();
                residentBytes.Add(process.WorkingSet64);
            }
        }
        Assert.Equal(4, Directory.GetFiles(_directory).Length);
        Assert.Empty(Directory.GetDirectories(_directory));
        if (Environment.GetEnvironmentVariable("RADISH_IMAGE_STRESS_REPORT") is { Length: > 0 } report)
        {
            Assert.True(Path.IsPathFullyQualified(report));
            durations.Sort();
            process.Refresh();
            await File.WriteAllTextAsync(report, System.Text.Json.JsonSerializer.Serialize(new
            {
                iterations = 1000, warmup = 20, concurrency = 1, width = 256, height = 192,
                p50Milliseconds = durations[499], p95Milliseconds = durations[949],
                maximumMilliseconds = durations[^1],
                peakResidentBytes = process.PeakWorkingSet64 > 0 ? (long?)process.PeakWorkingSet64 : null,
                sampledMaximumResidentBytes = residentBytes.Max(),
                residentBytes, outputBytes = Directory.GetFiles(_directory).Sum(path => new FileInfo(path).Length)
            }), TestContext.Current.CancellationToken);
        }
    }
}
